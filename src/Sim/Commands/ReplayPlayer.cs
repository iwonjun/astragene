using System;
using RtsGame.Sim.World;

namespace RtsGame.Sim.Commands;

/// <summary>Re-simulates a replay tick by tick and compares every tick hash with the recording.</summary>
public sealed class ReplayPlayer
{
    private readonly ReplayLog _log;
    public SimWorld World { get; private set; }
    public int Tick { get; private set; }
    public int FirstMismatchTick { get; private set; } = -1;
    public bool Finished => Tick >= _log.TickCount;
    public int Length => _log.TickCount;
    public ReplayPlayer(ReplayLog log) { _log = log; World = log.CreateWorld(); }
    public bool Step()
    {
        if (Finished) return false;
        World.Tick(_log.CommandsAtTick(Tick));
        if (FirstMismatchTick < 0 && World.Hash() != _log.HashAt(Tick)) FirstMismatchTick = Tick;
        Tick++;
        return true;
    }
    /// <summary>Rewinds by rebuilding from the initial state; lockstep worlds cannot run backwards.</summary>
    public void SeekTo(int tick)
    {
        tick = Math.Clamp(tick, 0, _log.TickCount);
        if (tick < Tick) { World = _log.CreateWorld(); Tick = 0; }
        while (Tick < tick) Step();
    }
    /// <summary>Returns -1 when all recorded ticks match.</summary>
    public static int Verify(ReplayLog log)
    {
        var player = new ReplayPlayer(log);
        while (player.Step()) if (player.FirstMismatchTick >= 0) return player.FirstMismatchTick;
        return player.FirstMismatchTick;
    }
}
