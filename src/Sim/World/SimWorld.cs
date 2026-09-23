using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RtsGame.Sim.Ai;
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
    internal EntityStore Entities {get;}
    private readonly SimClock _clock=new();
    public long TickNumber=>_clock.Tick;
    private readonly MovementSystem _movement;
    private readonly CombatSystem _combat;
    private readonly VisionSystem _vision;
    private readonly EconomySystem _economy;
    private readonly ProductionSystem _production;
    private readonly CommandQueue[] _queues;
    private readonly UnitState[] _states;
    private readonly Command[] _active;
    private readonly Fix2[] _patrolOrigin;
    private readonly Queue<SimEvent> _events=new();
    private DetRandom _random;
    private readonly bool[] _surrendered=new bool[4];
    private readonly ulong _mapHash;
    private readonly AiPlayer[] _ai;
    private readonly AiSeat[] _aiSeats;
    private readonly VisibilityFilter?[] _aiViews=new VisibilityFilter?[4];
    public ReadOnlySpan<AiSeat> AiSeats=>_aiSeats;
    public SimWorld(MapData map,ReadOnlySpan<SpawnSpec> spawns,ulong seed=1,int capacity=1024,int? initialOre=null,int? initialPlasma=null,AiSeat[]? ai=null)
    {
        _aiSeats=ai==null?Array.Empty<AiSeat>():(AiSeat[])ai.Clone();_ai=new AiPlayer[_aiSeats.Length];
        for(int n=0;n<_aiSeats.Length;n++){for(int m=0;m<n;m++)if(_aiSeats[m].Player==_aiSeats[n].Player)throw new ArgumentException("Duplicate AI seat.");_ai[n]=new AiPlayer(_aiSeats[n],capacity);}
        Map=map; _mapHash=map.Hash(); Entities=new EntityStore(capacity); _random=new DetRandom(seed);
        _movement=new MovementSystem(map.Grid,capacity); _combat=new CombatSystem(capacity); _vision=new VisionSystem(capacity); _economy=new EconomySystem(map,capacity); _production=new ProductionSystem(capacity);
        _queues=new CommandQueue[capacity]; _states=new UnitState[capacity]; _active=new Command[capacity]; _patrolOrigin=new Fix2[capacity];
        for(int i=0;i<capacity;i++)_queues[i]=new CommandQueue();
        for(int p=0;p<4;p++){if(initialOre.HasValue)_economy.Ore[p]=initialOre.Value;if(initialPlasma.HasValue)_economy.Plasma[p]=initialPlasma.Value;}
        foreach(var spawn in spawns) Spawn(spawn);
        _vision.Update(Entities,Map);
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
    internal bool Surrendered(int player)=>_surrendered[player];
    internal int UpgradeLevel(int player,int kind)=>(uint)kind<3?_economy.Upgrades[player,kind]:throw new ArgumentOutOfRangeException(nameof(kind));
    internal ProductionOrder ProductionAt(EntityId id,int index)=>_production.Order(id.Index,index);
    internal int ConstructionTicks(EntityId id)=>_economy.ConstructionLeft(id.Index);
    public VisibilityFilter ViewFor(int player)=>new(this,Entities,_vision,player);
    internal bool VisibleTo(int player,int x,int y)=>_vision.At(player,x,y)==Visibility.Visible;
    internal PlayerResources Resources(int player)=>_production.Resources(Entities,_economy,player);
    internal void RallyProduced(EntityId parent,EntityId child){var target=Entities.Movement[parent.Index].Destination;if(target!=Fix2.Zero)Accept(new Command(CommandType.Move,Entities.Owner[parent.Index].Player,child,EntityId.None,target));}
    internal void Emit(int player,string kind,EntityId id)=>_events.Enqueue(new(_clock.Tick,player,kind,id));
    internal UnitState State(EntityId id)=>Entities.IsAlive(id)?_states[id.Index]:UnitState.Idle;
    internal bool TryDequeueEvent(out SimEvent value)=>_events.TryDequeue(out value);
    public void Tick(ReadOnlySpan<Command> commands)
    {
        foreach(Command command in commands)if(!Accept(command))Emit(command.Player,"Rejected",command.Entity);
        // Computer players decide from their filtered view and submit ordinary commands through Accept.
        foreach(var ai in _ai)
        {
            if(!ai.ShouldThink(_clock.Tick))continue;
            var view=_aiViews[ai.Player]??=ViewFor(ai.Player);
            foreach(Command command in ai.Think(view,Map,_clock.Tick))Accept(command);
        }
        for(int i=0;i<Entities.Capacity;i++)
        {
            var id=Entities.IdAt(i);if(id==EntityId.None)continue;
            if(_states[i]==UnitState.Attacking && !Entities.IsAlive(_active[i].Target))_states[i]=UnitState.Idle;
            if(_states[i]==UnitState.Following && !_vision.CanSee(Entities,Entities.Owner[i].Player,_active[i].Target)){_states[i]=UnitState.Idle;Entities.Movement[i].Active=false;}
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
        _vision.Update(Entities,Map);
        _economy.Tick(this,_states,_active,_movement); _production.Tick(this,_economy);
        _movement.Tick(Entities); _vision.Update(Entities,Map); _combat.Tick(Entities,Map,_clock.Tick,_states,_active,_movement,_economy,_vision,Kill); _vision.Update(Entities,Map); _clock.Advance();
    }
    private bool Accept(Command c)
    {
        if((uint)c.Player>=4)return false;
        if(c.Type==CommandType.Leave)
        {
            _surrendered[c.Player]=true;
            for(int n=0;n<Entities.Capacity;n++)if(Entities.IdAt(n)!=EntityId.None && Entities.Owner[n].Player==c.Player)
            {
                Entities.Owner[n].Player=-1;Entities.Movement[n].Active=false;Entities.Combat[n].Target=EntityId.None;Entities.Combat[n].Windup=0;
                _states[n]=UnitState.Idle;_active[n]=default;_queues[n].Clear();_production.Destroy(n);_economy.Reset(n);
            }
            for(int p=0;p<4;p++)Emit(p,"PlayerLeft",new EntityId(c.Player,0));
            return true;
        }
        if(_surrendered[c.Player])return false;
        if(c.Type==CommandType.Surrender){_surrendered[c.Player]=true;Emit(c.Player,"Surrendered",EntityId.None);return true;}
        if((uint)c.Type>(uint)CommandType.Research || !Entities.IsAlive(c.Entity) || Entities.Owner[c.Entity.Index].Player!=c.Player)return false;
        if(c.Target!=EntityId.None && !_vision.CanSee(Entities,c.Player,c.Target))return false;
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
    /// <summary>Diagnostic JSON for desync reports. Integers and Q32.32 raw values only; never read by gameplay.</summary>
    public string DumpState()
    {
        var b=new StringBuilder(4096);
        b.Append("{\"tick\":").Append(Str(_clock.Tick)).Append(",\"hash\":\"").Append(Hash().ToString("x16",CultureInfo.InvariantCulture)).Append('"');
        b.Append(",\"entitiesHash\":\"").Append(Entities.Hash().ToString("x16",CultureInfo.InvariantCulture)).Append('"');
        var h=new WorldHasher();_vision.Hash(ref h);b.Append(",\"visionHash\":\"").Append(h.Value.ToString("x16",CultureInfo.InvariantCulture)).Append('"');
        h=new WorldHasher();_combat.Hash(ref h);b.Append(",\"combatHash\":\"").Append(h.Value.ToString("x16",CultureInfo.InvariantCulture)).Append('"');
        h=new WorldHasher();_economy.Hash(ref h);b.Append(",\"economyHash\":\"").Append(h.Value.ToString("x16",CultureInfo.InvariantCulture)).Append('"');
        h=new WorldHasher();_production.Hash(ref h);b.Append(",\"productionHash\":\"").Append(h.Value.ToString("x16",CultureInfo.InvariantCulture)).Append('"');
        b.Append(",\"players\":[");
        for(int p=0;p<4;p++)
        {
            var r=Resources(p);if(p>0)b.Append(',');
            b.Append("{\"ore\":").Append(Str(r.Ore)).Append(",\"plasma\":").Append(Str(r.Plasma)).Append(",\"supply\":").Append(Str(r.UsedSupply)).Append(",\"maxSupply\":").Append(Str(r.MaxSupply)).Append(",\"surrendered\":").Append(_surrendered[p]?"true":"false").Append('}');
        }
        b.Append("],\"entities\":[");bool first=true;
        for(int i=0;i<Entities.Capacity;i++)
        {
            var id=Entities.IdAt(i);if(id==EntityId.None)continue;if(!first)b.Append(',');first=false;
            b.Append("{\"index\":").Append(Str(i)).Append(",\"generation\":").Append(Str(id.Generation)).Append(",\"owner\":").Append(Str(Entities.Owner[i].Player));
            b.Append(",\"building\":").Append(Entities.Type[i].IsBuilding?"true":"false").Append(",\"definition\":").Append(Str(Entities.Type[i].Definition));
            b.Append(",\"x\":").Append(Str(Entities.Transform[i].Position.X.Raw)).Append(",\"y\":").Append(Str(Entities.Transform[i].Position.Y.Raw));
            b.Append(",\"health\":").Append(Str(Entities.Health[i].Current.Raw)).Append(",\"state\":\"").Append(_states[i].ToString()).Append("\",\"queued\":").Append(Str(_queues[i].Count)).Append('}');
        }
        return b.Append("]}").ToString();
    }
    private static string Str(long value)=>value.ToString(CultureInfo.InvariantCulture);
    public ulong Hash()
    {
        var h=new WorldHasher();h.AddUInt64(_mapHash);h.AddUInt64(Entities.Hash());h.AddInt64(_clock.Tick);
        h.AddUInt64(_random.State0);h.AddUInt64(_random.State1);
        for(int player=0;player<4;player++)h.AddByte(_surrendered[player]?(byte)1:(byte)0);
        Span<byte> bytes=stackalloc byte[Command.ByteSize];
        for(int i=0;i<Entities.Capacity;i++)
        {
            if(Entities.IdAt(i)==EntityId.None)continue;
            h.AddInt32((int)_states[i]);_queues[i].Hash(ref h);_active[i].Write(bytes);h.Add(bytes);h.AddFix2(_patrolOrigin[i]);
        }
        _vision.Hash(ref h); _combat.Hash(ref h); _economy.Hash(ref h); _production.Hash(ref h);
        foreach(var ai in _ai)ai.Hash(ref h);
        return h.Value;
    }
}
