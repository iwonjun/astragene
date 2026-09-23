using System;
using System.Reflection;
using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;
using RtsGame.Sim.Systems;
using RtsGame.Sim.World;
using Xunit;

namespace RtsGame.Sim.Tests;

public sealed class VisionTests
{
    private static Fix2 Pos(int x,int y)=>new(Fix64.FromInt(x),Fix64.FromInt(y));
    private static MapData Map(){var t=new Tile[128*128];Array.Fill(t,new Tile(true,true,0));return new MapData(new Grid(t));}
    [Fact]
    public void StampsUpdateOnlyOnChangesAndExplorationPersists()
    {
        var s=new EntityStore(3);var a=s.Create();var b=s.Create();
        s.Transform[a.Index].Position=Pos(20,20);s.Transform[b.Index].Position=Pos(20,20);s.Vision[a.Index].Radius=5;s.Vision[b.Index].Radius=5;
        var vision=new VisionSystem(3);var map=Map();vision.Update(s,map);int updates=vision.StampUpdates;
        vision.Update(s,map);Assert.Equal(updates,vision.StampUpdates);Assert.Equal(Visibility.Visible,vision.At(0,20,20));
        s.Destroy(a);vision.Update(s,map);Assert.Equal(Visibility.Visible,vision.At(0,20,20));
        s.Transform[b.Index].Position=Pos(60,60);vision.Update(s,map);
        Assert.Equal(Visibility.Explored,vision.At(0,20,20));Assert.Equal(Visibility.Unexplored,vision.At(0,100,100));
    }
    [Fact]
    public void CliffOcclusionCloakAndDetectorAreEnforced()
    {
        var tiles=new Tile[128*128];Array.Fill(tiles,new Tile(true,true,0));tiles[20*128+22]=new Tile(true,true,2);
        var map=new MapData(new Grid(tiles));var s=new EntityStore(2);var a=s.Create();var enemy=s.Create();
        s.Transform[a.Index].Position=Pos(20,20);s.Vision[a.Index].Radius=8;
        s.Transform[enemy.Index].Position=Pos(24,20);s.Owner[enemy.Index].Player=1;
        var vision=new VisionSystem(2);vision.Update(s,map);Assert.False(vision.CanSee(s,0,enemy));
        s.Transform[enemy.Index].Position=Pos(20,22);s.Vision[enemy.Index].Cloaked=true;vision.Update(s,map);Assert.False(vision.CanSee(s,0,enemy));
        s.Vision[a.Index].Detector=true;vision.Update(s,map);Assert.True(vision.CanSee(s,0,enemy));
    }
    [Fact]
    public void HiddenBuildingsUseLastSnapshotAndCurrentStateCannotLeak()
    {
        var world=new SimWorld(Map(),new[]{new SpawnSpec(0,0,false,Pos(20,20)),new SpawnSpec(1,0,true,Pos(23,20))},capacity:8);
        var view=world.ViewFor(0);var friendly=world.Entities.IdAt(0);var enemy=world.Entities.IdAt(1);var old=view.Get(enemy);
        world.Entities.Transform[0].Position=Pos(80,80);
        world.Tick(Array.Empty<RtsGame.Sim.Commands.Command>());
        world.Entities.Health[1].Current=Fix64.One;
        Assert.False(view.TryGet(enemy,out _));Assert.Equal(EntityId.None,view.IdAt(1));
        Assert.True(view.TryGhost(1,out var ghost));Assert.Equal(old.Health.Current,ghost.Health.Current);
        Assert.Throws<ArgumentException>(()=>view.Get(enemy));
        Assert.True(view.IsAlive(friendly));
    }
    [Fact]
    public void PublicWorldApiDoesNotExposeStoreOrUnfilteredEntityState()
    {
        foreach(var property in typeof(SimWorld).GetProperties(BindingFlags.Public|BindingFlags.Instance))Assert.NotEqual(typeof(EntityStore),property.PropertyType);
        Assert.Null(typeof(SimWorld).GetMethod("State",BindingFlags.Public|BindingFlags.Instance));
        Assert.Null(typeof(SimWorld).GetMethod("Resources",BindingFlags.Public|BindingFlags.Instance));
        Assert.Null(typeof(SimWorld).GetMethod("TryDequeueEvent",BindingFlags.Public|BindingFlags.Instance));
        foreach(var method in typeof(EntityStore).GetMethods(BindingFlags.Public|BindingFlags.Instance))Assert.DoesNotContain(method.Name,new[]{"Create","Destroy"});
    }
}
