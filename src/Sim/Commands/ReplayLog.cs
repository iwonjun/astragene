using System;
using System.Collections.Generic;
using System.IO;
using RtsGame.Sim.Core;
using RtsGame.Sim.World;
namespace RtsGame.Sim.Commands;
public sealed class ReplayLog
{
    private const uint Magic=0x52504741;
    public MapData Map {get;}
    public ulong Seed {get;}
    private readonly SpawnSpec[] _spawns;
    private readonly List<Command[]> _turns=new();
    private readonly List<ulong> _hashes=new();
    public int TickCount=>_hashes.Count;
    public int TurnCount=>_turns.Count;
    public ReplayLog(MapData map,ReadOnlySpan<SpawnSpec> spawns,ulong seed){Map=map;_spawns=spawns.ToArray();Seed=seed;}
    public SimWorld CreateWorld()=>new(Map,_spawns,Seed);
    public void AppendTurn(ReadOnlySpan<Command> commands)=>_turns.Add(commands.ToArray());
    public void AppendHash(ulong hash)=>_hashes.Add(hash);
    public ulong HashAt(int tick)=>_hashes[tick];
    public ReadOnlySpan<Command> CommandsAtTick(int tick)=>tick%2==0?_turns[tick/2]:ReadOnlySpan<Command>.Empty;
    public byte[] Encode()
    {
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);writer.Write(Magic);writer.Write(1);writer.Write(Seed);
        var map=MapLoader.Save(Map);writer.Write(map.Length);writer.Write(map);writer.Write(_spawns.Length);
        foreach(var s in _spawns){writer.Write(s.Player);writer.Write(s.Definition);writer.Write(s.Building);writer.Write(s.Position.X.Raw);writer.Write(s.Position.Y.Raw);}
        writer.Write(_turns.Count);Span<byte> bytes=stackalloc byte[Command.ByteSize];
        foreach(var turn in _turns){writer.Write(turn.Length);foreach(var c in turn){c.Write(bytes);writer.Write(bytes);}}
        writer.Write(_hashes.Count);foreach(ulong h in _hashes)writer.Write(h);return stream.ToArray();
    }
    public static ReplayLog Decode(byte[] data)
    {
        if(data.Length>128*1024*1024)throw new InvalidDataException("Replay too large.");
        using var stream=new MemoryStream(data,false);using var reader=new BinaryReader(stream);
        if(reader.ReadUInt32()!=Magic || reader.ReadInt32()!=1)throw new InvalidDataException("Replay format mismatch.");ulong seed=reader.ReadUInt64();
        int length=reader.ReadInt32();if(length!=MapLoader.ByteLength)throw new InvalidDataException("Map length mismatch.");var map=MapLoader.Load(reader.ReadBytes(length));
        int count=reader.ReadInt32();if(count<0 || count>1024)throw new InvalidDataException("Invalid spawn count.");var spawns=new SpawnSpec[count];
        for(int i=0;i<count;i++)spawns[i]=new SpawnSpec(reader.ReadInt32(),reader.ReadInt32(),reader.ReadBoolean(),new Fix2(Fix64.FromRaw(reader.ReadInt64()),Fix64.FromRaw(reader.ReadInt64())));
        var replay=new ReplayLog(map,spawns,seed);int turns=reader.ReadInt32();if(turns<0 || turns>360000)throw new InvalidDataException("Invalid duration.");
        for(int t=0;t<turns;t++){int n=reader.ReadInt32();if(n<0 || n>4*CommandPacket.MaxCommands)throw new InvalidDataException("Invalid turn command count.");var commands=new Command[n];for(int i=0;i<n;i++)commands[i]=Command.Read(reader.ReadBytes(Command.ByteSize));replay.AppendTurn(commands);}
        int hashes=reader.ReadInt32();if(hashes<0 || hashes>turns*2 || hashes<Math.Max(0,turns*2-1))throw new InvalidDataException("Invalid hash count.");
        for(int i=0;i<hashes;i++)replay.AppendHash(reader.ReadUInt64());if(stream.Position!=stream.Length)throw new InvalidDataException("Replay trailing bytes.");return replay;
    }
}
