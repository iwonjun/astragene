using System;
using System.Collections.Generic;
using RtsGame.Sim.World;

namespace RtsGame.Sim.Commands;

public enum LockstepStatus { Running, Waiting, Desynced }
public readonly record struct DesyncInfo(long Tick, int Player, ulong Local, ulong Remote);

/// <summary>
/// Transport-free lockstep driver. Every participant sends exactly one CommandPacket per turn.
/// A packet for turn S carries the full world hash at the start of turn S-HashLag when that turn
/// is a checkpoint (every 5 turns = 10 ticks); HashLag equals the maximum input delay so the
/// hash always exists when the packet is created.
/// </summary>
public sealed class LockstepSession
{
    public const int TicksPerTurn = 2, CheckpointTurns = 5, HashLag = 6;
    private readonly SimWorld _world;
    private readonly TurnManager _turns;
    private readonly ReplayLog? _replay;
    private readonly List<Command> _local = new();
    private readonly Queue<CommandPacket> _outbox = new();
    private readonly ulong[] _checkpoints = new ulong[TurnManager.Window];
    private readonly int[] _checkpointTurn = new int[TurnManager.Window];
    private readonly List<(int Player, int Turn, ulong Hash)> _pending = new();
    private bool _midTurn;
    private int _nextSendTurn;
    public int LocalPlayer { get; }
    public SimWorld World => _world;
    public LockstepStatus Status { get; private set; } = LockstepStatus.Running;
    public DesyncInfo? Desync { get; private set; }
    public int NextTurn => _turns.NextTurn;
    public int InputDelay => _turns.InputDelay;
    public int MissingMask => _midTurn ? 0 : _turns.MissingMask;
    public int VerifiedCheckpoints { get; private set; }
    public LockstepSession(SimWorld world, int localPlayer, ReadOnlySpan<int> players, int inputDelay = 3, ReplayLog? replay = null)
    {
        _world = world; LocalPlayer = localPlayer; _replay = replay;
        _turns = new TurnManager(players); _turns.SetInputDelay(inputDelay);
        bool found = false; foreach (int p in players) found |= p == localPlayer;
        if (!found) throw new ArgumentException("Local player must participate.");
        Array.Fill(_checkpointTurn, -1);
        RecordCheckpoint(); Flush();
    }
    /// <summary>Changes only this player's scheduling. Growth sends extra empty turns; shrink skips sends.</summary>
    public void SetInputDelay(int turns) => _turns.SetInputDelay(turns);
    public void QueueLocal(in Command command)
    {
        if (command.Player != LocalPlayer) throw new ArgumentException("Local commands must belong to the local player.");
        if (_local.Count < CommandPacket.MaxCommands) _local.Add(command);
    }
    public bool TryDequeueOutgoing(out CommandPacket packet) => _outbox.TryDequeue(out packet!);
    public void ScheduleLeave(int player, int turn) => _turns.ScheduleLeave(player, Math.Max(turn, _turns.NextTurn));
    /// <summary>Accepts a remote or loopback turn packet. Control packets (turn -1) are rejected.</summary>
    public bool Receive(CommandPacket packet)
    {
        if (packet.Turn < 0) return false;
        bool fresh = _turns.Submit(packet);
        if (fresh && packet.PlayerId != LocalPlayer && packet.Hash != 0) { _pending.Add((packet.PlayerId, packet.Turn - HashLag, packet.Hash)); CheckPending(); }
        return fresh;
    }
    /// <summary>Runs one 50 ms simulation tick if the current turn is complete.</summary>
    public bool TryStep()
    {
        if (Status == LockstepStatus.Desynced) return false;
        if (!_midTurn)
        {
            if (!_turns.TryTake(out _, out var commands)) { Status = LockstepStatus.Waiting; return false; }
            Status = LockstepStatus.Running;
            _world.Tick(commands); _midTurn = true;
            _replay?.Append(commands, _world.Hash());
        }
        else
        {
            _world.Tick(ReadOnlySpan<Command>.Empty); _midTurn = false;
            _replay?.Append(ReadOnlySpan<Command>.Empty, _world.Hash());
            RecordCheckpoint(); Flush(); CheckPending();
        }
        return Status != LockstepStatus.Desynced;
    }
    private void RecordCheckpoint()
    {
        int turn = _turns.NextTurn;
        if (turn % CheckpointTurns != 0) return;
        _checkpoints[turn % TurnManager.Window] = _world.Hash(); _checkpointTurn[turn % TurnManager.Window] = turn;
    }
    private void Flush()
    {
        int target = _turns.NextTurn + _turns.InputDelay;
        while (_nextSendTurn <= target)
        {
            int turn = _nextSendTurn++;
            var commands = turn == target ? _local.ToArray() : Array.Empty<Command>();
            if (turn == target) _local.Clear();
            int checkpoint = turn - HashLag;
            ulong hash = checkpoint >= 0 && checkpoint % CheckpointTurns == 0 ? _checkpoints[checkpoint % TurnManager.Window] : 0;
            var packet = new CommandPacket(turn, LocalPlayer, commands, hash);
            _turns.Submit(packet); _outbox.Enqueue(packet);
        }
    }
    private void CheckPending()
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            var (player, turn, remote) = _pending[i];
            int slot = turn % TurnManager.Window;
            if (turn > _turns.NextTurn || (turn == _turns.NextTurn && _midTurn) || _checkpointTurn[slot] != turn)
            {
                if (turn < _turns.NextTurn - TurnManager.Window / 2) _pending.RemoveAt(i); // too old to compare; never expected
                continue;
            }
            _pending.RemoveAt(i);
            if (_checkpoints[slot] == remote) { VerifiedCheckpoints++; continue; }
            Status = LockstepStatus.Desynced;
            Desync ??= new DesyncInfo((long)turn * TicksPerTurn, player, _checkpoints[slot], remote);
        }
    }
}
