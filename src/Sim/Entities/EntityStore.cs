using System;
using RtsGame.Sim.Core;

namespace RtsGame.Sim.Entities;

public sealed class EntityStore
{
    private readonly uint[] _generations;
    private readonly bool[] _alive;
    private readonly int[] _next;
    private int _free;
    internal readonly TransformComponent[] Transform;
    internal readonly HealthComponent[] Health;
    internal readonly OwnerComponent[] Owner;
    internal readonly UnitTypeComponent[] Type;
    internal readonly MovementComponent[] Movement;
    internal readonly CombatComponent[] Combat;
    internal readonly CargoComponent[] Cargo;
    internal readonly ProductionComponent[] Production;
    internal readonly VisionComponent[] Vision;
    public int Capacity => _alive.Length;
    public int Count { get; private set; }
    public EntityStore(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _generations = new uint[capacity]; _alive = new bool[capacity]; _next = new int[capacity];
        Transform = new TransformComponent[capacity]; Health = new HealthComponent[capacity];
        Owner = new OwnerComponent[capacity]; Type = new UnitTypeComponent[capacity];
        Movement = new MovementComponent[capacity]; Combat = new CombatComponent[capacity];
        Cargo = new CargoComponent[capacity]; Production = new ProductionComponent[capacity]; Vision = new VisionComponent[capacity];
        for (int i = 0; i < capacity; i++) { _generations[i] = 1; _next[i] = i + 1; }
        _next[capacity - 1] = -1;
    }
    public bool IsAlive(EntityId id) => (uint)id.Index < (uint)Capacity && _alive[id.Index] && _generations[id.Index] == id.Generation;
    public EntityId IdAt(int index)
    {
        if ((uint)index >= (uint)Capacity || !_alive[index]) return EntityId.None;
        return new EntityId(index, _generations[index]);
    }
    public EntitySnapshot Get(EntityId id)
    {
        if (!IsAlive(id)) throw new ArgumentException("Stale or invalid entity ID.");
        int i = id.Index;
        return new EntitySnapshot(id, Transform[i], Health[i], Owner[i], Type[i], Movement[i], Combat[i], Cargo[i], Production[i], Vision[i]);
    }
    internal EntityId Create()
    {
        if (_free < 0) throw new InvalidOperationException("Entity capacity exhausted.");
        int i = _free; _free = _next[i]; _next[i] = -1;
        _alive[i] = true; Count++;
        Combat[i].Target = EntityId.None; Combat[i].LastAttacker = EntityId.None; Cargo[i].ResourceNode = -1;
        return new EntityId(i, _generations[i]);
    }
    internal bool Destroy(EntityId id)
    {
        if (!IsAlive(id)) return false;
        int i = id.Index;
        if (_generations[i] == uint.MaxValue) throw new OverflowException("Entity generation exhausted.");
        _alive[i] = false; _generations[i]++; Count--;
        Transform[i] = default; Health[i] = default; Owner[i] = default; Type[i] = default;
        Movement[i] = default; Combat[i] = default; Cargo[i] = default; Production[i] = default; Vision[i] = default;
        _next[i] = _free; _free = i;
        return true;
    }
    public ulong Hash()
    {
        var h = new WorldHasher();
        h.AddInt32(Capacity); h.AddInt32(Count); h.AddInt32(_free);
        for (int i = 0; i < Capacity; i++)
        {
            h.AddUInt64(_generations[i]); h.AddInt32(_next[i]); h.AddByte(_alive[i] ? (byte)1 : (byte)0);
            if (!_alive[i]) continue;
            h.AddFix2(Transform[i].Position); h.AddFix(Transform[i].Rotation);
            h.AddFix(Health[i].Current); h.AddFix(Health[i].Maximum);
            h.AddInt32(Owner[i].Player); h.AddInt32(Type[i].Definition); h.AddByte(Type[i].IsBuilding ? (byte)1 : (byte)0);
            h.AddFix2(Movement[i].Destination); h.AddFix(Movement[i].Speed); h.AddInt32(Movement[i].StalledTicks); h.AddByte(Movement[i].Airborne ? (byte)1 : (byte)0);
            h.AddByte(Movement[i].Active ? (byte)1 : (byte)0); h.AddFix(Movement[i].Radius); h.AddFix(Movement[i].LastDistanceSquared);
            h.AddInt32(Combat[i].Target.Index); h.AddUInt64(Combat[i].Target.Generation); h.AddInt32(Combat[i].Cooldown); h.AddInt32(Combat[i].Windup);
            h.AddInt32(Combat[i].LastAttacker.Index); h.AddUInt64(Combat[i].LastAttacker.Generation);
            h.AddInt32(Cargo[i].Ore); h.AddInt32(Cargo[i].Plasma); h.AddInt32(Cargo[i].ResourceNode);
            h.AddInt32(Production[i].Definition); h.AddInt32(Production[i].RemainingTicks); h.AddInt32(Production[i].QueueCount);
            h.AddInt32(Vision[i].Radius); h.AddByte(Vision[i].Detector ? (byte)1 : (byte)0);
        }
        return h.Value;
    }
}
