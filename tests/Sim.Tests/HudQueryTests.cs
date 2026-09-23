using System;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;
using RtsGame.Sim.World;
using Xunit;
namespace RtsGame.Sim.Tests;
public sealed class HudQueryTests
{
    private static SimWorld World(){var tiles=new Tile[128*128];Array.Fill(tiles,new Tile(true,true,0));return new SimWorld(new MapData(new Grid(tiles)),new[]{new SpawnSpec(0,0,true,Fix2.Zero),new SpawnSpec(1,6,true,new Fix2(Fix64.FromInt(4),Fix64.Zero))},capacity:16);}
    [Fact] public void ProductionDetailsAreCopiesRestrictedToOwner()
    {
        var w=World();var a=w.ViewFor(0);var b=w.ViewFor(1);var id=a.IdAt(0);
        w.Tick(new[]{new Command(CommandType.Train,0,id,EntityId.None,Fix2.Zero,0)});
        Assert.Equal(0,a.ProductionAt(id,0).Definition);Assert.True(a.ProductionAt(id,0).RemainingTicks>0);
        Assert.Equal(-1,b.ProductionAt(id,0).Definition);Assert.Equal(-1,a.ProductionAt(id,4).Definition);Assert.Equal(0,a.UpgradeLevel(0));
    }
    [Fact] public void SurrenderIsSerializedHashedAndBlocksFurtherOrders()
    {
        var a=World();var b=World();var id=a.ViewFor(0).IdAt(0);var c=new Command(CommandType.Surrender,0,EntityId.None,EntityId.None,Fix2.Zero);
        Span<byte> bytes=stackalloc byte[Command.ByteSize];c.Write(bytes);Assert.Equal(c,Command.Read(bytes));
        a.Tick(new[]{c});b.Tick(new[]{c});Assert.True(a.ViewFor(0).Surrendered);Assert.False(a.ViewFor(1).Surrendered);Assert.Equal(a.Hash(),b.Hash());
        int ore=a.ViewFor(0).Resources.Ore;a.Tick(new[]{new Command(CommandType.Train,0,id,EntityId.None,Fix2.Zero,0)});Assert.Equal(ore,a.ViewFor(0).Resources.Ore);
    }
}
