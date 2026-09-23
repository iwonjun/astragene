using System;
using System.Buffers.Binary;
using System.IO;
namespace RtsGame.Sim.Commands;

// The only application message on the wire, including lobby/control envelopes at turn -1.
public sealed class CommandPacket
{
    public const int HeaderSize=24,MaxCommands=256;
    private readonly Command[] _commands;
    public int Turn {get;}
    public int PlayerId {get;}
    public ulong Hash {get;}
    public ReadOnlySpan<Command> Commands=>_commands;
    public CommandPacket(int turn,int playerId,ReadOnlySpan<Command> commands,ulong hash=0)
    {
        if((uint)playerId>=4 || commands.Length>MaxCommands)throw new ArgumentException("Invalid packet.");
        Turn=turn;PlayerId=playerId;Hash=hash;_commands=commands.ToArray();
        foreach(var c in _commands)if(c.Player!=playerId)throw new ArgumentException("Command player mismatch.");
    }
    public byte[] Encode()
    {
        var bytes=new byte[HeaderSize+_commands.Length*Command.ByteSize];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes,0x50434741);BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4),1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6),(ushort)_commands.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8),Turn);BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12),PlayerId);BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(16),Hash);
        for(int i=0;i<_commands.Length;i++)_commands[i].Write(bytes.AsSpan(HeaderSize+i*Command.ByteSize,Command.ByteSize));return bytes;
    }
    public static CommandPacket Decode(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length<HeaderSize || BinaryPrimitives.ReadUInt32LittleEndian(bytes)!=0x50434741 || BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(4))!=1)throw new InvalidDataException("Packet header mismatch.");
        int count=BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(6)),player=BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(12));
        if(count>MaxCommands || (uint)player>=4 || bytes.Length!=HeaderSize+count*Command.ByteSize)throw new InvalidDataException("Packet size/player mismatch.");
        var commands=new Command[count];for(int i=0;i<count;i++){commands[i]=Command.Read(bytes.Slice(HeaderSize+i*Command.ByteSize,Command.ByteSize));if(commands[i].Player!=player)throw new InvalidDataException("Command ownership mismatch.");}
        return new CommandPacket(BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8)),player,commands,BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(16)));
    }
}
