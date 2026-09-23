using System;
using System.Buffers.Binary;
using System.IO;
using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;

namespace RtsGame.Sim.Commands;

public enum CommandType { Move, AttackMove, Attack, Stop, Hold, Patrol, Follow, Build, Train, Cancel, Rally, Gather, Repair, Research, Surrender }
public readonly record struct Command(CommandType Type, int Player, EntityId Entity, EntityId Target, Fix2 Position, int Definition = -1, bool Queued = false)
{
    public const int ByteSize = 48;
    public void Write(Span<byte> bytes)
    {
        if(bytes.Length!=ByteSize) throw new ArgumentException("Command requires 48 bytes.");
        bytes.Clear(); bytes[0]=(byte)Type; bytes[1]=Queued?(byte)1:(byte)0;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(4),Player);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(8),Entity.Index);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(12),Entity.Generation);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(16),Target.Index);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(20),Target.Generation);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.Slice(24),Position.X.Raw);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.Slice(32),Position.Y.Raw);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(40),Definition);
    }
    public static Command Read(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length!=ByteSize || bytes[0]>(byte)CommandType.Surrender || bytes[1]>1 || bytes[2]!=0 || bytes[3]!=0 || BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(44))!=0)
            throw new InvalidDataException("Invalid command encoding.");
        return new Command((CommandType)bytes[0],BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4)),
            new EntityId(BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8)),BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(12))),
            new EntityId(BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(16)),BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(20))),
            new Fix2(Fix64.FromRaw(BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(24))),Fix64.FromRaw(BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(32)))),
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(40)),bytes[1]==1);
    }
}
