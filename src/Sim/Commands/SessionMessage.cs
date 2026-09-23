using System;
using System.Buffers.Binary;
using System.Text;
using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;

namespace RtsGame.Sim.Commands;

public enum SessionKind { Hello = 1, Assign, Lobby, SetFaction, SetReady, Start, Leave, Chat, Reject }

/// <summary>
/// Lobby/control payload carried inside a Session command of a CommandPacket at turn -1.
/// Field mapping: Definition=kind, Entity.Index=A, Entity.Generation=B, Target.Index=C,
/// Target.Generation=F, Position.X.Raw=D, Position.Y.Raw=E.
/// </summary>
public readonly record struct SessionMessage(SessionKind Kind, int A = 0, uint B = 0, int C = 0, uint F = 0, long D = 0, long E = 0)
{
    public const int ControlTurn = -1, Protocol = 1, MaxChatBytes = 480, ChatChunkBytes = 24;
    public Command ToCommand(int player) =>
        new(CommandType.Session, player, new EntityId(A, B), new EntityId(C, F), new Fix2(Fix64.FromRaw(D), Fix64.FromRaw(E)), (int)Kind);
    public static bool TryFrom(in Command command, out SessionMessage message)
    {
        message = default;
        if (command.Type != CommandType.Session || command.Definition < (int)SessionKind.Hello || command.Definition > (int)SessionKind.Reject) return false;
        message = new SessionMessage((SessionKind)command.Definition, command.Entity.Index, command.Entity.Generation, command.Target.Index,
            command.Target.Generation, command.Position.X.Raw, command.Position.Y.Raw);
        return true;
    }
    public static CommandPacket Packet(int player, SessionMessage message) =>
        new(ControlTurn, player, new[] { message.ToCommand(player) });

    public static SessionMessage Hello(ulong contentHash, ulong mapHash) => new(SessionKind.Hello, Protocol, D: unchecked((long)contentHash), E: unchecked((long)mapHash));
    public static SessionMessage Assign(int player) => new(SessionKind.Assign, player);
    public static SessionMessage SetFaction(int faction) => new(SessionKind.SetFaction, faction);
    public static SessionMessage SetReady(bool ready) => new(SessionKind.SetReady, ready ? 1 : 0);
    public static SessionMessage Reject(int reason) => new(SessionKind.Reject, reason);
    public static SessionMessage Leave(int player, int turn) => new(SessionKind.Leave, player, C: turn);
    /// <summary>A=slot bits (per slot: occupied=1, faction=2, ready=4, shifted by 4*slot), C=map, B=input delay, D=seed.</summary>
    public static SessionMessage Lobby(LobbySlots slots, int map, int inputDelay, ulong seed) => new(SessionKind.Lobby, slots.Bits, (uint)inputDelay, map, D: unchecked((long)seed));
    public static SessionMessage Start(LobbySlots slots, int map, int inputDelay, ulong seed) => new(SessionKind.Start, slots.Bits, (uint)inputDelay, map, D: unchecked((long)seed));

    /// <summary>Chat text is split into 24-byte UTF-8 chunks; every chunk command lives in one packet.</summary>
    public static CommandPacket ChatPacket(int player, string text, bool team)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        int length = Math.Min(bytes.Length, MaxChatBytes);
        while (length > 0 && length < bytes.Length && (bytes[length] & 0xC0) == 0x80) length--; // keep whole code points
        int chunks = Math.Max(1, (length + ChatChunkBytes - 1) / ChatChunkBytes);
        var commands = new Command[chunks];
        Span<byte> chunk = stackalloc byte[ChatChunkBytes];
        for (int i = 0; i < chunks; i++)
        {
            chunk.Clear();
            int start = i * ChatChunkBytes, count = Math.Clamp(length - start, 0, ChatChunkBytes);
            bytes.AsSpan(start, count).CopyTo(chunk);
            var m = new SessionMessage(SessionKind.Chat, i, (uint)(length | (team ? 1 << 16 : 0)),
                BinaryPrimitives.ReadInt32LittleEndian(chunk.Slice(0, 4)), BinaryPrimitives.ReadUInt32LittleEndian(chunk.Slice(4, 4)), BinaryPrimitives.ReadInt64LittleEndian(chunk.Slice(8, 8)), BinaryPrimitives.ReadInt64LittleEndian(chunk.Slice(16, 8)));
            commands[i] = m.ToCommand(player);
        }
        return new CommandPacket(ControlTurn, player, commands);
    }
    public static bool TryReadChat(CommandPacket packet, out string text, out bool team)
    {
        text = ""; team = false;
        var commands = packet.Commands;
        if (packet.Turn != ControlTurn || commands.Length == 0 || !TryFrom(commands[0], out var first) || first.Kind != SessionKind.Chat) return false;
        int length = (int)(first.B & 0xFFFF);
        team = (first.B >> 16 & 1) == 1;
        if (length > MaxChatBytes || commands.Length != Math.Max(1, (length + ChatChunkBytes - 1) / ChatChunkBytes)) return false;
        var bytes = new byte[commands.Length * ChatChunkBytes];
        for (int i = 0; i < commands.Length; i++)
        {
            if (!TryFrom(commands[i], out var m) || m.Kind != SessionKind.Chat || m.A != i || m.B != first.B) return false;
            var span = bytes.AsSpan(i * ChatChunkBytes, ChatChunkBytes);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(0, 4), m.C); BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), m.F);
            BinaryPrimitives.WriteInt64LittleEndian(span.Slice(8, 8), m.D); BinaryPrimitives.WriteInt64LittleEndian(span.Slice(16, 8), m.E);
        }
        text = Encoding.UTF8.GetString(bytes, 0, length);
        return true;
    }
}

/// <summary>Four lobby slots packed into 16 bits so lobby state fits one wire command.</summary>
public readonly record struct LobbySlots(int Bits)
{
    public bool Occupied(int slot) => (Bits >> (slot * 4) & 1) != 0;
    public int Faction(int slot) => Bits >> (slot * 4 + 1) & 1;
    public bool Ready(int slot) => (Bits >> (slot * 4 + 2) & 1) != 0;
    public int Count { get { int n = 0; for (int s = 0; s < 4; s++) if (Occupied(s)) n++; return n; } }
    public LobbySlots With(int slot, bool occupied, int faction, bool ready)
    {
        if ((uint)slot >= 4 || (uint)faction > 1) throw new ArgumentOutOfRangeException(nameof(slot));
        int cleared = Bits & ~(0xF << (slot * 4));
        int value = occupied ? 1 | faction << 1 | (ready ? 4 : 0) : 0;
        return new LobbySlots(cleared | value << (slot * 4));
    }
    public int[] Players() { int n = 0; var list = new int[Count]; for (int s = 0; s < 4; s++) if (Occupied(s)) list[n++] = s; return list; }
}
