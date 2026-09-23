using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.Data;
using RtsGame.Sim.Entities;
using RtsGame.Sim.World;

namespace RtsGame.Sim.Systems;

internal sealed class ProductionSystem
{
    private struct Job {internal int Definition,Left,Ore,Plasma,Supply;internal bool Research;}
    private readonly Job[,] _jobs;
    private readonly int[] _count;
    internal ProductionSystem(int capacity){_jobs=new Job[capacity,5];_count=new int[capacity];}
    internal ProductionOrder Order(int index,int queue)
    {
        if(queue<0 || queue>=_count[index])return new ProductionOrder(-1,0,false);
        var job=_jobs[index,queue];return new ProductionOrder(job.Definition,job.Left,job.Research);
    }
    internal int QueueCount(int index)=>_count[index];
    internal PlayerResources Resources(EntityStore s,EconomySystem economy,int player)
    {
        int used=0,max=0;
        for(int i=0;i<s.Capacity;i++)if(s.IdAt(i)!=EntityId.None&&s.Owner[i].Player==player)
        {
            if(s.Type[i].IsBuilding){if(economy.ConstructionLeft(i)==0)max+=DefDatabase.Buildings[s.Type[i].Definition].Supply;}
            else used+=DefDatabase.Units[s.Type[i].Definition].Supply;
            for(int q=0;q<_count[i];q++)used+=_jobs[i,q].Supply;
        }
        return new(economy.Ore[player],economy.Plasma[player],used,max);
    }
    internal bool Enqueue(EntityStore s,EconomySystem economy,Command c)
    {
        int i=c.Entity.Index;if(!s.Type[i].IsBuilding||_count[i]==5||economy.ConstructionLeft(i)>0)return false;
        bool research=c.Type==CommandType.Research;
        Job job;
        if(research)
        {
            if((uint)c.Definition>=(uint)DefDatabase.Upgrades.Length||s.Type[i].Definition is not (3 or 9))return false;
            var d=DefDatabase.Upgrades[c.Definition];int kind=d.Id/3;
            if(economy.Upgrades[c.Player,kind]+1!=d.Level)return false;
            for(int e=0;e<s.Capacity;e++)if(s.IdAt(e)!=EntityId.None&&s.Owner[e].Player==c.Player)for(int q=0;q<_count[e];q++)if(_jobs[e,q].Research&&_jobs[e,q].Definition/3==kind)return false;
            job=new Job{Definition=d.Id,Research=true,Left=d.TrainTicks,Ore=d.Ore,Plasma=d.Plasma};
        }
        else
        {
            if((uint)c.Definition>=(uint)DefDatabase.Units.Length)return false;
            var d=DefDatabase.Units[c.Definition];
            if(d.Trainer!=s.Type[i].Definition||!economy.HasTech(s,c.Player,d.RequiredTech))return false;
            var resources=Resources(s,economy,c.Player);int supply=d.Supply*d.SpawnCount;
            if(resources.UsedSupply+supply>resources.MaxSupply)return false;
            job=new Job{Definition=d.Id,Left=d.TrainTicks,Ore=d.Ore,Plasma=d.Plasma,Supply=supply};
        }
        if(!economy.Pay(c.Player,job.Ore,job.Plasma))return false;
        _jobs[i,_count[i]++]=job;UpdateComponent(s,i);return true;
    }
    internal bool Cancel(EntityStore s,EconomySystem economy,Command c)
    {
        int i=c.Entity.Index,q=c.Definition<0?_count[i]-1:c.Definition;
        if(q<0||q>=_count[i])return false;
        var job=_jobs[i,q];economy.Ore[c.Player]+=job.Ore;economy.Plasma[c.Player]+=job.Plasma;
        Remove(i,q);UpdateComponent(s,i);return true;
    }
    internal void Destroy(int index){_count[index]=0;for(int q=0;q<5;q++)_jobs[index,q]=default;}
    private void Remove(int i,int q){for(int j=q;j<_count[i]-1;j++)_jobs[i,j]=_jobs[i,j+1];_jobs[i,--_count[i]]=default;}
    private void UpdateComponent(EntityStore s,int i)
    {s.Production[i]=new ProductionComponent{QueueCount=_count[i],Definition=_count[i]>0?_jobs[i,0].Definition:-1,RemainingTicks=_count[i]>0?_jobs[i,0].Left:0};}
    internal void Tick(SimWorld world,EconomySystem economy)
    {
        var s=world.Entities;
        System.Span<Fix2> positions=stackalloc Fix2[2];
        for(int i=0;i<s.Capacity;i++)
        {
            if(s.IdAt(i)==EntityId.None){Destroy(i);continue;}
            if(_count[i]==0)continue;
            ref Job job=ref _jobs[i,0];
            if(job.Left>0)job.Left--;
            if(job.Left>0){UpdateComponent(s,i);continue;}
            int player=s.Owner[i].Player;
            if(job.Research)
            {
                var d=DefDatabase.Upgrades[job.Definition];economy.Upgrades[player,d.Id/3]=d.Level;
                if(d.Id/3==2)for(int e=0;e<s.Capacity;e++)if(s.IdAt(e)!=EntityId.None&&!s.Type[e].IsBuilding&&s.Owner[e].Player==player)s.Movement[e].Speed=DefDatabase.Units[s.Type[e].Definition].Speed+Fix64.FromRatio(economy.UpgradeAmount(player,2),1000);
                world.Emit(player,"UpgradeComplete",s.IdAt(i));Remove(i,0);UpdateComponent(s,i);continue;
            }
            UnitDef unit=DefDatabase.Units[job.Definition];
            var resources=Resources(s,economy,player);
            if(resources.UsedSupply>resources.MaxSupply||s.Count+unit.SpawnCount>s.Capacity)continue;
            // Find all spawn positions before mutating, so a pair is produced atomically.
            int found=0;
            int bx=s.Transform[i].Position.X.FloorToInt(),by=s.Transform[i].Position.Y.FloorToInt();
            int width=DefDatabase.Buildings[s.Type[i].Definition].Width;
            for(int radius=1;radius<=5&&found<unit.SpawnCount;radius++)
            for(int y=by-radius;y<=by+width+radius&&found<unit.SpawnCount;y++)
            for(int x=bx-radius;x<=bx+width+radius&&found<unit.SpawnCount;x++)
            {
                if(!Grid.Contains(x,y)||!world.Map.Grid[x,y].Walkable||world.Map.Grid[x,y].ResourceNodeId>=0)continue;
                bool occupied=false;
                for(int e=0;e<s.Capacity;e++)if(s.IdAt(e)!=EntityId.None)
                {
                    int ex=s.Transform[e].Position.X.FloorToInt(),ey=s.Transform[e].Position.Y.FloorToInt();int ew=s.Type[e].IsBuilding?DefDatabase.Buildings[s.Type[e].Definition].Width:1;
                    if(x>=ex&&x<ex+ew&&y>=ey&&y<ey+ew){occupied=true;break;}
                }
                if(occupied)continue;
                var position=new Fix2(Fix64.FromRatio(x*2+1,2),Fix64.FromRatio(y*2+1,2));
                bool duplicate=false;for(int p=0;p<found;p++)if(positions[p]==position)duplicate=true;
                if(!duplicate)positions[found++]=position;
            }
            if(found<unit.SpawnCount)continue;
            for(int n=0;n<unit.SpawnCount;n++){var id=world.Spawn(new SpawnSpec(player,unit.Id,false,positions[n]));s.Movement[id.Index].Speed=unit.Speed+Fix64.FromRatio(economy.UpgradeAmount(player,2),1000);world.RallyProduced(s.IdAt(i),id);world.Emit(player,"UnitComplete",id);}
            Remove(i,0);UpdateComponent(s,i);
        }
    }
    internal void Hash(ref WorldHasher h)
    {
        for(int i=0;i<_count.Length;i++){h.AddInt32(_count[i]);for(int q=0;q<_count[i];q++){var j=_jobs[i,q];h.AddInt32(j.Definition);h.AddInt32(j.Left);h.AddInt32(j.Ore);h.AddInt32(j.Plasma);h.AddInt32(j.Supply);h.AddByte(j.Research?(byte)1:(byte)0);}}
    }
}
