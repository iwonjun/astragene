using System;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.Data;
using RtsGame.Sim.Entities;
using RtsGame.Sim.World;

namespace RtsGame.Sim.Systems;

internal sealed class CombatSystem
{
    private struct Projectile
    {
        internal bool Active;
        internal EntityId Source,Target;
        internal Fix2 Position,Destination;
        internal int Definition,Height,Owner,Life;
    }
    private readonly Projectile[] _projectiles;
    internal CombatSystem(int capacity){_projectiles=new Projectile[capacity*4];}
    internal static Fix64 Damage(int amount,AttackType type,ArmorType armorType,int armor,bool uphill,int scale=100)
    {
        int multiplier=100;
        if(type==AttackType.Piercing)multiplier=armorType==ArmorType.Light?100:armorType==ArmorType.Medium?75:50;
        if(type==AttackType.Explosive)multiplier=armorType==ArmorType.Light?50:armorType==ArmorType.Medium?75:100;
        var damage=Fix64.FromInt(amount)*Fix64.FromRatio(multiplier,100)*Fix64.FromRatio(scale,100);
        if(uphill)damage*=Fix64.FromRatio(3,4);
        return FixMath.Max(Fix64.One,damage-Fix64.FromInt(armor));
    }
    internal void Tick(EntityStore s,MapData map,long tick,UnitState[] states,Command[] commands,MovementSystem movement,Action<EntityId,EntityId> kill)
    {
        for(int i=0;i<s.Capacity;i++)
        {
            EntityId id=s.IdAt(i);if(id==EntityId.None || s.Type[i].IsBuilding)continue;
            UnitDef definition=DefDatabase.Units[s.Type[i].Definition];
            ref CombatComponent combat=ref s.Combat[i];
            if(combat.Cooldown>0)combat.Cooldown--;
            bool enabled=states[i] is UnitState.Idle or UnitState.Attacking or UnitState.Holding or UnitState.Patrolling || commands[i].Type==CommandType.AttackMove;
            if(!enabled){combat.Windup=0;continue;}
            if(tick%8==0 || (states[i]==UnitState.Attacking && s.IsAlive(commands[i].Target)))
            {
                if(states[i]==UnitState.Attacking && Enemy(s,i,commands[i].Target))combat.Target=commands[i].Target;
                else if(!Enemy(s,i,combat.Target))combat.Target=Acquire(s,i,definition.Vision,combat.LastAttacker);
            }
            if(!Enemy(s,i,combat.Target)){combat.Windup=0;continue;}
            Fix2 target=s.Transform[combat.Target.Index].Position;
            Fix64 range=Fix64.FromInt(definition.Range);
            bool inRange=(target-s.Transform[i].Position).LengthSquared<=range*range;
            if(!inRange)
            {
                combat.Windup=0;
                if(states[i]!=UnitState.Holding)movement.Move(s,id,target);
                continue;
            }
            if(!definition.AttackWhileMoving && s.Movement[i].Active){s.Movement[i].Active=false;combat.Windup=0;continue;}
            if(states[i]!=UnitState.Moving)s.Movement[i].Active=false;
            if(combat.Cooldown>0)continue;
            if(combat.Windup==0){combat.Windup=definition.WindupTicks; if(combat.Windup>0)continue;}
            else {combat.Windup--;if(combat.Windup>0)continue;}
            int h=Height(map,s.Transform[i].Position);
            if(definition.ProjectileSpeed==0)Hit(s,map,id,combat.Target,target,definition,h,kill);
            else
            {
                for(int p=0;p<_projectiles.Length;p++)if(!_projectiles[p].Active)
                {
                    _projectiles[p]=new Projectile{Active=true,Source=id,Target=combat.Target,Position=s.Transform[i].Position,Destination=target,Definition=definition.Id,Height=h,Owner=s.Owner[i].Player,Life=200};break;
                }
            }
            combat.Cooldown=definition.CooldownTicks;
        }
        for(int p=0;p<_projectiles.Length;p++)
        {
            ref Projectile shot=ref _projectiles[p];if(!shot.Active)continue;
            if(s.IsAlive(shot.Target))shot.Destination=s.Transform[shot.Target.Index].Position;
            var d=DefDatabase.Units[shot.Definition];Fix2 delta=shot.Destination-shot.Position;
            Fix64 distance=delta.Length,step=Fix64.FromRatio(d.ProjectileSpeed,20);
            if(distance<=step)
            {Hit(s,map,shot.Source,shot.Target,shot.Destination,d,shot.Height,kill);shot.Active=false;}
            else {shot.Position+=delta/distance*step;if(--shot.Life<=0)shot.Active=false;}
        }
    }
    private static bool Enemy(EntityStore s,int source,EntityId target)=>s.IsAlive(target)&&s.Owner[target.Index].Player!=s.Owner[source].Player;
    private static EntityId Acquire(EntityStore s,int source,int vision,EntityId attacker)
    {
        Fix64 limit=Fix64.FromInt(vision*vision);
        if(Enemy(s,source,attacker)&&(s.Transform[attacker.Index].Position-s.Transform[source].Position).LengthSquared<=limit)return attacker;
        EntityId result=EntityId.None;
        for(int i=0;i<s.Capacity;i++)
        {
            var id=s.IdAt(i);if(!Enemy(s,source,id))continue;
            Fix64 distance=(s.Transform[i].Position-s.Transform[source].Position).LengthSquared;
            if(distance<limit){limit=distance;result=id;}
        }
        return result;
    }
    private static int Height(MapData map,Fix2 p)=>map.Grid[p.X.FloorToInt(),p.Y.FloorToInt()].Height;
    private static void Hit(EntityStore s,MapData map,EntityId source,EntityId target,Fix2 center,UnitDef d,int sourceHeight,Action<EntityId,EntityId> kill)
    {
        Fix64 radius=Fix64.FromRatio(d.SplashRadiusMilli,1000);
        for(int i=0;i<s.Capacity;i++)
        {
            var id=s.IdAt(i);if(id==EntityId.None)continue;
            int scale=100;
            if(radius==Fix64.Zero){if(id!=target)continue;}
            else
            {
                Fix64 distance=(s.Transform[i].Position-center).Length;
                if(distance>radius)continue;
                scale=distance<=radius/Fix64.FromInt(3)?100:distance<=radius*Fix64.FromRatio(2,3)?50:25;
            }
            var type=s.Type[i];int armor=type.IsBuilding?DefDatabase.Buildings[type.Definition].Armor:DefDatabase.Units[type.Definition].Armor;
            ArmorType defense=type.IsBuilding?ArmorType.Heavy:DefDatabase.Units[type.Definition].Defense;
            s.Health[i].Current-=Damage(d.Damage,d.Attack,defense,armor,sourceHeight<Height(map,s.Transform[i].Position),scale);
            s.Combat[i].LastAttacker=source;
            if(s.Health[i].Current<=Fix64.Zero)kill(id,source);
        }
    }
    internal void Hash(ref WorldHasher h)
    {
        for(int i=0;i<_projectiles.Length;i++)
        {
            ref Projectile p=ref _projectiles[i];h.AddByte(p.Active?(byte)1:(byte)0);if(!p.Active)continue;
            h.AddInt32(i);h.AddInt32(p.Source.Index);h.AddUInt64(p.Source.Generation);h.AddInt32(p.Target.Index);h.AddUInt64(p.Target.Generation);
            h.AddFix2(p.Position);h.AddFix2(p.Destination);h.AddInt32(p.Definition);h.AddInt32(p.Height);h.AddInt32(p.Owner);h.AddInt32(p.Life);
        }
    }
}
