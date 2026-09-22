using System;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;
using RtsGame.Sim.Systems;
using RtsGame.Sim.World;
using RtsGame.Sim.Data;
using Xunit;

namespace RtsGame.Sim.Tests;

public sealed class CombatTests
{
    private static MapData Map(){var tiles=new Tile[128*128];Array.Fill(tiles,new Tile(true,true,0));return new MapData(new Grid(tiles));}
    private static Fix2 Pos(int x,int y)=>new(Fix64.FromInt(x),Fix64.FromInt(y));
    [Theory]
    [InlineData(AttackType.Normal,ArmorType.Heavy,100)]
    [InlineData(AttackType.Piercing,ArmorType.Heavy,50)]
    [InlineData(AttackType.Piercing,ArmorType.Medium,75)]
    [InlineData(AttackType.Explosive,ArmorType.Light,50)]
    [InlineData(AttackType.Explosive,ArmorType.Medium,75)]
    [InlineData(AttackType.Explosive,ArmorType.Heavy,100)]
    public void DamageUsesAuthoredTable(AttackType attack,ArmorType defense,int expected)
    {Assert.Equal(Fix64.FromInt(expected-3),CombatSystem.Damage(100,attack,defense,3,false));}
    [Fact]
    public void UphillAndArmorAreDeterministic()
    {
        Assert.Equal(Fix64.FromInt(72),CombatSystem.Damage(100,AttackType.Normal,ArmorType.Light,3,true));
        Assert.Equal(Fix64.One,CombatSystem.Damage(1,AttackType.Explosive,ArmorType.Light,99,true));
    }
    [Fact]
    public void TwoArmiesThreeThousandTicksHaveSameSurvivorsAndHealth()
    {
        var spawns=new SpawnSpec[40];
        for(int i=0;i<20;i++){spawns[i]=new(0,1,false,Pos(45+i%5,45+i/5));spawns[20+i]=new(1,6,false,Pos(53+i%5,45+i/5));}
        var a=new SimWorld(Map(),spawns,capacity:64);var b=new SimWorld(Map(),spawns,capacity:64);
        for(int tick=0;tick<3000;tick++){a.Tick(ReadOnlySpan<Command>.Empty);b.Tick(ReadOnlySpan<Command>.Empty);}
        Assert.Equal(a.Hash(),b.Hash());Assert.InRange(a.Entities.Count,1,39);
        for(int i=0;i<64;i++){var id=a.Entities.IdAt(i);Assert.Equal(id,b.Entities.IdAt(i));if(id!=EntityId.None)Assert.Equal(a.Entities.Get(id).Health,b.Entities.Get(id).Health);}
        bool death=false;while(a.TryDequeueEvent(out var e))if(e.Kind=="Death")death=true;Assert.True(death);
    }
    [Fact]
    public void SiegeWindupAndProjectileSplashDamageAllies()
    {
        var world=new SimWorld(Map(),new[]{new SpawnSpec(0,3,false,Pos(20,20)),new SpawnSpec(1,6,false,Pos(27,20)),new SpawnSpec(0,0,false,Pos(27,21))},capacity:16);
        var siege=world.Entities.IdAt(0);var target=world.Entities.IdAt(1);var ally=world.Entities.IdAt(2);
        var orders=new[]{new Command(CommandType.Attack,0,siege,target,Pos(27,20)),new Command(CommandType.Hold,1,target,EntityId.None,Fix2.Zero),new Command(CommandType.Hold,0,ally,EntityId.None,Fix2.Zero)};
        world.Tick(orders);
        Assert.Equal(Fix64.FromInt(118),world.Entities.Get(target).Health.Current);
        for(int i=0;i<30;i++)world.Tick(ReadOnlySpan<Command>.Empty);
        Assert.True(world.Entities.Get(target).Health.Current<Fix64.FromInt(118));
        Assert.True(world.Entities.Get(ally).Health.Current<Fix64.FromInt(68));
    }
}
