using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;
using RtsGame.Sim.Pathing;
using RtsGame.Sim.World;

namespace RtsGame.Sim.Systems;

internal sealed class MovementSystem
{
    private readonly Grid _grid;
    private readonly FlowFieldCache _cache;
    private readonly LocalAvoidance _avoidance;
    internal int PathBuildCount => _cache.BuildCount;
    internal MovementSystem(Grid grid, int capacity) { _grid=grid; _cache=new FlowFieldCache(grid); _avoidance=new LocalAvoidance(capacity); }
    internal void Tick(EntityStore store)
    {
        for (int i=0;i<store.Capacity;i++)
        {
            if (store.IdAt(i)==EntityId.None || store.Type[i].IsBuilding || !store.Movement[i].Active) continue;
            ref MovementComponent m = ref store.Movement[i];
            Fix2 position=store.Transform[i].Position;
            Fix2 target=m.Destination;
            int gx=target.X.FloorToInt(), gy=target.Y.FloorToInt();
            if (!LocalAvoidance.CanOccupy(_grid,target,m.Airborne)) { m.Active=false; continue; }
            if (!m.Airborne)
            {
                if (m.StalledTicks>=60) { _cache.Invalidate(gx,gy); m.StalledTicks=0; }
                int next=_cache.Get(gx,gy).NextCell(position.X.FloorToInt(),position.Y.FloorToInt());
                if (next<0) { m.StalledTicks++; continue; }
                if (next!=gy*Grid.Size+gx) target=new Fix2(Fix64.FromRatio((next%Grid.Size)*2+1,2),Fix64.FromRatio((next/Grid.Size)*2+1,2));
            }
            Fix2 delta=target-position;
            Fix64 distance=delta.Length;
            Fix64 step=m.Speed/Fix64.FromInt(SimClock.TicksPerSecond);
            Fix2 nextPosition=distance<=step ? target : position+delta/distance*step;
            if (LocalAvoidance.CanOccupy(_grid,nextPosition,m.Airborne)) store.Transform[i].Position=nextPosition;
            Fix64 remaining=(m.Destination-store.Transform[i].Position).LengthSquared;
            if (remaining < m.LastDistanceSquared) m.StalledTicks=0; else m.StalledTicks++;
            m.LastDistanceSquared=remaining;
            if (remaining<=m.Radius*m.Radius || remaining==Fix64.Zero) m.Active=false;
        }
        _avoidance.Resolve(store,_grid);
    }
    internal bool Move(EntityStore store, EntityId id, Fix2 target)
    {
        if (!store.IsAlive(id) || !LocalAvoidance.CanOccupy(_grid,target,store.Movement[id.Index].Airborne)) return false;
        ref var m=ref store.Movement[id.Index];
        m.Destination=target; m.Active=true; m.StalledTicks=0;
        m.LastDistanceSquared=(target-store.Transform[id.Index].Position).LengthSquared;
        return true;
    }
}
