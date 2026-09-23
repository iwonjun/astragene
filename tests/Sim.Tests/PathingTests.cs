using System;
using System.Diagnostics;
using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;
using RtsGame.Sim.Pathing;
using RtsGame.Sim.Systems;
using RtsGame.Sim.World;
using Xunit;
using Xunit.Abstractions;

namespace RtsGame.Sim.Tests;

public sealed class PathingTests
{
    private readonly ITestOutputHelper _output;
    public PathingTests(ITestOutputHelper output) { _output=output; }
    private static Grid Map(bool maze=false)
    {
        var tiles=new Tile[128*128];
        for(int y=0;y<128;y++) for(int x=0;x<128;x++)
        {
            bool open=x>0&&y>0&&x<127&&y<127;
            if(maze && x>=16 && x<=96 && x%16==0)
                open=(x/16%2==0) ? y<12 : y>115;
            tiles[y*128+x]=new Tile(open,open,0);
        }
        return new Grid(tiles);
    }
    private static Fix2 Pos(int x,int y)=>new(Fix64.FromRatio(x*2+1,2),Fix64.FromRatio(y*2+1,2));
    private static EntityId Unit(EntityStore s,int x,int y,bool air=false)
    {
        var id=s.Create(); s.Transform[id.Index].Position=Pos(x,y);
        s.Movement[id.Index].Speed=Fix64.FromInt(6); s.Movement[id.Index].Airborne=air;
        s.Movement[id.Index].Radius=Fix64.FromRatio(1,4); return id;
    }
    [Fact]
    public void FieldSolvesEveryReachableMazeTileAndDoesNotCutCorners()
    {
        var grid=Map(true); var field=new FlowField(grid,120,120);
        for(int y=1;y<127;y++) for(int x=1;x<127;x++)
        {
            if(!grid[x,y].Walkable) continue;
            int cell=y*128+x, remaining=128*128;
            while(cell!=120*128+120 && remaining-->0)
            {
                int next=field.NextCell(cell%128,cell/128);
                Assert.True(next>=0);
                Assert.True(field.Cost(next%128,next/128)<field.Cost(cell%128,cell/128));
                if(next%128!=cell%128 && next/128!=cell/128)
                { Assert.True(grid[next%128,cell/128].Walkable); Assert.True(grid[cell%128,next/128].Walkable); }
                cell=next;
            }
            Assert.Equal(120*128+120,cell);
        }
    }
    [Fact]
    public void LruEvictsOldestAndInvalidationRebuilds()
    {
        var cache=new FlowFieldCache(Map()); var first=cache.Get(1,1);
        Assert.Same(first,cache.Get(1,1));
        for(int i=2;i<=33;i++) cache.Get(i,1);
        Assert.NotSame(first,cache.Get(1,1));
        int before=cache.BuildCount; cache.Invalidate(1,1); cache.Get(1,1);
        Assert.Equal(before+1,cache.BuildCount);
    }
    [Fact]
    public void MovementReplaysFiveThousandTicksAndMazeUnitsArrive()
    {
        var grid=Map(true); var a=new EntityStore(8); var b=new EntityStore(8);
        var ma=new MovementSystem(grid,8); var mb=new MovementSystem(grid,8);
        for(int i=0;i<8;i++)
        {
            var ia=Unit(a,4+i,4,i==7); var ib=Unit(b,4+i,4,i==7);
            ma.Move(a,ia,Pos(118+i,120)); mb.Move(b,ib,Pos(118+i,120));
        }
        for(int tick=0;tick<5000;tick++) { ma.Tick(a); mb.Tick(b); }
        Assert.Equal(a.Hash(),b.Hash());
        for(int i=0;i<8;i++) Assert.False(a.Movement[i].Active);
        Assert.True(ma.PathBuildCount>=7);
    }
    [Fact]
    public void TwoHundredUnitsStayUnderThreeMillisecondsPerTick()
    {
        var grid=Map(); var s=new EntityStore(200); var movement=new MovementSystem(grid,200);
        for(int i=0;i<200;i++) { var id=Unit(s,5+i%20,5+i/20); movement.Move(s,id,Pos(100,100)); }
        movement.Tick(s); // Includes the initial flow field build outside steady-state budget.
        var watch=Stopwatch.StartNew();
        for(int tick=0;tick<1000;tick++) movement.Tick(s);
        watch.Stop();
        double ms=watch.Elapsed.TotalMilliseconds/1000;
        _output.WriteLine($"200 units: {ms:F4} ms/tick (1000 ticks, Release expected)");
        Assert.True(ms<3,$"Average {ms:F4} ms/tick");
    }
    [Fact]
    public void SeparationAndUnreachableTargetsAreDeterministic()
    {
        var grid=Map(); var s=new EntityStore(2); var movement=new MovementSystem(grid,2);
        var a=Unit(s,10,10); var b=Unit(s,10,10);
        Assert.False(movement.Move(s,a,Pos(0,0)));
        movement.Tick(s);
        Assert.True((s.Transform[a.Index].Position-s.Transform[b.Index].Position).Length>=Fix64.FromRatio(1,2));
    }
    [Fact]
    public void NearbyIndependentGoalsAvoidFieldsButObstaclesStillUseFields()
    {
        var s=new EntityStore(40);var movement=new MovementSystem(Map(),40);
        for(int i=0;i<40;i++){var id=Unit(s,10+i,10);movement.Move(s,id,Pos(10+i,16));}
        movement.Tick(s);Assert.Equal(0,movement.PathBuildCount);
        var tiles=new Tile[128*128];Array.Fill(tiles,new Tile(true,true,0));tiles[11*128+11]=new Tile(false,false,0);
        var blocked=new MovementSystem(new Grid(tiles),1);var single=new EntityStore(1);var a=Unit(single,10,10);
        blocked.Move(single,a,Pos(12,12));blocked.Tick(single);Assert.Equal(1,blocked.PathBuildCount);
        for(int i=0;i<100;i++){blocked.Tick(single);var p=single.Transform[a.Index].Position;Assert.False(p.X.FloorToInt()==11 && p.Y.FloorToInt()==11);}
        Assert.False(single.Movement[a.Index].Active);
    }}
