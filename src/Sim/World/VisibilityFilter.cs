using System;
using RtsGame.Sim.Entities;
using RtsGame.Sim.Systems;

namespace RtsGame.Sim.World;

public readonly record struct ProductionOrder(int Definition,int RemainingTicks,bool Research);

public sealed class VisibilityFilter
{
    private readonly SimWorld _world;
    private readonly EntityStore _store;
    private readonly VisionSystem _vision;
    public int Player {get;}
    public int Capacity=>_store.Capacity;
    internal VisibilityFilter(SimWorld world,EntityStore store,VisionSystem vision,int player){if((uint)player>=4)throw new ArgumentOutOfRangeException(nameof(player));_world=world;_store=store;_vision=vision;Player=player;}
    public bool Surrendered=>_world.Surrendered(Player);
    public int UpgradeLevel(int kind)=>_world.UpgradeLevel(Player,kind);
    public ProductionOrder ProductionAt(EntityId id,int index)=>TryGet(id,out var e)&&e.Owner.Player==Player?_world.ProductionAt(id,index):new ProductionOrder(-1,0,false);
    public int ConstructionTicks(EntityId id)=>TryGet(id,out var e)&&e.Owner.Player==Player?_world.ConstructionTicks(id):0;
    public PlayerResources Resources=>_world.Resources(Player);
    public bool TryDequeueEvent(out SimEvent value){while(_world.TryDequeueEvent(out value))if(value.Player==Player)return true;return false;}
    public Visibility At(int x,int y)=>_vision.At(Player,x,y);
    public EntityId IdAt(int index){var id=_store.IdAt(index);return _vision.CanSee(_store,Player,id)?id:EntityId.None;}
    public bool IsAlive(EntityId id)=>_vision.CanSee(_store,Player,id);
    public bool TryGet(EntityId id,out EntitySnapshot value){if(IsAlive(id)){value=_store.Get(id);return true;}value=default;return false;}
    public EntitySnapshot Get(EntityId id)=>TryGet(id,out var value)?value:throw new ArgumentException("Entity is not visible.");
    public bool TryGhost(int index,out EntitySnapshot snapshot)=>_vision.TryGhost(Player,index,out snapshot);
}
