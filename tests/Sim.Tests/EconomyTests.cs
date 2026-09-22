using System;
using System.Collections.Generic;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;
using RtsGame.Sim.World;
using Xunit;

namespace RtsGame.Sim.Tests;

public sealed class EconomyTests
{
    private static Fix2 Pos(int x,int y)=>new(Fix64.FromInt(x),Fix64.FromInt(y));
    private static SimWorld Create(bool verge=false,int workers=6,int ore=2000)
    {
        var tiles=new Tile[128*128];Array.Fill(tiles,new Tile(true,true,0));
        for(int n=0;n<5;n++)tiles[15*128+10+n]=new Tile(true,false,0,n);
        var spawns=new List<SpawnSpec>{new(0,verge?6:0,true,Pos(10,10))};
        for(int i=0;i<workers;i++)spawns.Add(new(0,verge?5:0,false,Pos(15+i,13)));
        return new SimWorld(new MapData(new Grid(tiles)),spawns.ToArray(),capacity:128,initialOre:ore,initialPlasma:1000);
    }
    private static Command Order(CommandType type,EntityId entity,int definition=-1,Fix2? position=null)=>new(type,0,entity,EntityId.None,position??Fix2.Zero,definition);
    private static void Step(SimWorld world,int ticks){for(int i=0;i<ticks;i++)world.Tick(ReadOnlySpan<Command>.Empty);}
    [Fact]
    public void ScriptedBotBuildsTwelveWorkersBarracksAndTrooper()
    {
        var world=Create(ore:450);var core=world.Entities.IdAt(0);var initial=world.Resources(0);
        var orders=new List<Command>();
        for(int i=1;i<=6;i++)orders.Add(Order(CommandType.Gather,world.Entities.IdAt(i),(i-1)%5));
        world.Tick(orders.ToArray());
        for(int i=0;i<6;i++){world.Tick(new[]{Order(CommandType.Train,core,0)});Step(world,240);}
        int workers=0;for(int i=0;i<world.Entities.Capacity;i++){var id=world.Entities.IdAt(i);if(id!=EntityId.None&&!world.Entities.Get(id).Type.IsBuilding)workers++;}
        Assert.Equal(12,workers);
        Assert.True(world.Resources(0).Ore>initial.Ore-6*55);
        var builder=world.Entities.IdAt(1);
        world.Tick(new[]{Order(CommandType.Build,builder,2,Pos(17,8))});Step(world,700);
        EntityId barracks=EntityId.None;
        for(int i=0;i<world.Entities.Capacity;i++){var id=world.Entities.IdAt(i);if(id!=EntityId.None&&world.Entities.Get(id).Type.IsBuilding&&world.Entities.Get(id).Type.Definition==2)barracks=id;}
        Assert.NotEqual(EntityId.None,barracks);
        world.Tick(new[]{Order(CommandType.Build,builder,1,Pos(17,5))});Step(world,440);
        world.Tick(new[]{Order(CommandType.Train,barracks,1)});Step(world,340);
        bool trooper=false;for(int i=0;i<world.Entities.Capacity;i++){var id=world.Entities.IdAt(i);if(id!=EntityId.None&&!world.Entities.Get(id).Type.IsBuilding&&world.Entities.Get(id).Type.Definition==1)trooper=true;}
        Assert.True(trooper);
    }
    [Fact]
    public void QueueFiveAndCancelRefundsAllReservedCosts()
    {
        var world=Create(workers:0);var core=world.Entities.IdAt(0);var before=world.Resources(0);
        for(int i=0;i<6;i++)world.Tick(new[]{Order(CommandType.Train,core,0)});
        Assert.Equal(5,world.Entities.Get(core).Production.QueueCount);
        Assert.Equal(before.Ore-5*55,world.Resources(0).Ore);
        for(int i=0;i<5;i++)world.Tick(new[]{Order(CommandType.Cancel,core)});
        Assert.Equal(before.Ore,world.Resources(0).Ore);Assert.Equal(0,world.Resources(0).UsedSupply);
    }
    [Fact]
    public void LuminaWorkerIsOccupiedWhileVergeGrowsWithoutWorker()
    {
        foreach(bool verge in new[]{false,true})
        {
            var world=Create(verge,1);var worker=world.Entities.IdAt(1);var position=world.Entities.Get(worker).Transform.Position;
            world.Tick(new[]{Order(CommandType.Build,worker,verge?7:1,Pos(18,10))});
            world.Tick(new[]{Order(CommandType.Move,worker,position:Pos(30,13))});Step(world,20);
            if(verge)Assert.NotEqual(position,world.Entities.Get(worker).Transform.Position);else Assert.Equal(position,world.Entities.Get(worker).Transform.Position);
            Assert.Equal(3,world.Entities.Count);
        }
    }
    [Fact]
    public void OnlyTwoWorkersGatherAtOneNodeAndGasNeedsExtractor()
    {
        var tiles=new Tile[128*128];Array.Fill(tiles,new Tile(true,true,0));tiles[20*128+20]=new Tile(true,false,0,0);
        var world=new SimWorld(new MapData(new Grid(tiles)),new[]{new SpawnSpec(0,0,true,Pos(10,10)),new SpawnSpec(0,0,false,Pos(20,20)),new SpawnSpec(0,0,false,Pos(21,20)),new SpawnSpec(0,0,false,Pos(20,21))},capacity:16);
        world.Tick(new[]{Order(CommandType.Gather,world.Entities.IdAt(1),0),Order(CommandType.Gather,world.Entities.IdAt(2),0),Order(CommandType.Gather,world.Entities.IdAt(3),0)});Step(world,39);
        int cargo=0;for(int i=1;i<=3;i++)cargo+=world.Entities.Get(world.Entities.IdAt(i)).Cargo.Ore;
        Assert.Equal(10,cargo);
        tiles[20*128+20]=new Tile(true,false,0,5);
        var gas=new SimWorld(new MapData(new Grid(tiles)),new[]{new SpawnSpec(0,0,true,Pos(10,10)),new SpawnSpec(0,0,false,Pos(20,20))},capacity:16);
        gas.Tick(new[]{Order(CommandType.Gather,gas.Entities.IdAt(1),5)});Step(gas,50);
        Assert.Equal(0,gas.Entities.Get(gas.Entities.IdAt(1)).Cargo.Plasma);
    }
    [Fact]
    public void ResearchHasSequentialLevelsAndAppliesMovementBonus()
    {
        var tiles=new Tile[128*128];Array.Fill(tiles,new Tile(true,true,0));
        var world=new SimWorld(new MapData(new Grid(tiles)),new[]{new SpawnSpec(0,0,true,Pos(10,10)),new SpawnSpec(0,3,true,Pos(20,20)),new SpawnSpec(0,1,false,Pos(30,30))},capacity:16,initialOre:5000,initialPlasma:5000);
        var archive=world.Entities.IdAt(1);var unit=world.Entities.IdAt(2);var speed=world.Entities.Get(unit).Movement.Speed;
        world.Tick(new[]{Order(CommandType.Research,archive,7)});Assert.Equal(0,world.Entities.Get(archive).Production.QueueCount);
        for(int level=0;level<3;level++){world.Tick(new[]{Order(CommandType.Research,archive,6+level)});Step(world,1500);}
        Assert.Equal(speed+Fix64.FromRatio(450,1000),world.Entities.Get(unit).Movement.Speed);
        world.Tick(new[]{Order(CommandType.Research,archive,8)});Assert.Equal(0,world.Entities.Get(archive).Production.QueueCount);
    }}
