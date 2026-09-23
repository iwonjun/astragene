using System;
using System.Collections.Generic;
using System.IO;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;
using RtsGame.Sim.World;
using Xunit;

namespace RtsGame.Sim.Tests;

public sealed class NetworkTests
{
    private static MapData Duel() => MapLoader.Load(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "duel.map")));
    private static Fix2 Pos(int x, int y) => new(Fix64.FromInt(x), Fix64.FromInt(y));

    /// <summary>Reliable ordered link with per-packet latency and loss converted into retransmission delay.</summary>
    private sealed class Link
    {
        private readonly Queue<(int At, byte[] Bytes)> _queue = new();
        private readonly Random _random;
        private readonly int _latency, _jitter, _lossPercent, _retransmit;
        private int _last;
        public int Sent, Lost;
        public Link(int seed, int latency, int jitter, int lossPercent, int retransmit)
        { _random = new Random(seed); _latency = latency; _jitter = jitter; _lossPercent = lossPercent; _retransmit = retransmit; }
        public void Send(int now, CommandPacket packet)
        {
            int at = now + _latency + _random.Next(_jitter + 1);
            while (_random.Next(100) < _lossPercent) { at += _retransmit; Lost++; }
            _last = Math.Max(_last, at); Sent++;
            _queue.Enqueue((_last, packet.Encode()));
        }
        public void Deliver(int now, LockstepSession to)
        {
            while (_queue.Count > 0 && _queue.Peek().At <= now) to.Receive(CommandPacket.Decode(_queue.Dequeue().Bytes));
        }
    }

    /// <summary>Scripted opponent using only the player's filtered view; commands are identical on every client.</summary>
    private static void Bot(LockstepSession session, Random random, int frame)
    {
        if (frame % 7 != 0) return;
        var view = session.World.ViewFor(session.LocalPlayer);
        var own = new List<EntitySnapshot>();
        for (int i = 0; i < view.Capacity; i++) { var id = view.IdAt(i); if (id != EntityId.None) { var e = view.Get(id); if (e.Owner.Player == session.LocalPlayer) own.Add(e); } }
        if (own.Count == 0) return;
        var pick = own[random.Next(own.Count)];
        int p = session.LocalPlayer;
        if (pick.Type.IsBuilding)
        {
            int unit = pick.Type.Definition == 0 ? 0 : pick.Type.Definition == 6 ? 5 : -1;
            if (unit >= 0 && random.Next(3) == 0) session.QueueLocal(new Command(CommandType.Train, p, pick.Id, EntityId.None, Fix2.Zero, unit));
            return;
        }
        var target = random.Next(4) == 0 ? Pos(p == 0 ? 107 : 20, p == 0 ? 107 : 20) : Pos(20 + random.Next(88), 20 + random.Next(88));
        var type = random.Next(3) == 0 ? CommandType.AttackMove : CommandType.Move;
        session.QueueLocal(new Command(type, p, pick.Id, EntityId.None, target, -1, random.Next(5) == 0));
    }

    private static (LockstepSession A, LockstepSession B, ReplayLog Replay, int Stalls, Link AB) Play(int ticks, int latency, int jitter, int loss, bool changeDelay = false)
    {
        var map = Duel(); var spawns = MatchSetup.Spawns(new[] { 0, 1 }); ulong seed = 0xA57A;
        var replay = new ReplayLog(map, spawns, seed);
        var a = new LockstepSession(new SimWorld(map, spawns, seed), 0, new[] { 0, 1 }, 3, replay);
        var b = new LockstepSession(new SimWorld(map, spawns, seed), 1, new[] { 0, 1 }, 3);
        var ab = new Link(11, latency, jitter, loss, 6); var ba = new Link(12, latency, jitter, loss, 6);
        var botA = new Random(1); var botB = new Random(2);
        int frame = 0, stalls = 0;
        while (a.World.TickNumber < ticks || b.World.TickNumber < ticks)
        {
            if (frame > ticks * 4) throw new InvalidOperationException("Lockstep made no progress.");
            ab.Deliver(frame, b); ba.Deliver(frame, a);
            if (changeDelay && frame == 3000) a.SetInputDelay(6);
            if (changeDelay && frame == 6000) { a.SetInputDelay(2); b.SetInputDelay(5); }
            Bot(a, botA, frame); Bot(b, botB, frame);
            if (a.World.TickNumber < ticks && !a.TryStep() && a.Status == LockstepStatus.Waiting) stalls++;
            if (b.World.TickNumber < ticks) b.TryStep();
            Assert.NotEqual(LockstepStatus.Desynced, a.Status); Assert.NotEqual(LockstepStatus.Desynced, b.Status);
            while (a.TryDequeueOutgoing(out var pa)) ab.Send(frame, pa);
            while (b.TryDequeueOutgoing(out var pb)) ba.Send(frame, pb);
            frame++;
        }
        return (a, b, replay, stalls, ab);
    }

    [Fact]
    public void PacketAndSessionMessagesRoundTripAndRejectForgery()
    {
        var commands = new[] { new Command(CommandType.Move, 2, new EntityId(3, 4), EntityId.None, Pos(5, 6), -1, true) };
        var packet = new CommandPacket(77, 2, commands, 0xDEADBEEFUL);
        var decoded = CommandPacket.Decode(packet.Encode());
        Assert.Equal(77, decoded.Turn); Assert.Equal(2, decoded.PlayerId); Assert.Equal(0xDEADBEEFUL, decoded.Hash); Assert.Equal(commands[0], decoded.Commands[0]);
        Assert.Throws<ArgumentException>(() => new CommandPacket(1, 1, commands));
        var bytes = packet.Encode(); bytes[12] = 1; Assert.Throws<InvalidDataException>(() => CommandPacket.Decode(bytes));
        Assert.Throws<InvalidDataException>(() => CommandPacket.Decode(packet.Encode().AsSpan(0, 30)));

        var slots = new LobbySlots(0).With(0, true, 1, true).With(1, true, 0, false);
        var lobby = SessionMessage.Packet(0, SessionMessage.Lobby(slots, 0, 4, 0xFEEDFACECAFEUL));
        Assert.True(SessionMessage.TryFrom(CommandPacket.Decode(lobby.Encode()).Commands[0], out var m));
        var back = new LobbySlots(m.A);
        Assert.Equal(SessionKind.Lobby, m.Kind); Assert.Equal(0xFEEDFACECAFEUL, unchecked((ulong)m.D)); Assert.Equal(4u, m.B);
        Assert.True(back.Occupied(0) && back.Ready(0) && back.Faction(0) == 1 && back.Occupied(1) && !back.Ready(1) && !back.Occupied(2));
        Assert.Equal(new[] { 0, 1 }, back.Players());

        string text = "gg 잘했어요 — 다음 판도 부탁해요! 🚀 " + new string('x', 600);
        var chat = CommandPacket.Decode(SessionMessage.ChatPacket(1, text, true).Encode());
        Assert.True(SessionMessage.TryReadChat(chat, out var read, out bool team));
        Assert.True(team); Assert.StartsWith("gg 잘했어요", read); Assert.True(System.Text.Encoding.UTF8.GetByteCount(read) <= SessionMessage.MaxChatBytes);
        Assert.False(SessionMessage.TryReadChat(packet, out _, out _));
    }

    [Fact]
    public void TurnManagerWaitsForEveryoneAndRejectsConflicts()
    {
        var turns = new TurnManager(new[] { 0, 1 });
        Assert.Equal(3, turns.MissingMask);
        Assert.True(turns.Submit(new CommandPacket(0, 0, Array.Empty<Command>())));
        Assert.False(turns.Submit(new CommandPacket(0, 0, Array.Empty<Command>())));
        Assert.Throws<InvalidOperationException>(() => turns.Submit(new CommandPacket(0, 0, Array.Empty<Command>(), 5)));
        Assert.False(turns.TryTake(out _, out _)); Assert.Equal(2, turns.MissingMask);
        turns.Submit(new CommandPacket(0, 1, Array.Empty<Command>()));
        Assert.True(turns.TryTake(out var packets, out _)); Assert.Equal(2, packets.Length);
        Assert.Throws<ArgumentException>(() => turns.Submit(new CommandPacket(1 + TurnManager.Window, 0, Array.Empty<Command>())));
        turns.ScheduleLeave(1, 1);
        turns.Submit(new CommandPacket(1, 0, Array.Empty<Command>()));
        Assert.True(turns.TryTake(out _, out var joined));
        Assert.Equal(CommandType.Leave, joined[0].Type); Assert.Equal(1, joined[0].Player);
        Assert.Equal(4, TurnManager.DelayForRtt(180)); Assert.Equal(6, TurnManager.DelayForRtt(2000)); Assert.Equal(2, TurnManager.DelayForRtt(0));
    }

    [Fact]
    public void FifteenMinuteLockstepUnderLatencyAndLossNeverDesyncsAndReplaysExactly()
    {
        const int ticks = 15 * 60 * 20;
        // 200 ms one-way latency (4 ticks) with jitter; 2% loss costs a 300 ms reliable retransmission.
        var (a, b, replay, stalls, link) = Play(ticks, 4, 2, 2, changeDelay: true);
        Assert.Equal(a.World.Hash(), b.World.Hash());
        Assert.True(a.VerifiedCheckpoints >= ticks / 10 - 10, $"verified {a.VerifiedCheckpoints}");
        Assert.True(b.VerifiedCheckpoints >= ticks / 10 - 10);
        Assert.True(link.Lost > 0, "loss model must drop packets");
        Assert.True(stalls < ticks / 4, $"stalled {stalls} frames");
        Assert.Equal(ticks, replay.TickCount);
        var decoded = ReplayLog.Decode(replay.Encode());
        Assert.Equal(-1, ReplayPlayer.Verify(decoded));
        var player = new ReplayPlayer(decoded); player.SeekTo(ticks);
        Assert.Equal(a.World.Hash(), player.World.Hash());
        player.SeekTo(100); Assert.Equal(100, player.Tick); Assert.Equal(replay.HashAt(99), player.World.Hash());
    }

    [Fact]
    public void DivergentWorldIsDetectedAtCheckpointAndDumped()
    {
        var map = Duel(); var spawns = MatchSetup.Spawns(new[] { 0, 1 });
        var a = new LockstepSession(new SimWorld(map, spawns, 5), 0, new[] { 0, 1 });
        var b = new LockstepSession(new SimWorld(map, spawns, 5), 1, new[] { 0, 1 });
        // Corrupt only B's world outside the lockstep stream, as a buggy client would.
        b.World.Tick(new[] { new Command(CommandType.Move, 1, b.World.Entities.IdAt(8), EntityId.None, Pos(60, 60)) });
        for (int frame = 0; frame < 200 && a.Status != LockstepStatus.Desynced; frame++)
        {
            a.TryStep(); b.TryStep();
            while (a.TryDequeueOutgoing(out var p)) b.Receive(p);
            while (b.TryDequeueOutgoing(out var p)) a.Receive(p);
        }
        Assert.Equal(LockstepStatus.Desynced, a.Status);
        var info = a.Desync!.Value;
        Assert.Equal(1, info.Player); Assert.Equal(0, info.Tick % 10); Assert.NotEqual(info.Local, info.Remote);
        Assert.False(a.TryStep());
        string dump = a.World.DumpState();
        Assert.StartsWith("{\"tick\":", dump); Assert.Contains("\"entities\":[", dump); Assert.Contains("\"economyHash\"", dump); Assert.EndsWith("]}", dump);
    }

    [Fact]
    public void LeavingPlayerIsNeutralizedAndMatchContinues()
    {
        var map = Duel(); var spawns = MatchSetup.Spawns(new[] { 0, 1 });
        var a = new LockstepSession(new SimWorld(map, spawns, 9), 0, new[] { 0, 1 });
        var b = new LockstepSession(new SimWorld(map, spawns, 9), 1, new[] { 0, 1 });
        int lastFromB = -1;
        for (int i = 0; i < 40; i++)
        {
            a.TryStep(); b.TryStep();
            while (a.TryDequeueOutgoing(out var p)) b.Receive(p);
            while (b.TryDequeueOutgoing(out var p)) { a.Receive(p); lastFromB = Math.Max(lastFromB, p.Turn); }
        }
        // B's last packets are all delivered; the relay announces the first missing turn.
        a.ScheduleLeave(1, lastFromB + 1);
        for (int i = 0; i < 200; i++) { a.TryStep(); while (a.TryDequeueOutgoing(out _)) { } }
        Assert.Equal(LockstepStatus.Running, a.Status);
        Assert.True(a.World.TickNumber > 200);
        var view = a.World.ViewFor(0);
        Assert.True(a.World.Surrendered(1));
        for (int i = 0; i < a.World.Entities.Capacity; i++) { var id = a.World.Entities.IdAt(i); if (id != EntityId.None) Assert.NotEqual(1, a.World.Entities.Owner[i].Player); }
        // Neutral units can still be attacked without owner-indexed lookups failing.
        var neutral = a.World.Entities.IdAt(7); var own = a.World.Entities.IdAt(1);
        a.World.Entities.Transform[neutral.Index].Position = a.World.Entities.Transform[own.Index].Position + Pos(1, 0);
        for (int i = 0; i < 400; i++) a.World.Tick(ReadOnlySpan<Command>.Empty);
        Assert.NotNull(view);
    }
}
