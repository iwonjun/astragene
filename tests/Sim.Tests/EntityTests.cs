using System;
using RtsGame.Sim.Core;
using RtsGame.Sim.Data;
using RtsGame.Sim.Entities;
using Xunit;

namespace RtsGame.Sim.Tests;

public sealed class EntityTests
{
    [Fact]
    public void GenerationsRejectStaleHandlesAndResetComponents()
    {
        var store = new EntityStore(1);
        var first = store.Create();
        store.Health[0].Current = Fix64.FromInt(9);
        Assert.Throws<InvalidOperationException>(() => store.Create());
        Assert.True(store.Destroy(first));
        Assert.False(store.Destroy(first));
        var second = store.Create();
        Assert.Equal(first.Index, second.Index);
        Assert.NotEqual(first.Generation, second.Generation);
        Assert.Throws<ArgumentException>(() => store.Get(first));
        Assert.Equal(Fix64.Zero, store.Get(second).Health.Current);
        Assert.Equal(EntityId.None, store.Get(second).Combat.Target);
        Assert.Equal(1, store.Count);
        var copy = store.Get(second).Health;
        copy.Current = Fix64.FromInt(99);
        Assert.Equal(Fix64.Zero, store.Get(second).Health.Current);
    }
    [Fact]
    public void HundredThousandBatchesOfFiveHundredRemainStable()
    {
        var a = new EntityStore(500); var b = new EntityStore(500);
        var idsA = new EntityId[500]; var idsB = new EntityId[500];
        // Warm JIT before measuring the allocation-free hot loop.
        for (int i = 0; i < 500; i++) { idsA[i] = a.Create(); idsB[i] = b.Create(); }
        for (int i = 0; i < 500; i++) { a.Destroy(idsA[i]); b.Destroy(idsB[i]); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool mismatch = false;
        for (int batch = 0; batch < 100000; batch++)
        {
            for (int i = 0; i < 500; i++)
            {
                idsA[i] = a.Create(); idsB[i] = b.Create();
                a.Transform[idsA[i].Index].Position = new Fix2(Fix64.FromInt(i), Fix64.FromInt(batch));
                b.Transform[idsB[i].Index].Position = new Fix2(Fix64.FromInt(i), Fix64.FromInt(batch));
            }
            if (batch % 1000 == 0 && a.Hash() != b.Hash()) mismatch = true;
            for (int i = 0; i < 500; i++) { a.Destroy(idsA[i]); b.Destroy(idsB[i]); }
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.False(mismatch);
        Assert.Equal(a.Hash(), b.Hash());
        Assert.Equal(0, a.Count);
        Assert.Equal(0L, allocated);
    }
    [Fact]
    public void GeneratedDefinitionsHaveValidIdsAndReferences()
    {
        Assert.Equal(10, DefDatabase.Units.Length);
        Assert.Equal(12, DefDatabase.Buildings.Length);
        Assert.Equal(9, DefDatabase.Upgrades.Length);
        for (int i = 0; i < DefDatabase.Units.Length; i++)
        {
            var unit = DefDatabase.Units[i];
            Assert.Equal(i, unit.Id); Assert.True(unit.Health > 0 && unit.TrainTicks > 0 && unit.SpeedMilli > 0);
            Assert.InRange(unit.Trainer, 0, DefDatabase.Buildings.Length - 1);
            Assert.Equal(unit.Faction, DefDatabase.Buildings[unit.Trainer].Faction);
            if (unit.RequiredTech >= 0) Assert.Equal(unit.Faction, DefDatabase.Buildings[unit.RequiredTech].Faction);
        }
        for (int i = 0; i < DefDatabase.Buildings.Length; i++)
        {
            var building = DefDatabase.Buildings[i];
            Assert.Equal(i, building.Id); Assert.True(building.Health > 0 && building.Width > 0 && building.BuildTicks > 0);
            if (building.RequiredTech >= 0) Assert.Equal(building.Faction, DefDatabase.Buildings[building.RequiredTech].Faction);
        }
    }
}
