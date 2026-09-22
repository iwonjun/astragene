using System;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.Data;
using RtsGame.Sim.Entities;
using RtsGame.Sim.World;

namespace RtsGame.Sim.Systems;

public readonly record struct PlayerResources(int Ore,int Plasma,int UsedSupply,int MaxSupply);
internal sealed class EconomySystem
{
    internal readonly int[] Ore=new int[4],Plasma=new int[4];
    internal readonly int[,] Upgrades=new int[4,3];
    private readonly int[] _remaining,_nodeX,_nodeY;
    private readonly bool[] _gas;
    private readonly int[] _gatherTimer;
    private readonly EntityId[] _constructionWorker;
    private readonly int[] _constructionLeft;
    private readonly int[] _constructionOre,_constructionPlasma;
    private readonly bool[] _returning;
    private readonly int[] _slots;
    internal static int Rule(int index)=>DefDatabase.Rules[index].Value;
    internal EconomySystem(MapData map,int capacity)
    {
        for(int p=0;p<4;p++){Ore[p]=Rule(0);Plasma[p]=Rule(1);}
        int count=0;for(int y=0;y<128;y++)for(int x=0;x<128;x++)count=Math.Max(count,map.Grid[x,y].ResourceNodeId+1);
        _remaining=new int[count];_nodeX=new int[count];_nodeY=new int[count];_gas=new bool[count];_slots=new int[count];
        for(int y=0;y<128;y++)for(int x=0;x<128;x++)
        {int id=map.Grid[x,y].ResourceNodeId;if(id<0)continue;_nodeX[id]=x;_nodeY[id]=y;_gas[id]=id%10 is 5 or 9;_remaining[id]=Rule(_gas[id]?3:2);}
        _gatherTimer=new int[capacity];_returning=new bool[capacity];_constructionWorker=new EntityId[capacity];Array.Fill(_constructionWorker,EntityId.None);
        _constructionLeft=new int[capacity];_constructionOre=new int[capacity];_constructionPlasma=new int[capacity];
    }
    internal void Reset(int i){_gatherTimer[i]=0;_returning[i]=false;_constructionWorker[i]=EntityId.None;_constructionLeft[i]=0;_constructionOre[i]=0;_constructionPlasma[i]=0;}
    internal int ConstructionLeft(int index)=>_constructionLeft[index];
    internal int UpgradeAmount(int player,int kind)
    {
        int value=0;for(int level=0;level<Upgrades[player,kind];level++)value+=DefDatabase.Upgrades[kind*3+level].Amount;return value;
    }
    internal bool HasTech(EntityStore s,int player,int definition)
    {
        if(definition<0)return true;
        for(int i=0;i<s.Capacity;i++)if(s.IdAt(i)!=EntityId.None&&s.Type[i].IsBuilding&&s.Owner[i].Player==player&&s.Type[i].Definition==definition&&_constructionLeft[i]==0)return true;
        return false;
    }
    internal bool Pay(int player,int ore,int plasma)
    {if(Ore[player]<ore||Plasma[player]<plasma)return false;Ore[player]-=ore;Plasma[player]-=plasma;return true;}
    internal bool BeginBuild(SimWorld world,Command c)
    {
        var s=world.Entities;int worker=c.Entity.Index;
        if(s.Type[worker].IsBuilding||!DefDatabase.Units[s.Type[worker].Definition].Worker||(uint)c.Definition>=(uint)DefDatabase.Buildings.Length)return false;
        var d=DefDatabase.Buildings[c.Definition];if(d.Faction!=DefDatabase.Units[s.Type[worker].Definition].Faction||!HasTech(s,c.Player,d.RequiredTech))return false;
        int x=c.Position.X.FloorToInt(),y=c.Position.Y.FloorToInt();
        bool extractor=d.Id is 5 or 11;
        int resource=Grid.Contains(x,y)?world.Map.Grid[x,y].ResourceNodeId:-1;
        if(extractor&&(resource<0||!_gas[resource]))return false;
        for(int yy=y;yy<y+d.Width;yy++)for(int xx=x;xx<x+d.Width;xx++)
        {
            if(!Grid.Contains(xx,yy)||(!world.Map.Grid[xx,yy].Buildable&&!(extractor&&xx==x&&yy==y))||!Visible(s,c.Player,xx,yy))return false;
            for(int i=0;i<s.Capacity;i++)
            {
                if(s.IdAt(i)==EntityId.None||i==worker)continue;
                var p=s.Transform[i].Position;int ix=p.X.FloorToInt(),iy=p.Y.FloorToInt();
                int width=s.Type[i].IsBuilding?DefDatabase.Buildings[s.Type[i].Definition].Width:1;
                if(xx>=ix&&xx<ix+width&&yy>=iy&&yy<iy+width)return false;
            }
        }
        if(s.Count==s.Capacity||!Pay(c.Player,d.Ore,d.Plasma))return false;
        var building=world.Spawn(new SpawnSpec(c.Player,c.Definition,true,c.Position));
        _constructionLeft[building.Index]=d.BuildTicks;_constructionOre[building.Index]=d.Ore;_constructionPlasma[building.Index]=d.Plasma;
        _constructionWorker[building.Index]=d.Faction==Faction.Lumina?c.Entity:EntityId.None;
        s.Health[building.Index].Current=Fix64.One;
        return true;
    }
    internal bool WorkerLocked(EntityId worker)
    {for(int i=0;i<_constructionWorker.Length;i++)if(_constructionLeft[i]>0&&_constructionWorker[i]==worker)return true;return false;}
    internal bool CancelBuild(EntityStore s,EntityId building)
    {
        int i=building.Index;if(_constructionLeft[i]<=0)return false;int p=s.Owner[i].Player;
        Ore[p]+=_constructionOre[i];Plasma[p]+=_constructionPlasma[i];_constructionLeft[i]=0;_constructionWorker[i]=EntityId.None;return true;
    }
    internal bool BeginGather(EntityStore s,Command c)
    {
        int i=c.Entity.Index;
        if(s.Type[i].IsBuilding||!DefDatabase.Units[s.Type[i].Definition].Worker||(uint)c.Definition>=(uint)_remaining.Length)return false;
        if(_gas[c.Definition]&&!HasExtractor(s,c.Player,c.Definition))return false;
        s.Cargo[i].ResourceNode=c.Definition;_gatherTimer[i]=0;_returning[i]=false;return true;
    }
    private bool HasExtractor(EntityStore s,int player,int node)
    {
        for(int i=0;i<s.Capacity;i++)if(s.IdAt(i)!=EntityId.None&&s.Type[i].IsBuilding&&s.Owner[i].Player==player&&s.Type[i].Definition is 5 or 11&&_constructionLeft[i]==0&&s.Transform[i].Position.X.FloorToInt()==_nodeX[node]&&s.Transform[i].Position.Y.FloorToInt()==_nodeY[node])return true;
        return false;
    }
    private static bool Visible(EntityStore s,int player,int x,int y)
    {
        for(int i=0;i<s.Capacity;i++)if(s.IdAt(i)!=EntityId.None&&s.Owner[i].Player==player)
        {int dx=s.Transform[i].Position.X.FloorToInt()-x,dy=s.Transform[i].Position.Y.FloorToInt()-y;if(dx*dx+dy*dy<=s.Vision[i].Radius*s.Vision[i].Radius)return true;}
        return false;
    }
    internal void Tick(SimWorld world,UnitState[] states,Command[] commands,MovementSystem movement)
    {
        var s=world.Entities;Array.Clear(_slots);
        for(int i=0;i<s.Capacity;i++)
        {
            var id=s.IdAt(i);if(id==EntityId.None){_constructionLeft[i]=0;_constructionWorker[i]=EntityId.None;continue;}
            int player=s.Owner[i].Player;
            if(s.Type[i].IsBuilding)
            {
                var d=DefDatabase.Buildings[s.Type[i].Definition];
                if(_constructionLeft[i]>0)
                {
                    if(d.Faction==Faction.Lumina&&!s.IsAlive(_constructionWorker[i]))continue;
                    _constructionLeft[i]--;s.Health[i].Current=FixMath.Min(s.Health[i].Maximum,s.Health[i].Current+Fix64.FromRatio(d.Health,d.BuildTicks));
                    if(_constructionLeft[i]==0){s.Health[i].Current=s.Health[i].Maximum;_constructionWorker[i]=EntityId.None;world.Emit(player,"BuildComplete",id);}
                }
                else if(d.Faction==Faction.Verge)s.Health[i].Current=FixMath.Min(s.Health[i].Maximum,s.Health[i].Current+Fix64.FromRatio(Rule(9),20));
                continue;
            }
            if(states[i]==UnitState.Building&&!WorkerLocked(id)){states[i]=UnitState.Idle;continue;}
            if(states[i]==UnitState.Repairing)
            {
                var target=commands[i].Target;if(!s.IsAlive(target)||s.Owner[target.Index].Player!=player||!s.Type[target.Index].IsBuilding||DefDatabase.Buildings[s.Type[target.Index].Definition].Faction!=Faction.Lumina){states[i]=UnitState.Idle;continue;}
                if((s.Transform[target.Index].Position-s.Transform[i].Position).Length>Fix64.FromInt(2)){movement.Move(s,id,s.Transform[target.Index].Position);continue;}
                s.Movement[i].Active=false;
                if(s.Health[target.Index].Current<s.Health[target.Index].Maximum&&world.TickNumber%20==0&&Pay(player,1,0))s.Health[target.Index].Current=FixMath.Min(s.Health[target.Index].Maximum,s.Health[target.Index].Current+Fix64.FromInt(Rule(8)));
                continue;
            }
            if(states[i]!=UnitState.Gathering)continue;
            int node=s.Cargo[i].ResourceNode;if((uint)node>=(uint)_remaining.Length){states[i]=UnitState.Idle;continue;}
            Fix2 nodePos=new(Fix64.FromRatio(_nodeX[node]*2+1,2),Fix64.FromRatio(_nodeY[node]*2+1,2));
            if(_returning[i])
            {
                int depot=-1;Fix64 nearest=Fix64.MaxValue;
                for(int j=0;j<s.Capacity;j++)if(s.IdAt(j)!=EntityId.None&&s.Type[j].IsBuilding&&s.Owner[j].Player==player&&s.Type[j].Definition is 0 or 6&&_constructionLeft[j]==0)
                {var distance=(s.Transform[j].Position-s.Transform[i].Position).LengthSquared;if(distance<nearest){nearest=distance;depot=j;}}
                if(depot<0)continue;
                if(nearest>Fix64.FromInt(9)){movement.Move(s,id,s.Transform[depot].Position);continue;}
                Ore[player]+=s.Cargo[i].Ore;Plasma[player]+=s.Cargo[i].Plasma;s.Cargo[i].Ore=0;s.Cargo[i].Plasma=0;_returning[i]=false;
            }
            if(_remaining[node]<=0){states[i]=UnitState.Idle;continue;}
            if(_gas[node]&&!HasExtractor(s,player,node)){states[i]=UnitState.Idle;continue;}
            if((nodePos-s.Transform[i].Position).LengthSquared>Fix64.One){movement.Move(s,id,nodePos);continue;}
            s.Movement[i].Active=false;
            if(_slots[node]>=Rule(7))continue;
            _slots[node]++;
            if(++_gatherTimer[i]<Rule(6))continue;
            _gatherTimer[i]=0;int amount=Math.Min(_remaining[node],Rule(_gas[node]?5:4));_remaining[node]-=amount;
            if(_gas[node])s.Cargo[i].Plasma=amount;else s.Cargo[i].Ore=amount;_returning[i]=true;
        }
    }
    internal void Hash(ref WorldHasher h)
    {
        for(int p=0;p<4;p++){h.AddInt32(Ore[p]);h.AddInt32(Plasma[p]);for(int u=0;u<3;u++)h.AddInt32(Upgrades[p,u]);}
        foreach(int value in _remaining)h.AddInt32(value);
        for(int i=0;i<_gatherTimer.Length;i++){h.AddInt32(_gatherTimer[i]);h.AddByte(_returning[i]?(byte)1:(byte)0);h.AddInt32(_constructionLeft[i]);h.AddInt32(_constructionWorker[i].Index);h.AddUInt64(_constructionWorker[i].Generation);h.AddInt32(_constructionOre[i]);h.AddInt32(_constructionPlasma[i]);}
    }
}
