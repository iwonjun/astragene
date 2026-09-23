using System;
using System.IO;
using RtsGame.Sim.Ai;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;
using RtsGame.Sim.World;
using Xunit;

namespace RtsGame.Sim.Tests;

public sealed class OutcomeTests
{
    private static MapData Duel() => MapLoader.Load(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "duel.map")));

    [Fact]
    public void SurrenderEndsTheMatchAndStatsArePublic()
    {
        var world = new SimWorld(Duel(), MatchSetup.Spawns(new[] { 0, 1 }), 2);
        world.Tick(new[] { new Command(CommandType.Move, 0, world.Entities.IdAt(1), EntityId.None, new Fix2(Fix64.FromInt(30), Fix64.FromInt(30))) });
        Assert.False(world.ViewFor(1).MatchOver);
        world.Tick(new[] { new Command(CommandType.Surrender, 1, EntityId.None, EntityId.None, Fix2.Zero) });
        var view = world.ViewFor(1);
        Assert.True(view.MatchOver); Assert.Equal(0, view.Winner); Assert.True(view.StatsOf(1).Defeated);
        Assert.Equal(1, view.StatsOf(0).Commands); Assert.Equal(new[] { 1 }, view.ApmOf(0));
        bool defeatedEvent = false, overEvent = false;
        while (view.TryDequeueEvent(out var e)) { defeatedEvent |= e.Kind == "Defeated" && e.Entity.Index == 1; overEvent |= e.Kind == "MatchOver" && e.Entity.Index == 0; }
        Assert.True(defeatedEvent && overEvent);
    }

    [Fact]
    public void LosingEveryBuildingDefeatsAndStatsAccumulate()
    {
        var world = new SimWorld(Duel(), MatchSetup.Spawns(new[] { 0, 1 }), 3, ai: new[] { new AiSeat(0, AiDifficulty.Hard) });
        for (int t = 0; t < 20 * 60 * 15 && !world.ViewFor(0).MatchOver; t++) world.Tick(ReadOnlySpan<Command>.Empty);
        var view = world.ViewFor(0);
        Assert.True(view.MatchOver); Assert.Equal(0, view.Winner);
        var hard = view.StatsOf(0); var idle = view.StatsOf(1);
        Assert.True(hard.OreGathered > 1000 && hard.UnitsProduced > 10 && hard.BuildingsBuilt >= 4 && hard.Kills >= 1, hard.ToString());
        Assert.True(idle.Defeated && idle.UnitsLost >= 1 && idle.OreGathered == 0, idle.ToString());
        Assert.True(view.ApmOf(0).Length >= 3);
        Assert.True(world.MatchEndTick > 0);
    }

    [Fact]
    public void SinglePlayerSandboxNeverEnds()
    {
        var tiles = new Tile[128 * 128]; Array.Fill(tiles, new Tile(true, true, 0));
        var world = new SimWorld(new MapData(new Grid(tiles)), new[] { new SpawnSpec(0, 1, false, new Fix2(Fix64.FromInt(9), Fix64.FromInt(9))) }, capacity: 8);
        for (int t = 0; t < 20; t++) world.Tick(ReadOnlySpan<Command>.Empty);
        Assert.False(world.ViewFor(0).MatchOver);
    }
}
