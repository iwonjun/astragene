using System;
using System.Collections.Generic;
using System.IO;
using RtsGame.Sim.World;
using Xunit;

namespace RtsGame.Sim.Tests;

public sealed class WorldTests
{
    private static byte[] Bytes() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "duel.map"));
    [Fact]
    public void LoadTwiceAndRoundTripHaveIdenticalHash()
    {
        byte[] bytes = Bytes();
        MapData a = MapLoader.Load(bytes);
        MapData b = MapLoader.Load(bytes);
        Assert.Equal(a.Hash(), b.Hash());
        Assert.Equal(bytes, MapLoader.Save(a));
        Assert.Throws<InvalidDataException>(() => MapLoader.Load(bytes.AsSpan(1)));
        bytes[0] = 0;
        Assert.Throws<InvalidDataException>(() => MapLoader.Load(bytes));
        bytes = Bytes(); bytes[13] = 3;
        Assert.Throws<InvalidDataException>(() => MapLoader.Load(bytes));
        bytes = Bytes(); bytes[12] = 2;
        Assert.Throws<InvalidDataException>(() => MapLoader.Load(bytes));
        Assert.Throws<ArgumentOutOfRangeException>(() => a.Grid[-1,0]);
    }
    [Fact]
    public void GeneratedMapIsSymmetricAndBasesAreConnected()
    {
        var map = MapLoader.Load(Bytes());
        int[] resources = new int[20];
        for (int y = 0; y < Grid.Size; y++)
        for (int x = 0; x < Grid.Size; x++)
        {
            Tile a = map.Grid[x,y]; Tile b = map.Grid[127-x,127-y];
            Assert.Equal(a.Walkable, b.Walkable);
            Assert.Equal(a.Buildable, b.Buildable);
            Assert.Equal(a.Height, b.Height);
            Assert.Equal(a.ResourceNodeId >= 0, b.ResourceNodeId >= 0);
            if (a.ResourceNodeId >= 0) resources[a.ResourceNodeId]++;
        }
        foreach (int count in resources) Assert.Equal(1, count);
        Assert.Equal(2, map.Grid[20,20].Height);
        Assert.Equal(1, map.Grid[38,38].Height);
        Assert.Equal(0, map.Grid[64,64].Height);
        var queue = new Queue<int>();
        var visited = new bool[Grid.Size * Grid.Size];
        queue.Enqueue(20 * 128 + 20); visited[20 * 128 + 20] = true;
        int[] dx = { 1,0,-1,0 }; int[] dy = { 0,1,0,-1 };
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            for (int i = 0; i < 4; i++)
            {
                int x = current % 128 + dx[i], y = current / 128 + dy[i];
                if (!Grid.Contains(x,y) || !map.Grid[x,y].Walkable || visited[y*128+x]) continue;
                visited[y*128+x] = true; queue.Enqueue(y*128+x);
            }
        }
        Assert.True(visited[107*128+107]);
        Assert.True(visited[38*128+38]);
        Assert.True(visited[64*128+64]);
    }
    [Fact]
    public void SpatialQueriesAreSortedAfterMoveRemovalAndDifferentInsertOrders()
    {
        var a = new SpatialHash(20); var b = new SpatialHash(20);
        for (int i = 0; i < 20; i++) a.Update(i, i, i);
        for (int i = 19; i >= 0; i--) b.Update(i, i, i);
        var first = new List<int>(); var second = new List<int>();
        a.Query(0,0,127,127,first); b.Query(0,0,127,127,second);
        Assert.Equal(first, second);
        a.Update(0,100,100); a.Remove(3);
        a.Query(0,0,3,3,first);
        Assert.Equal(new[] {1,2}, first);
        a.Query(99,99,101,101,first); Assert.Equal(new[] {0}, first);
        a.Query(-9,-9,-1,-1,first); Assert.Empty(first);
        Assert.Throws<ArgumentOutOfRangeException>(() => a.Update(20,0,0));
    }
}
