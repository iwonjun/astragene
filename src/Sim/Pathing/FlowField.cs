using System;
using System.Collections.Generic;
using RtsGame.Sim.World;

namespace RtsGame.Sim.Pathing;

public sealed class FlowField
{
    private readonly int[] _cost = new int[Grid.Size * Grid.Size];
    private readonly int[] _next = new int[Grid.Size * Grid.Size];
    private static readonly int[] Dx = { 0,1,0,-1,1,1,-1,-1 };
    private static readonly int[] Dy = { -1,0,1,0,-1,1,1,-1 };
    public int TargetX { get; }
    public int TargetY { get; }
    public FlowField(Grid grid, int targetX, int targetY)
    {
        if (!Grid.Contains(targetX,targetY) || !grid[targetX,targetY].Walkable) throw new ArgumentException("Target is not walkable.");
        TargetX = targetX; TargetY = targetY;
        Array.Fill(_cost, int.MaxValue); Array.Fill(_next, -1);
        int goal = targetY * Grid.Size + targetX; _cost[goal] = 0; _next[goal] = goal;
        var queue = new PriorityQueue<int,long>(); queue.Enqueue(goal, goal);
        while (queue.TryDequeue(out int cell, out long priority))
        {
            if (priority / _cost.Length != _cost[cell]) continue;
            int x = cell % Grid.Size, y = cell / Grid.Size;
            for (int d = 0; d < 8; d++)
            {
                int nx = x + Dx[d], ny = y + Dy[d];
                if (!Grid.Contains(nx,ny) || !grid[nx,ny].Walkable) continue;
                if (d >= 4 && (!grid[x,ny].Walkable || !grid[nx,y].Walkable)) continue;
                int neighbor = ny * Grid.Size + nx;
                int cost = _cost[cell] + (d < 4 ? 10 : 14);
                if (cost >= _cost[neighbor]) continue;
                _cost[neighbor] = cost; _next[neighbor] = cell;
                queue.Enqueue(neighbor, (long)cost * _cost.Length + neighbor);
            }
        }
    }
    public int NextCell(int x, int y) => Grid.Contains(x,y) ? _next[y * Grid.Size + x] : -1;
    public int Cost(int x, int y) => Grid.Contains(x,y) ? _cost[y * Grid.Size + x] : int.MaxValue;
}
