using System;
using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;
using RtsGame.Sim.World;

namespace RtsGame.Sim.Pathing;

internal sealed class LocalAvoidance
{
    private readonly int[] _active;
    internal LocalAvoidance(int capacity) { _active=new int[capacity]; }
    internal void Resolve(EntityStore store, Grid grid)
    {
        int count=0;
        for(int i=0;i<store.Capacity;i++)
            if(store.IdAt(i)!=EntityId.None && !store.Type[i].IsBuilding)_active[count++]=i;
        // Dense IDs avoid per-entity candidate sorting. Reject on each axis before wide arithmetic.
        for(int iteration=0;iteration<2;iteration++)
        for(int a=0;a<count;a++)
        for(int b=a+1;b<count;b++)
        {
            int i=_active[a],j=_active[b];
            if(store.Movement[i].Airborne!=store.Movement[j].Airborne)continue;
            long radius=store.Movement[i].Radius.Raw+store.Movement[j].Radius.Raw;
            if(radius<=0)continue;
            long dx=store.Transform[j].Position.X.Raw-store.Transform[i].Position.X.Raw;
            if(dx>=radius || dx<=-radius)continue;
            long dy=store.Transform[j].Position.Y.Raw-store.Transform[i].Position.Y.Raw;
            if(dy>=radius || dy<=-radius)continue;
            UInt128 squared=(UInt128)((Int128)dx*dx+(Int128)dy*dy);
            if(squared>=(UInt128)((Int128)radius*radius))continue;
            long distance=(long)FixMath.IntegerSqrt(squared);
            long overlap=(radius-distance)/2;
            long px=distance==0?overlap:(long)((Int128)dx*overlap/distance);
            long py=distance==0?0:(long)((Int128)dy*overlap/distance);
            Fix2 push=new(Fix64.FromRaw(px),Fix64.FromRaw(py));
            Fix2 first=store.Transform[i].Position-push,second=store.Transform[j].Position+push;
            if(CanOccupy(grid,first,store.Movement[i].Airborne))store.Transform[i].Position=first;
            if(CanOccupy(grid,second,store.Movement[j].Airborne))store.Transform[j].Position=second;
        }
    }
    internal static bool CanOccupy(Grid grid,Fix2 position,bool airborne)
    {
        int x=position.X.FloorToInt(),y=position.Y.FloorToInt();
        return Grid.Contains(x,y)&&(airborne||grid[x,y].Walkable);
    }
}
