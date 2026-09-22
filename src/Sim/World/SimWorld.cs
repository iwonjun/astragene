using System;
using System.Collections.Generic;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.Data;
using RtsGame.Sim.Entities;
using RtsGame.Sim.Systems;

namespace RtsGame.Sim.World;

public readonly record struct SpawnSpec(int Player,int Definition,bool Building,Fix2 Position);
public readonly record struct SimEvent(long Tick,int Player,string Kind,EntityId Entity);

public sealed class SimWorld
{
    public MapData Map {get;}
    public EntityStore Entities {get;}
    private readonly SimClock _clock=new();
    public long TickNumber=>_clock.Tick;
    private readonly MovementSystem _movement;
    private readonly CombatSystem _combat;
    private readonly EconomySystem _economy;
    private readonly ProductionSystem _production;
    private readonly CommandQueue[] _queues;
    private readonly UnitState[] _states;
    private readonly Command[] _active;
    private readonly Fix2[] _patrolOrigin;
    private readonly Queue<SimEvent> _events=new();
    private DetRandom _random;
    public SimWorld(MapData map,ReadOnlySpan<SpawnSpec> spawns,ulong seed=1,int capacity=1024,int? initialOre=null,int? initialPlasma=null)
    {
        Map=map; Entities=new EntityStore(capacity); _random=new DetRandom(seed);
        _movement=new MovementSystem(map.Grid,capacity); _combat=new CombatSystem(capacity); _economy=new EconomySystem(map,capacity); _production=new ProductionSystem(capacity);
        _queues=new CommandQueue[capacity]; _states=new UnitState[capacity]; _active=new Command[capacity]; _patrolOrigin=new Fix2[capacity];
        for(int i=0;i<capacity;i++)_queues[i]=new CommandQueue();
        for(int p=0;p<4;p++){if(initialOre.HasValue)_economy.Ore[p]=initialOre.Value;if(initialPlasma.HasValue)_economy.Plasma[p]=initialPlasma.Value;}
        foreach(var spawn in spawns) Spawn(spawn);
    }
    internal EntityId Spawn(SpawnSpec spawn)
    {
        if(spawn.Player<0 || spawn.Player>3 || !Grid.Contains(spawn.Position.X.FloorToInt(),spawn.Position.Y.FloorToInt()))throw new ArgumentException("Invalid spawn.");
        int health,vision;
        if(spawn.Building){var d=DefDatabase.Buildings[spawn.Definition];health=d.Health;vision=d.Vision;}
        else {var d=DefDatabase.Units[spawn.Definition];health=d.Health;vision=d.Vision;}
        var id=Entities.Create();int i=id.Index;_economy.Reset(i);_production.Destroy(i);
        Entities.Transform[i].Position=spawn.Position;Entities.Health[i]=new HealthComponent{Current=Fix64.FromInt(health),Maximum=Fix64.FromInt(health)};
        Entities.Owner[i].Player=spawn.Player;Entities.Type[i]=new UnitTypeComponent{Definition=spawn.Definition,IsBuilding=spawn.Building};Entities.Vision[i].Radius=vision;
        if(!spawn.Building){var d=DefDatabase.Units[spawn.Definition];Entities.Movement[i].Speed=d.Speed;Entities.Movement[i].Airborne=d.Airborne;Entities.Movement[i].Radius=Fix64.FromRatio(EconomySystem.Rule(10),1000);}
        _queues[i].Clear();_states[i]=UnitState.Idle;_active[i]=default;_patrolOrigin[i]=Fix2.Zero;
        return id;
    }
    public PlayerResources Resources(int player)=>_production.Resources(Entities,_economy,player);
    internal void RallyProduced(EntityId parent,EntityId child){var target=Entities.Movement[parent.Index].Destination;if(target!=Fix2.Zero)Accept(new Command(CommandType.Move,Entities.Owner[parent.Index].Player,child,EntityId.None,target));}
    internal void Emit(int player,string kind,EntityId id)=>_events.Enqueue(new(_clock.Tick,player,kind,id));
    public UnitState State(EntityId id)=>Entities.IsAlive(id)?_states[id.Index]:UnitState.Idle;
    public bool TryDequeueEvent(out SimEvent value)=>_events.TryDequeue(out value);
    public void Tick(ReadOnlySpan<Command> commands)
    {
        foreach(Command command in commands) Accept(command);
        for(int i=0;i<Entities.Capacity;i++)
        {
            var id=Entities.IdAt(i);if(id==EntityId.None)continue;
            if(_states[i]==UnitState.Attacking && !Entities.IsAlive(_active[i].Target))_states[i]=UnitState.Idle;
            if(_states[i]==UnitState.Following && Entities.IsAlive(_active[i].Target))
                _movement.Move(Entities,id,Entities.Get(_active[i].Target).Transform.Position);
            if(_states[i]==UnitState.Patrolling && !Entities.Movement[i].Active)
            {
                Fix2 next=_patrolOrigin[i];_patrolOrigin[i]=_active[i].Position;
                _active[i]=_active[i] with {Position=next};_movement.Move(Entities,id,next);
            }
            if((_states[i]==UnitState.Moving && !Entities.Movement[i].Active) || (_states[i]==UnitState.Following && !Entities.IsAlive(_active[i].Target)))_states[i]=UnitState.Idle;
            if(_states[i]==UnitState.Idle && _queues[i].TryDequeue(out var queued))Execute(queued);
        }
        _economy.Tick(this,_states,_active,_movement); _production.Tick(this,_economy);
        _movement.Tick(Entities); _combat.Tick(Entities,Map,_clock.Tick,_states,_active,_movement,_economy,Kill); _clock.Advance();
    }
    private bool Accept(Command c)
    {
        if((uint)c.Type>(uint)CommandType.Research || !Entities.IsAlive(c.Entity) || Entities.Owner[c.Entity.Index].Player!=c.Player){_events.Enqueue(new(_clock.Tick,c.Player,"Rejected",c.Entity));return false;}
        if(_economy.WorkerLocked(c.Entity) && c.Type!=CommandType.Cancel)return false;
        if(c.Queued && c.Type!=CommandType.Stop && c.Type!=CommandType.Cancel)return _queues[c.Entity.Index].Enqueue(c);
        _queues[c.Entity.Index].Clear();return Execute(c);
    }
    private bool Execute(Command c)
    {
        int i=c.Entity.Index;
        if(c.Type is CommandType.Train or CommandType.Research)return _production.Enqueue(Entities,_economy,c);
        if(c.Type==CommandType.Cancel)
        {
            if(Entities.Type[i].IsBuilding&&_economy.CancelBuild(Entities,c.Entity)){Kill(c.Entity,EntityId.None);return true;}
            return _production.Cancel(Entities,_economy,c);
        }
        if(c.Type==CommandType.Build && !_economy.BeginBuild(this,c))return false;
        if(c.Type==CommandType.Gather && !_economy.BeginGather(Entities,c))return false;
        _active[i]=c;_states[i]=CommandStateMachine.Next(c.Type);
        Entities.Movement[i].Active=false;
        switch(c.Type)
        {
            case CommandType.Move:case CommandType.AttackMove:case CommandType.Patrol:
                _patrolOrigin[i]=Entities.Transform[i].Position;
                if(Entities.Type[i].IsBuilding || !_movement.Move(Entities,c.Entity,c.Position)){_states[i]=UnitState.Idle;return false;}break;
            case CommandType.Follow:case CommandType.Attack:case CommandType.Repair:
                if(!Entities.IsAlive(c.Target)){_states[i]=UnitState.Idle;return false;}break;
            case CommandType.Rally:Entities.Movement[i].Destination=c.Position;break;
        }
        return true;
    }
    private void Kill(EntityId id,EntityId source)
    {
        int player=Entities.Owner[id.Index].Player;
        if(Entities.Destroy(id)){_production.Destroy(id.Index);_economy.Reset(id.Index);_queues[id.Index].Clear();_states[id.Index]=UnitState.Idle;_events.Enqueue(new(_clock.Tick,player,"Death",id));}
    }
    public ulong Hash()
    {
        var h=new WorldHasher();h.AddUInt64(Map.Hash());h.AddUInt64(Entities.Hash());h.AddInt64(_clock.Tick);
        h.AddUInt64(_random.State0);h.AddUInt64(_random.State1);
        Span<byte> bytes=stackalloc byte[Command.ByteSize];
        for(int i=0;i<Entities.Capacity;i++)
        {
            if(Entities.IdAt(i)==EntityId.None)continue;
            h.AddInt32((int)_states[i]);_queues[i].Hash(ref h);_active[i].Write(bytes);h.Add(bytes);h.AddFix2(_patrolOrigin[i]);
        }
        _combat.Hash(ref h); _economy.Hash(ref h); _production.Hash(ref h);
        return h.Value;
    }
}
