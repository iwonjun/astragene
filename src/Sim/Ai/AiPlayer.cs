using System;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.World;

namespace RtsGame.Sim.Ai;

/// <summary>
/// One computer opponent. It lives inside the simulation so every lockstep client runs the same decisions
/// from the same state; it observes only through its VisibilityFilter and acts only through Commands that
/// pass the same validation as human input.
/// </summary>
public sealed class AiPlayer
{
    private readonly AiProfile _profile;
    private readonly AiContext _context = new();
    private readonly StrategyBrain _brain;
    private readonly EconomyManager _economy;
    private readonly ProductionManager _production;
    private readonly ArmyManager _army;
    private long _thinks;
    public int Player { get; }
    public AiDifficulty Difficulty { get; }
    public AiPlayer(AiSeat seat, int capacity)
    {
        if ((uint)seat.Player >= 4) throw new ArgumentOutOfRangeException(nameof(seat));
        Player = seat.Player; Difficulty = seat.Difficulty; _profile = AiProfile.For(seat.Difficulty);
        _brain = new StrategyBrain(_profile); _economy = new EconomyManager(_profile); _production = new ProductionManager(_profile); _army = new ArmyManager(_profile, capacity);
    }
    /// <summary>Players think on staggered ticks so several AIs never spike the same tick.</summary>
    public bool ShouldThink(long tick) => (tick + Player * 3) % _profile.ThinkInterval == 0;
    public ReadOnlySpan<Command> Think(VisibilityFilter view, MapData map, long tick)
    {
        if (view.Player != Player) throw new ArgumentException("AI must observe through its own filter.");
        _context.Build(view, map, tick);
        if (view.Surrendered || (_context.Buildings.Count == 0 && _context.Workers.Count == 0 && _context.Army.Count == 0)) return ReadOnlySpan<Command>.Empty;
        _thinks++;
        _brain.Update(_context);
        _economy.Run(_context);
        _production.Run(_context, _brain, _economy);
        _army.Run(_context, _brain);
        return System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_context.Output);
    }
    public void Hash(ref WorldHasher h)
    {
        h.AddInt32(Player); h.AddInt32((int)Difficulty); h.AddInt64(_thinks);
        _brain.Hash(ref h); _economy.Hash(ref h); _production.Hash(ref h); _army.Hash(ref h);
    }
}
