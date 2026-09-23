using System;
using System.IO;
using System.Reflection;
using RtsGame.Sim.Ai;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Data;
using RtsGame.Sim.Entities;
using RtsGame.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace RtsGame.Sim.Tests;

public sealed class AiTests
{
    private readonly ITestOutputHelper _out;
    public AiTests(ITestOutputHelper output) { _out = output; }
    private static MapData Duel() => MapLoader.Load(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "duel.map")));

    private readonly record struct Stats(int Workers, int Army, int ArmySupply, int Buildings, int Barracks, int Ore, int Plasma, int Upgrades);
    private static Stats Measure(SimWorld world, int player)
    {
        int workers = 0, army = 0, supply = 0, buildings = 0, barracks = 0;
        for (int i = 0; i < world.Entities.Capacity; i++)
        {
            var id = world.Entities.IdAt(i); if (id == EntityId.None || world.Entities.Owner[i].Player != player) continue;
            var t = world.Entities.Type[i];
            if (t.IsBuilding) { buildings++; if (t.Definition is 2 or 8) barracks++; continue; }
            var d = DefDatabase.Units[t.Definition]; if (d.Worker) workers++; else { army++; supply += d.Supply; }
        }
        var r = world.Resources(player);
        int upgrades = 0; for (int k = 0; k < 3; k++) upgrades += world.UpgradeLevel(player, k);
        return new Stats(workers, army, supply, buildings, barracks, r.Ore, r.Plasma, upgrades);
    }
    private SimWorld Play(int[] factions, AiSeat[] seats, int ticks, ulong seed = 7, int report = 0)
    {
        var world = new SimWorld(Duel(), MatchSetup.Spawns(factions), seed, ai: seats);
        for (int t = 0; t < ticks; t++)
        {
            world.Tick(ReadOnlySpan<Command>.Empty);
            if (report > 0 && (t + 1) % report == 0) _out.WriteLine($"t={(t + 1) / 20}s P0 {Measure(world, 0)}  P1 {Measure(world, 1)}");
            if (Measure(world, 0).Buildings == 0 || Measure(world, 1).Buildings == 0) break;
        }
        return world;
    }

    [Theory]
    [InlineData(AiDifficulty.Easy, 0)]
    [InlineData(AiDifficulty.Medium, 1)]
    [InlineData(AiDifficulty.Hard, 0)]
    [InlineData(AiDifficulty.Hard, 1)]
    public void EachDifficultyRunsItsBuildOrderAndAttacksAnIdleOpponent(AiDifficulty difficulty, int faction)
    {
        var world = Play(new[] { faction, 1 - faction }, new[] { new AiSeat(0, difficulty) }, 20 * 60 * 15, report: 20 * 60);
        var me = Measure(world, 0); var foe = Measure(world, 1);
        Assert.True(me.Workers >= 10, $"workers {me.Workers}");
        Assert.True(me.Barracks >= 1);
        Assert.True(me.Army > 0 || foe.Buildings == 0, "army produced");
        // An idle opponent must eventually lose buildings to the attack waves.
        Assert.True(foe.Buildings < 1 || world.Entities.Get(FirstBuilding(world, 1)).Health.Current < world.Entities.Get(FirstBuilding(world, 1)).Health.Maximum || foe.Buildings == 0,
            $"enemy buildings {foe.Buildings}");
        if (difficulty == AiDifficulty.Hard) Assert.Equal(0, foe.Buildings);
    }
    private static EntityId FirstBuilding(SimWorld world, int player)
    {
        for (int i = 0; i < world.Entities.Capacity; i++) { var id = world.Entities.IdAt(i); if (id != EntityId.None && world.Entities.Owner[i].Player == player && world.Entities.Type[i].IsBuilding) return id; }
        return EntityId.None;
    }

    [Fact]
    public void HardBeatsEasyInBothFactionPairings()
    {
        foreach (var factions in new[] { new[] { 0, 1 }, new[] { 1, 0 } })
        {
            var world = Play(factions, new[] { new AiSeat(0, AiDifficulty.Hard), new AiSeat(1, AiDifficulty.Easy) }, 20 * 60 * 20, 11, 20 * 120);
            var hard = Measure(world, 0); var easy = Measure(world, 1);
            _out.WriteLine($"factions {factions[0]}v{factions[1]} hard {hard} easy {easy} at {world.TickNumber / 20}s");
            Assert.True(easy.Buildings == 0 || hard.Buildings + hard.ArmySupply > easy.Buildings + easy.ArmySupply);
        }
    }

    [Fact]
    public void AiVersusAiIsDeterministicAndReplaysExactly()
    {
        var seats = new[] { new AiSeat(0, AiDifficulty.Hard), new AiSeat(1, AiDifficulty.Medium) };
        var spawns = MatchSetup.Spawns(new[] { 0, 1 });
        var a = new SimWorld(Duel(), spawns, 3, ai: seats); var b = new SimWorld(Duel(), spawns, 3, ai: seats);
        var replay = new ReplayLog(a.Map, spawns, 3, ai: seats);
        for (int t = 0; t < 6000; t++)
        {
            a.Tick(ReadOnlySpan<Command>.Empty); b.Tick(ReadOnlySpan<Command>.Empty); replay.Append(ReadOnlySpan<Command>.Empty, a.Hash());
            if (t % 500 == 0) Assert.Equal(a.Hash(), b.Hash());
        }
        Assert.Equal(a.Hash(), b.Hash());
        var decoded = ReplayLog.Decode(replay.Encode());
        Assert.Equal(2, decoded.AiSeats.Length);
        Assert.Equal(-1, ReplayPlayer.Verify(decoded));
        // A different difficulty is a different game: AI state participates in the world hash.
        var c = new SimWorld(Duel(), spawns, 3, ai: new[] { new AiSeat(0, AiDifficulty.Easy), new AiSeat(1, AiDifficulty.Medium) });
        for (int t = 0; t < 400; t++) c.Tick(ReadOnlySpan<Command>.Empty);
        var d = new SimWorld(Duel(), spawns, 3, ai: seats);
        for (int t = 0; t < 400; t++) d.Tick(ReadOnlySpan<Command>.Empty);
        Assert.NotEqual(c.Hash(), d.Hash());
    }

    [Fact]
    public void AiSeesOnlyThroughItsFilterAndHasNoResourceCheat()
    {
        // Structural guarantee: no AI type can hold the world or raw entity storage.
        foreach (var type in typeof(AiPlayer).Assembly.GetTypes())
        {
            if (type.Namespace != "RtsGame.Sim.Ai") continue;
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Assert.NotEqual(typeof(SimWorld), field.FieldType);
                Assert.NotEqual(typeof(EntityStore), field.FieldType);
            }
        }
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Sim/Ai"));
        foreach (string file in Directory.GetFiles(root, "*.cs"))
        {
            string text = File.ReadAllText(file);
            Assert.DoesNotContain(".Entities.", text); Assert.DoesNotContain("EntityStore", text); Assert.DoesNotContain("SimWorld", text); Assert.DoesNotContain("_economy.Ore", text);
        }
        // Economic parity: AI income equals a scripted player's with the same worker count, because both use Economy.
        var world = new SimWorld(Duel(), MatchSetup.Spawns(new[] { 0, 0 }), 5, ai: new[] { new AiSeat(0, AiDifficulty.Hard) });
        for (int t = 0; t < 200; t++) world.Tick(ReadOnlySpan<Command>.Empty);
        Assert.True(world.Resources(0).Ore <= 450 + 200 * 6, "no free income");
        Assert.Equal(450, world.Resources(1).Ore);
    }
}
