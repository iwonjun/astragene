using System;
using RtsGame.Sim.Core;
using RtsGame.Sim.Data;

namespace RtsGame.Sim.Ai;

/// <summary>
/// Top of the AI hierarchy: remembers what scouting revealed, measures threats against its base, chooses the
/// army composition and decides when the army attacks. Managers below only execute these decisions.
/// </summary>
internal sealed class StrategyBrain
{
    private readonly AiProfile _profile;
    private readonly int[] _seenArmor = new int[3];
    internal int ThreatX, ThreatY, ThreatTicks, TargetX, TargetY, Wave;
    internal bool Attacking, KnowsEnemyBase;
    internal long LastThreatTick = -10000;
    internal StrategyBrain(AiProfile profile) { _profile = profile; }
    internal bool Defending => ThreatTicks >= _profile.ReactionTicks;

    internal void Update(AiContext c)
    {
        // Scouting memory: the largest enemy army seen per armor type drives Hard counter-production.
        Span<int> armor = stackalloc int[3];
        foreach (var e in c.Enemies) { var d = DefDatabase.Units[e.Type.Definition]; if (!d.Worker) armor[(int)d.Defense] += d.Supply; }
        for (int a = 0; a < 3; a++) _seenArmor[a] = Math.Max(_seenArmor[a], armor[a]);

        // Threat: nearest visible enemy to any of our buildings within 16 tiles. Easy reacts late, Hard at once.
        int best = int.MaxValue, tx = 0, ty = 0;
        foreach (var e in c.Enemies)
        {
            int ex = AiContext.X(e), ey = AiContext.Y(e);
            foreach (var b in c.Buildings)
            {
                int d = AiContext.Distance2(ex, ey, AiContext.X(b) + 1, AiContext.Y(b) + 1);
                if (d < best) { best = d; tx = ex; ty = ey; }
            }
        }
        if (best <= 16 * 16) { ThreatTicks += _profile.ThinkInterval; ThreatX = tx; ThreatY = ty; LastThreatTick = c.Tick; }
        else if (c.Tick - LastThreatTick > 200) ThreatTicks = 0;

        // Attack target: nearest known enemy structure (visible or remembered ghost), else the mirrored start.
        best = int.MaxValue; TargetX = c.EnemyX; TargetY = c.EnemyY; KnowsEnemyBase = false;
        foreach (var b in c.EnemyBuildings)
        {
            int bx = AiContext.X(b) + 1, by = AiContext.Y(b) + 1, d = AiContext.Distance2(bx, by, c.HomeX, c.HomeY);
            if (d < best) { best = d; TargetX = bx; TargetY = by; KnowsEnemyBase = true; }
        }

        int threshold = _profile.AttackSupply + Math.Min(Wave, 4) * 4;
        if (!Attacking && (c.ArmySupply >= threshold || c.Resources.UsedSupply >= 180)) { Attacking = true; Wave++; }
        else if (Attacking && c.ArmySupply * 3 < _profile.AttackSupply) Attacking = false;
    }

    /// <summary>Relative production weight per unit definition for this faction.</summary>
    internal int Weight(Faction faction, int unit)
    {
        int heavy = _seenArmor[2], light = _seenArmor[0], medium = _seenArmor[1], total = heavy + light + medium;
        bool counter = _profile.Counter && total >= 4;
        if (faction == Faction.Lumina)
            return unit switch
            {
                1 => counter && light * 2 >= total ? 80 : 55,        // Trooper, Normal: all-rounder, best vs Light
                2 => counter && heavy * 2 >= total ? 75 : 30,        // Lancer, Explosive: vs Heavy
                3 => counter && heavy * 3 >= total ? 20 : 8,         // Howitzer, splash siege
                4 => 4,                                              // Wisp: scouting air
                _ => 0,
            };
        return unit switch
        {
            6 => counter && light * 2 >= total ? 55 : 40,            // Render, Piercing melee: full vs Light
            7 => counter && (heavy + medium) * 2 >= total ? 70 : 40, // Spitter, Normal: never penalised
            8 => counter && light * 2 >= total ? 40 : 20,            // Swarmling pair
            9 => counter && medium * 2 >= total ? 30 : 10,           // Wing air
            _ => 0,
        };
    }

    internal void Hash(ref WorldHasher h)
    {
        for (int a = 0; a < 3; a++) h.AddInt32(_seenArmor[a]);
        h.AddInt32(ThreatX); h.AddInt32(ThreatY); h.AddInt32(ThreatTicks); h.AddInt32(TargetX); h.AddInt32(TargetY); h.AddInt32(Wave);
        h.AddByte(Attacking ? (byte)1 : (byte)0); h.AddInt64(LastThreatTick);
    }
}
