using System;
using System.Collections.Generic;
using RtsGame.Sim.Core;

namespace RtsGame.Sim.World;

public readonly record struct PlayerStats(int OreGathered, int PlasmaGathered, int UnitsProduced, int BuildingsBuilt, int UnitsLost, int Kills, int Commands, bool Defeated);

/// <summary>
/// Deterministic per-player match counters and the win/lose decision. A player who started with entities is
/// defeated after surrendering, leaving or losing every building; the last player standing wins.
/// </summary>
internal sealed class MatchStats
{
    internal const int TicksPerMinute = 20 * 60;
    internal readonly int[] UnitsProduced = new int[4], BuildingsBuilt = new int[4], UnitsLost = new int[4], Kills = new int[4], Commands = new int[4];
    internal readonly bool[] Participant = new bool[4], Defeated = new bool[4];
    private readonly List<int>[] _apm = { new(), new(), new(), new() };
    internal bool Over { get; private set; }
    internal int Winner { get; private set; } = -1;
    internal long EndTick { get; private set; } = -1;

    internal void CountCommand(int player, long tick)
    {
        Commands[player]++;
        int minute = (int)(tick / TicksPerMinute);
        var list = _apm[player];
        while (list.Count <= minute) list.Add(0);
        list[minute]++;
    }
    internal int[] Apm(int player)
    {
        var list = _apm[player]; var copy = new int[list.Count];
        for (int i = 0; i < copy.Length; i++) copy[i] = list[i];
        return copy;
    }
    /// <summary>Returns the players newly defeated this tick (bit mask) so the world can emit events.</summary>
    internal int Update(long tick, ReadOnlySpan<int> buildings, ReadOnlySpan<bool> surrendered)
    {
        int newly = 0;
        for (int p = 0; p < 4; p++)
            if (Participant[p] && !Defeated[p] && (surrendered[p] || buildings[p] == 0)) { Defeated[p] = true; newly |= 1 << p; }
        if (!Over)
        {
            int participants = 0, alive = 0, last = -1;
            for (int p = 0; p < 4; p++) { if (!Participant[p]) continue; participants++; if (!Defeated[p]) { alive++; last = p; } }
            if (participants >= 2 && alive <= 1) { Over = true; Winner = alive == 1 ? last : -1; EndTick = tick; }
        }
        return newly;
    }
    internal void Hash(ref WorldHasher h)
    {
        for (int p = 0; p < 4; p++)
        {
            h.AddInt32(UnitsProduced[p]); h.AddInt32(BuildingsBuilt[p]); h.AddInt32(UnitsLost[p]); h.AddInt32(Kills[p]); h.AddInt32(Commands[p]);
            h.AddByte(Participant[p] ? (byte)1 : (byte)0); h.AddByte(Defeated[p] ? (byte)1 : (byte)0);
        }
        h.AddByte(Over ? (byte)1 : (byte)0); h.AddInt32(Winner); h.AddInt64(EndTick);
    }
}
