using System;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.Data;
using RtsGame.Sim.Entities;

namespace RtsGame.Sim.Ai;

/// <summary>
/// Moves the army as a group (rally, defend, attack waves) and runs per-unit micro below it:
/// focus fire on the weakest enemy in reach and, on Hard, pulling badly damaged units out of the fight.
/// </summary>
internal sealed class ArmyManager
{
    private readonly AiProfile _profile;
    private readonly long[] _retreatUntil;
    private readonly uint[] _retreatGeneration;
    private bool _scouted;
    internal ArmyManager(AiProfile profile, int capacity) { _profile = profile; _retreatUntil = new long[capacity]; _retreatGeneration = new uint[capacity]; }

    internal void Run(AiContext c, StrategyBrain brain)
    {
        int towardX = Math.Sign(64 - c.HomeX), towardY = Math.Sign(64 - c.HomeY);
        int rallyX = c.HomeX + towardX * 7, rallyY = c.HomeY + towardY * 7;
        var rally = AiContext.Tile(rallyX, rallyY);

        if (_profile.Scout && !_scouted && c.Tick >= 600 && c.Workers.Count > 6)
        {
            // Hard sends one worker through the enemy start and back home to learn the build early.
            var scout = c.Workers[c.Workers.Count - 1];
            c.Issue(CommandType.Move, scout.Id, EntityId.None, AiContext.Tile(c.EnemyX, c.EnemyY));
            c.Issue(CommandType.Move, scout.Id, EntityId.None, AiContext.Tile(c.HomeX, c.HomeY), queued: true);
            _scouted = true;
        }

        bool defend = brain.Defending;
        var goal = defend ? AiContext.Tile(brain.ThreatX, brain.ThreatY) : brain.Attacking ? AiContext.Tile(brain.TargetX, brain.TargetY) : rally;
        foreach (var u in c.Army)
        {
            int i = u.Id.Index;
            if (_retreatGeneration[i] == u.Id.Generation && _retreatUntil[i] > c.Tick) continue;
            if (_profile.Retreat && Retreat(c, u, rally)) continue;
            if (_profile.FocusFire && FocusFire(c, u)) continue;
            var state = c.State(u);
            if (!defend && !brain.Attacking)
            {
                if (state == UnitState.Idle && AiContext.Distance2(AiContext.X(u), AiContext.Y(u), rallyX, rallyY) > 36) c.Issue(CommandType.Move, u.Id, EntityId.None, rally);
                continue;
            }
            // Re-issue only when the unit is idle or heading somewhere else, to keep the command stream small.
            bool heading = u.Movement.Active && (u.Movement.Destination - goal).LengthSquared <= Fix64.FromInt(16);
            if (state == UnitState.Idle || (!heading && state != UnitState.Attacking)) c.Issue(CommandType.AttackMove, u.Id, EntityId.None, goal);
        }
        // Workers help only against an attack on the base itself, and only while the army is absent.
        if (defend && c.ArmySupply == 0 && _profile.FocusFire)
            foreach (var w in c.Workers)
                if (AiContext.Distance2(AiContext.X(w), AiContext.Y(w), brain.ThreatX, brain.ThreatY) <= 64) c.Issue(CommandType.AttackMove, w.Id, EntityId.None, AiContext.Tile(brain.ThreatX, brain.ThreatY));
    }

    private bool Retreat(AiContext c, in EntitySnapshot u, Fix2 rally)
    {
        if (u.Health.Current * Fix64.FromInt(100) >= u.Health.Maximum * Fix64.FromInt(35)) return false;
        int ux = AiContext.X(u), uy = AiContext.Y(u);
        bool pressed = false;
        foreach (var e in c.Enemies) if (!DefDatabase.Units[e.Type.Definition].Worker && AiContext.Distance2(ux, uy, AiContext.X(e), AiContext.Y(e)) <= 64) { pressed = true; break; }
        if (!pressed) return false;
        c.Issue(CommandType.Move, u.Id, EntityId.None, rally);
        _retreatUntil[u.Id.Index] = c.Tick + 60; _retreatGeneration[u.Id.Index] = u.Id.Generation;
        return true;
    }

    private static bool FocusFire(AiContext c, in EntitySnapshot u)
    {
        var d = DefDatabase.Units[u.Type.Definition];
        int ux = AiContext.X(u), uy = AiContext.Y(u), reach = (d.Range + 2) * (d.Range + 2);
        EntityId best = EntityId.None; long bestHealth = long.MaxValue;
        foreach (var e in c.Enemies)
        {
            if (AiContext.Distance2(ux, uy, AiContext.X(e), AiContext.Y(e)) > reach) continue;
            long hp = e.Health.Current.Raw;
            if (hp < bestHealth) { bestHealth = hp; best = e.Id; }
        }
        if (best == EntityId.None) return false;
        if (u.Combat.Target != best) c.Issue(CommandType.Attack, u.Id, best, Fix2.Zero);
        return true;
    }

    internal void Hash(ref WorldHasher h)
    {
        h.AddByte(_scouted ? (byte)1 : (byte)0);
        for (int i = 0; i < _retreatUntil.Length; i++) if (_retreatUntil[i] != 0) { h.AddInt32(i); h.AddInt64(_retreatUntil[i]); h.AddUInt64(_retreatGeneration[i]); }
    }
}
