using System.Collections.Generic;
using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;
using RtsGame.Sim.World;

namespace RtsGame.Sim.Pathing;

internal sealed class LocalAvoidance
{
    private readonly SpatialHash _spatial;
    private readonly List<int> _candidates;
    internal LocalAvoidance(int capacity) { _spatial = new SpatialHash(capacity); _candidates = new List<int>(capacity); }
    internal void Resolve(EntityStore store, Grid grid)
    {
        for (int iteration = 0; iteration < 2; iteration++)
        {
            for (int i = 0; i < store.Capacity; i++)
            {
                if (store.IdAt(i) == EntityId.None || store.Type[i].IsBuilding) { _spatial.Remove(i); continue; }
                var p = store.Transform[i].Position;
                _spatial.Update(i,p.X.FloorToInt(),p.Y.FloorToInt());
            }
            for (int i = 0; i < store.Capacity; i++)
            {
                if (store.IdAt(i) == EntityId.None || store.Type[i].IsBuilding) continue;
                Fix2 p = store.Transform[i].Position;
                _spatial.Query(p.X.FloorToInt()-2,p.Y.FloorToInt()-2,p.X.FloorToInt()+2,p.Y.FloorToInt()+2,_candidates);
                foreach (int j in _candidates)
                {
                    if (j <= i || store.Movement[i].Airborne != store.Movement[j].Airborne) continue;
                    Fix64 radius = store.Movement[i].Radius + store.Movement[j].Radius;
                    Fix2 delta = store.Transform[j].Position - store.Transform[i].Position;
                    if (radius <= Fix64.Zero || delta.LengthSquared >= radius * radius) continue;
                    Fix64 distance = delta.Length;
                    Fix2 direction = distance == Fix64.Zero ? new Fix2(Fix64.One,Fix64.Zero) : delta / distance;
                    Fix2 push = direction * ((radius-distance) / Fix64.FromInt(2));
                    Fix2 a = store.Transform[i].Position - push, b = store.Transform[j].Position + push;
                    if (CanOccupy(grid,a,store.Movement[i].Airborne)) store.Transform[i].Position = a;
                    if (CanOccupy(grid,b,store.Movement[j].Airborne)) store.Transform[j].Position = b;
                }
            }
        }
    }
    internal static bool CanOccupy(Grid grid, Fix2 position, bool airborne)
    {
        int x = position.X.FloorToInt(), y = position.Y.FloorToInt();
        return Grid.Contains(x,y) && (airborne || grid[x,y].Walkable);
    }
}
