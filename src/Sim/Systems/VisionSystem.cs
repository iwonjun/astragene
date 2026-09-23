using System;
using System.Collections.Generic;
using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;
using RtsGame.Sim.World;

namespace RtsGame.Sim.Systems;

public enum Visibility { Unexplored, Explored, Visible }
internal sealed class VisionSystem
{
    private readonly int[,] _counts=new int[4,128*128],_detectors=new int[4,128*128];
    private readonly bool[,] _explored=new bool[4,128*128];
    private readonly int[] _x,_y,_owner,_radius;
    private readonly uint[] _generation;
    private readonly bool[] _detector;
    private readonly List<int>[] _stamps;
    private static readonly int[][] Circles=MakeCircles();
    private readonly EntitySnapshot[,] _lastBuildings;
    private readonly bool[,] _knownBuildings;
    internal int StampUpdates {get;private set;}
    internal VisionSystem(int capacity)
    {
        _x=new int[capacity];_y=new int[capacity];_owner=new int[capacity];Array.Fill(_owner,-1);_radius=new int[capacity];_generation=new uint[capacity];_detector=new bool[capacity];
        _stamps=new List<int>[capacity];for(int i=0;i<capacity;i++)_stamps[i]=new List<int>(512);
        _lastBuildings=new EntitySnapshot[4,capacity];_knownBuildings=new bool[4,capacity];
    }
    private static int[][] MakeCircles()
    {
        var circles=new int[33][];
        for(int r=0;r<=32;r++){var offsets=new List<int>();for(int y=-r;y<=r;y++)for(int x=-r;x<=r;x++)if(x*x+y*y<=r*r){offsets.Add(x);offsets.Add(y);}circles[r]=offsets.ToArray();}
        return circles;
    }
    internal Visibility At(int player,int x,int y)
    {
        if((uint)player>=4||!Grid.Contains(x,y))return Visibility.Unexplored;
        int index=y*128+x;return _counts[player,index]>0?Visibility.Visible:_explored[player,index]?Visibility.Explored:Visibility.Unexplored;
    }
    internal bool CanSee(EntityStore store,int player,EntityId id)
    {
        if(!store.IsAlive(id))return false;
        if(store.Owner[id.Index].Player==player)return true;
        var position=store.Transform[id.Index].Position;int x=position.X.FloorToInt(),y=position.Y.FloorToInt();
        return At(player,x,y)==Visibility.Visible&&(!store.Vision[id.Index].Cloaked||_detectors[player,y*128+x]>0);
    }
    internal void Update(EntityStore store,MapData map)
    {
        for(int i=0;i<store.Capacity;i++)
        {
            var id=store.IdAt(i);bool alive=id!=EntityId.None;
            int player=alive?store.Owner[i].Player:-1;
            int x=alive?store.Transform[i].Position.X.FloorToInt():-1,y=alive?store.Transform[i].Position.Y.FloorToInt():-1;
            int radius=alive?store.Vision[i].Radius:0;bool detector=alive&&store.Vision[i].Detector;
            if(radius<0||radius>32)throw new InvalidOperationException("Vision radius must be within 0..32.");
            if(_owner[i]==player&&_x[i]==x&&_y[i]==y&&_generation[i]==id.Generation&&_radius[i]==radius&&_detector[i]==detector)continue;
            if(_owner[i]>=0)foreach(int tile in _stamps[i]){_counts[_owner[i],tile]--;if(_detector[i])_detectors[_owner[i],tile]--;}
            _stamps[i].Clear();_owner[i]=player;_x[i]=x;_y[i]=y;_generation[i]=id.Generation;_radius[i]=radius;_detector[i]=detector;
            if(!alive||player<0||player>=4)continue;
            StampUpdates++;
            int[] circle=Circles[radius];
            for(int n=0;n<circle.Length;n+=2)
            {
                int tx=x+circle[n],ty=y+circle[n+1];if(!Grid.Contains(tx,ty)||!LineVisible(map.Grid,x,y,tx,ty))continue;
                int tile=ty*128+tx;_counts[player,tile]++;_explored[player,tile]=true;if(detector)_detectors[player,tile]++;_stamps[i].Add(tile);
            }
        }
        for(int p=0;p<4;p++)for(int i=0;i<store.Capacity;i++)
        {
            var id=store.IdAt(i);
            if(id!=EntityId.None&&store.Type[i].IsBuilding&&CanSee(store,p,id)){_lastBuildings[p,i]=store.Get(id);_knownBuildings[p,i]=true;}
            else if(_knownBuildings[p,i])
            {
                var old=_lastBuildings[p,i];
                if(At(p,old.Transform.Position.X.FloorToInt(),old.Transform.Position.Y.FloorToInt())==Visibility.Visible)_knownBuildings[p,i]=false;
            }
        }
    }
    private static bool LineVisible(Grid grid,int x,int y,int tx,int ty)
    {
        int height=grid[x,y].Height,dx=Math.Abs(tx-x),dy=-Math.Abs(ty-y),sx=x<tx?1:-1,sy=y<ty?1:-1,error=dx+dy;
        while(true)
        {
            if(grid[x,y].Height>height)return false;
            if(x==tx&&y==ty)return true;
            int twice=error*2;if(twice>=dy){error+=dy;x+=sx;}if(twice<=dx){error+=dx;y+=sy;}
        }
    }
    internal bool TryGhost(int player,int index,out EntitySnapshot snapshot)
    {snapshot=_lastBuildings[player,index];return _knownBuildings[player,index]&&At(player,snapshot.Transform.Position.X.FloorToInt(),snapshot.Transform.Position.Y.FloorToInt())==Visibility.Explored;}
    internal void Hash(ref WorldHasher h)
    {
        for(int p=0;p<4;p++)
        {
            for(int i=0;i<128*128;i++){h.AddInt32(_counts[p,i]);h.AddInt32(_detectors[p,i]);h.AddByte(_explored[p,i]?(byte)1:(byte)0);}
            for(int i=0;i<_stamps.Length;i++)
            {h.AddByte(_knownBuildings[p,i]?(byte)1:(byte)0);if(!_knownBuildings[p,i])continue;var s=_lastBuildings[p,i];h.AddInt32(s.Id.Index);h.AddUInt64(s.Id.Generation);h.AddFix2(s.Transform.Position);h.AddFix(s.Health.Current);h.AddInt32(s.Type.Definition);h.AddInt32(s.Owner.Player);}
        }
    }
}
