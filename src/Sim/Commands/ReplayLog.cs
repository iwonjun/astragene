using System;
using System.Collections.Generic;
using System.IO;
using RtsGame.Sim.Ai;
using RtsGame.Sim.Core;
using RtsGame.Sim.World;
namespace RtsGame.Sim.Commands;

/// <summary>
/// Replay = map + spawns + seed + content hash + the exact command list applied on every tick
/// and the world hash after that tick. Lockstep turns apply commands on the first tick of a turn;
/// local matches may apply them on any tick, so the stream is stored per tick.
/// </summary>
public sealed class ReplayLog
{
    private const uint Magic=0x52504741;
    private const int Version=3,MaxTicks=20*60*60*6;
    public MapData Map {get;}
    public ulong Seed {get;}
    public ulong ContentHash {get;}
    private readonly SpawnSpec[] _spawns;
    private readonly AiSeat[] _ai;
    private readonly List<Command[]> _ticks=new();
    private readonly List<ulong> _hashes=new();
    public int TickCount=>_hashes.Count;
    public ReplayLog(MapData map,ReadOnlySpan<SpawnSpec> spawns,ulong seed,ulong contentHash=0,ReadOnlySpan<AiSeat> ai=default){Map=map;_spawns=spawns.ToArray();Seed=seed;ContentHash=contentHash==0?MatchSetup.ContentHash():contentHash;_ai=ai.ToArray();}
    public SimWorld CreateWorld()=>new(Map,_spawns,Seed,ai:_ai.Length==0?null:_ai);
    public ReadOnlySpan<AiSeat> AiSeats=>_ai;
    public ReadOnlySpan<SpawnSpec> Spawns=>_spawns;
    public void Append(ReadOnlySpan<Command> commands,ulong hashAfter)
    {
        if(_ticks.Count>=MaxTicks)throw new InvalidOperationException("Replay duration limit reached.");
        _ticks.Add(commands.Length==0?Array.Empty<Command>():commands.ToArray());_hashes.Add(hashAfter);
    }
    public ulong HashAt(int tick)=>_hashes[tick];
    public ReadOnlySpan<Command> CommandsAtTick(int tick)=>_ticks[tick];
    public byte[] Encode()
    {
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);writer.Write(Magic);writer.Write(Version);writer.Write(Seed);writer.Write(ContentHash);
        var map=MapLoader.Save(Map);writer.Write(map.Length);writer.Write(map);writer.Write(_spawns.Length);
        foreach(var s in _spawns){writer.Write(s.Player);writer.Write(s.Definition);writer.Write(s.Building);writer.Write(s.Position.X.Raw);writer.Write(s.Position.Y.Raw);}
        writer.Write(_ai.Length);foreach(var a in _ai){writer.Write(a.Player);writer.Write((int)a.Difficulty);}
        writer.Write(_ticks.Count);Span<byte> bytes=stackalloc byte[Command.ByteSize];
        for(int t=0;t<_ticks.Count;t++){var tick=_ticks[t];writer.Write((ushort)tick.Length);foreach(var c in tick){c.Write(bytes);writer.Write(bytes);}writer.Write(_hashes[t]);}
        return stream.ToArray();
    }
    public static ReplayLog Decode(byte[] data)
    {
        if(data.Length>128*1024*1024)throw new InvalidDataException("Replay too large.");
        try
        {
            using var stream=new MemoryStream(data,false);using var reader=new BinaryReader(stream);
            if(reader.ReadUInt32()!=Magic || reader.ReadInt32()!=Version)throw new InvalidDataException("Replay format mismatch.");ulong seed=reader.ReadUInt64(),content=reader.ReadUInt64();
            int length=reader.ReadInt32();if(length!=MapLoader.ByteLength)throw new InvalidDataException("Map length mismatch.");var map=MapLoader.Load(reader.ReadBytes(length));
            int count=reader.ReadInt32();if(count<0 || count>1024)throw new InvalidDataException("Invalid spawn count.");var spawns=new SpawnSpec[count];
            for(int i=0;i<count;i++)spawns[i]=new SpawnSpec(reader.ReadInt32(),reader.ReadInt32(),reader.ReadBoolean(),new Fix2(Fix64.FromRaw(reader.ReadInt64()),Fix64.FromRaw(reader.ReadInt64())));
            int seats=reader.ReadInt32();if(seats<0 || seats>4)throw new InvalidDataException("Invalid AI seat count.");var ai=new AiSeat[seats];
            for(int i=0;i<seats;i++){int player=reader.ReadInt32(),difficulty=reader.ReadInt32();if((uint)player>=4 || (uint)difficulty>2)throw new InvalidDataException("Invalid AI seat.");ai[i]=new AiSeat(player,(AiDifficulty)difficulty);}
            var replay=new ReplayLog(map,spawns,seed,content,ai);int ticks=reader.ReadInt32();if(ticks<0 || ticks>MaxTicks)throw new InvalidDataException("Invalid duration.");
            for(int t=0;t<ticks;t++)
            {
                int n=reader.ReadUInt16();if(n>4*CommandPacket.MaxCommands)throw new InvalidDataException("Invalid tick command count.");
                var commands=n==0?Array.Empty<Command>():new Command[n];for(int i=0;i<n;i++)commands[i]=Command.Read(reader.ReadBytes(Command.ByteSize));
                replay.Append(commands,reader.ReadUInt64());
            }
            if(stream.Position!=stream.Length)throw new InvalidDataException("Replay trailing bytes.");return replay;
        }
        catch(EndOfStreamException e){throw new InvalidDataException("Replay truncated.",e);}
    }
}
