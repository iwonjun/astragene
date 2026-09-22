using System;
using System.IO;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;
using RtsGame.Sim.World;
using Xunit;

namespace RtsGame.Sim.Tests;

public sealed class CommandTests
{
    private static SimWorld World()
    {
        var tiles=new Tile[128*128];Array.Fill(tiles,new Tile(true,true,0));
        return new SimWorld(new MapData(new Grid(tiles)),new[]{new SpawnSpec(0,1,false,new Fix2(Fix64.FromInt(10),Fix64.FromInt(10)))},capacity:8);
    }
    [Fact]
    public void WireRoundTripAllTypesRejectsCorruption()
    {
        for(int t=0;t<=12;t++)
        {
            var c=new Command((CommandType)t,3,new EntityId(123,42),new EntityId(789,12),new Fix2(Fix64.FromRatio(-3,7),Fix64.FromInt(55)),9,true);
            var bytes=new byte[Command.ByteSize];c.Write(bytes);Assert.Equal(c,Command.Read(bytes));
            Assert.Equal((byte)t,bytes[0]);Assert.Equal(123,bytes[8]);
            bytes[44]=1;Assert.Throws<InvalidDataException>(()=>Command.Read(bytes));
        }
        Assert.Throws<InvalidDataException>(()=>Command.Read(new byte[2]));
    }
    [Fact]
    public void QueueIsBoundedFifoAndCanBeCleared()
    {
        var q=new CommandQueue();
        for(int i=0;i<8;i++)Assert.True(q.Enqueue(new Command(CommandType.Move,0,new EntityId(i,1),EntityId.None,Fix2.Zero)));
        Assert.False(q.Enqueue(default));
        for(int i=0;i<8;i++){Assert.True(q.TryDequeue(out var c));Assert.Equal(i,c.Entity.Index);}
        Assert.False(q.TryDequeue(out _));q.Enqueue(default);q.Clear();Assert.Equal(0,q.Count);
    }
    [Fact]
    public void WorldOnlyAcceptsOwnedCommandsAndStopInterrupts()
    {
        var world=World();var id=world.Entities.IdAt(0);var start=world.Entities.Get(id).Transform.Position;
        var move=new Command(CommandType.Move,0,id,EntityId.None,new Fix2(Fix64.FromInt(20),Fix64.FromInt(10)));
        world.Tick(new[]{move with {Player=1}});Assert.Equal(start,world.Entities.Get(id).Transform.Position);
        world.Tick(new[]{move});Assert.True(world.Entities.Get(id).Transform.Position.X>start.X);
        world.Tick(new[]{move with {Type=CommandType.Stop}});var stopped=world.Entities.Get(id).Transform.Position;
        for(int i=0;i<10;i++)world.Tick(ReadOnlySpan<Command>.Empty);
        Assert.Equal(stopped,world.Entities.Get(id).Transform.Position);Assert.Equal(UnitState.Idle,world.State(id));
    }
    [Fact]
    public void QueuedMovementRunsInOrderAndReplaysExactly()
    {
        var a=World();var b=World();var id=a.Entities.IdAt(0);
        var first=new Command(CommandType.Move,0,id,EntityId.None,new Fix2(Fix64.FromInt(12),Fix64.FromInt(10)));
        var second=first with {Position=new Fix2(Fix64.FromInt(12),Fix64.FromInt(12)),Queued=true};
        a.Tick(new[]{first,second});b.Tick(new[]{first,second});
        for(int i=0;i<100;i++){a.Tick(ReadOnlySpan<Command>.Empty);b.Tick(ReadOnlySpan<Command>.Empty);}
        Assert.Equal(a.Hash(),b.Hash());Assert.Equal(UnitState.Idle,a.State(id));
        Assert.True((a.Entities.Get(id).Transform.Position-second.Position).Length<Fix64.One);
    }
}
