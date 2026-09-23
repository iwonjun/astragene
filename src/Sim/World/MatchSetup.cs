using System;
using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;
using RtsGame.Sim.Data;
namespace RtsGame.Sim.World;
public static class MatchSetup
{
    public static SpawnSpec[] Spawns(ReadOnlySpan<int> factions,bool soak=false)
    {
        if(factions.Length<1 || factions.Length>4)throw new ArgumentException("1..4 players required.");
        var spawns=new SpawnSpec[factions.Length*(soak?21:7)];int n=0;
        for(int p=0;p<factions.Length;p++)
        {
            if((uint)factions[p]>1)throw new ArgumentException("Invalid faction.");
            int x=p is 0 or 3?20:107,y=p is 0 or 2?20:107;
            spawns[n++]=new SpawnSpec(p,factions[p]*6,true,Pos(x,y));
            int count=soak?20:6;
            for(int i=0;i<count;i++)spawns[n++]=new SpawnSpec(p,factions[p]*5+(soak&&i>=6?1:0),false,Pos(x-5+i%10,y+4+i/10));
        }
        return spawns;
    }
    private static Fix2 Pos(int x,int y)=>new(Fix64.FromInt(x),Fix64.FromInt(y));
    public static ulong ContentHash()
    {
        var h=new WorldHasher();
        // Generated immutable definitions, encoded as invariant integer/string values.
        foreach(var d in DefDatabase.Units){h.AddInt32(d.Id);h.AddInt32(d.Health);h.AddInt32(d.Damage);h.AddInt32(d.Armor);h.AddInt32(d.Range);h.AddInt32(d.SpeedMilli);h.AddInt32(d.TrainTicks);h.AddInt32(d.Ore);h.AddInt32(d.Plasma);h.AddInt32(d.Supply);h.AddInt32(d.Vision);h.AddInt32(d.CooldownTicks);h.AddInt32(d.WindupTicks);h.AddInt32(d.ProjectileSpeed);h.AddInt32(d.SplashRadiusMilli);h.AddInt32(d.SpawnCount);h.AddInt32(d.Trainer);h.AddInt32(d.RequiredTech);h.AddInt32((int)d.Faction);h.AddInt32((int)d.Attack);h.AddInt32((int)d.Defense);h.AddByte(d.Airborne?(byte)1:(byte)0);h.AddByte(d.Worker?(byte)1:(byte)0);h.AddByte(d.AttackWhileMoving?(byte)1:(byte)0);}
        foreach(var d in DefDatabase.Buildings){h.AddInt32(d.Id);h.AddInt32(d.Health);h.AddInt32(d.Armor);h.AddInt32(d.Ore);h.AddInt32(d.Plasma);h.AddInt32(d.BuildTicks);h.AddInt32(d.Supply);h.AddInt32(d.Width);h.AddInt32(d.Vision);h.AddInt32(d.RequiredTech);h.AddInt32((int)d.Faction);}
        foreach(var d in DefDatabase.Upgrades){h.AddInt32(d.Id);h.AddInt32(d.Level);h.AddInt32(d.Ore);h.AddInt32(d.Plasma);h.AddInt32(d.TrainTicks);h.AddInt32(d.Amount);}
        foreach(var d in DefDatabase.Rules){h.AddInt32(d.Id);h.AddInt32(d.Value);}return h.Value;
    }
}
