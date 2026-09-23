using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using RtsGame.Sim.Ai;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;
using RtsGame.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace RtsGame.Sim.Tests;

public sealed class ProfileTests
{
    private readonly ITestOutputHelper _out;
    public ProfileTests(ITestOutputHelper output) { _out = output; }
    private static MapData Duel() => MapLoader.Load(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "duel.map")));
    private static Fix2 Pos(int x, int y) => new(Fix64.FromInt(x), Fix64.FromInt(y));

    private sealed class StopwatchProfiler : ITickProfiler
    {
        public readonly long[] Total = new long[TickSection.Count];
        private readonly long[] _start = new long[TickSection.Count];
        public bool Enabled = true;
        public void Begin(int section) { if (Enabled) _start[section] = Stopwatch.GetTimestamp(); }
        public void End(int section) { if (Enabled) Total[section] += Stopwatch.GetTimestamp() - _start[section]; }
    }

    /// <summary>400 fighting units and 100 buildings: the Phase 13 profiling scenario from the performance target.</summary>
    internal static SimWorld Battle()
    {
        var spawns = new List<SpawnSpec>();
        for (int p = 0; p < 2; p++)
        {
            for (int b = 0; b < 50; b++) spawns.Add(new SpawnSpec(p, p == 0 ? 1 : 7, true, p == 0 ? Pos(8 + b % 10 * 2, 8 + b / 10 * 3) : Pos(100 + b % 10 * 2, 104 + b / 10 * 3)));
            int[] roster = p == 0 ? new[] { 1, 1, 2, 3, 4 } : new[] { 6, 7, 8, 8, 9 };
            for (int u = 0; u < 200; u++) spawns.Add(new SpawnSpec(p, roster[u % 5], false, p == 0 ? Pos(34 + u % 20, 50 + u / 20) : Pos(74 + u % 20, 68 + u / 20)));
        }
        return new SimWorld(Duel(), spawns.ToArray(), 21);
    }
    internal static void Engage(SimWorld world)
    {
        var orders = new List<Command>();
        for (int i = 0; i < world.Entities.Capacity; i++)
        {
            var id = world.Entities.IdAt(i); if (id == EntityId.None || world.Entities.Type[i].IsBuilding) continue;
            int p = world.Entities.Owner[i].Player;
            orders.Add(new Command(CommandType.AttackMove, p, id, EntityId.None, p == 0 ? Pos(80, 80) : Pos(50, 55)));
        }
        world.Tick(orders.ToArray());
    }

    /// <summary>JIT-compile every system on a throwaway copy so the measured world starts cold only in data, not code.</summary>
    private static void Warmup() { var w = Battle(); Engage(w); for (int t = 0; t < 120; t++) w.Tick(ReadOnlySpan<Command>.Empty); }

    [Fact]
    public void FourHundredUnitsAndHundredBuildingsStayWithinTickBudget()
    {
        var world = Battle(); Engage(world);
        Warmup();
        var profiler = new StopwatchProfiler(); world.Profiler = profiler;
        const int ticks = 1200;
        long before = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew(); double worst = 0, fullSum = 0; int fullTicks = 0;
        for (int t = 0; t < ticks; t++)
        {
            int count = world.Entities.Count;
            profiler.Enabled = count >= 450; // rank systems at full load, where the budget matters
            long s = Stopwatch.GetTimestamp();
            world.Tick(ReadOnlySpan<Command>.Empty);
            double ms = (Stopwatch.GetTimestamp() - s) * 1000.0 / Stopwatch.Frequency;
            worst = Math.Max(worst, ms);
            if (count >= 450) { fullSum += ms; fullTicks++; }
            if (t % 200 == 0) _out.WriteLine($"tick {t}: entities {count}, {ms:0.000} ms");
        }
        sw.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        double mean = sw.Elapsed.TotalMilliseconds / ticks, fullMean = fullTicks == 0 ? 0 : fullSum / fullTicks;
        var order = new int[TickSection.Count]; for (int i = 0; i < order.Length; i++) order[i] = i;
        Array.Sort(order, (a, b) => profiler.Total[b].CompareTo(profiler.Total[a]));
        _out.WriteLine($"mean={mean:0.000}ms  mean@>=450 entities={fullMean:0.000}ms over {fullTicks} ticks  worst={worst:0.000}ms alloc/tick={allocated / ticks}B");
        for (int r = 0; r < 5; r++) _out.WriteLine($"{r + 1}. {TickSection.Names[order[r]],-14} {profiler.Total[order[r]] * 1000.0 / Stopwatch.Frequency / Math.Max(1, fullTicks):0.000} ms/tick at full load");
        Assert.True(fullTicks >= 40, "the scenario must start at ~500 entities");
        Assert.True(fullMean <= 5.0, $"mean tick {fullMean:0.000} ms at full load exceeds the 5 ms budget");
        Assert.True(mean <= 5.0, $"mean tick {mean:0.000} ms exceeds the 5 ms budget");
    }

    [Fact]
    public void LongRunWithFiveHundredEntitiesIsStable()
    {
        // 20 minutes of AI vs AI plus the battle roster: memory must not grow once warmed up.
        var world = new SimWorld(Duel(), MatchSetup.Spawns(new[] { 0, 1 }), 4, ai: new[] { new AiSeat(0, AiDifficulty.Hard), new AiSeat(1, AiDifficulty.Hard) });
        for (int t = 0; t < 2000; t++) world.Tick(ReadOnlySpan<Command>.Empty);
        GC.Collect(); long warm = GC.GetTotalMemory(true);
        for (int t = 0; t < 20 * 60 * 20 - 2000; t++) world.Tick(ReadOnlySpan<Command>.Empty);
        GC.Collect(); long end = GC.GetTotalMemory(true);
        _out.WriteLine($"entities={world.Entities.Count} warm={warm / 1024}KB end={end / 1024}KB over={world.ViewFor(0).MatchOver} winner={world.ViewFor(0).Winner}");
        Assert.True(end - warm < 8 * 1024 * 1024, "memory grew during the long run");
    }
}
