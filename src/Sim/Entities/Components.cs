using RtsGame.Sim.Core;

namespace RtsGame.Sim.Entities;

public struct TransformComponent { public Fix2 Position; public Fix64 Rotation; }
public struct HealthComponent { public Fix64 Current; public Fix64 Maximum; }
public struct OwnerComponent { public int Player; }
public struct UnitTypeComponent { public int Definition; public bool IsBuilding; }
public struct MovementComponent { public Fix2 Destination; public Fix64 Speed; public int StalledTicks; public bool Airborne; public bool Active; public Fix64 Radius; public Fix64 LastDistanceSquared; }
public struct CombatComponent { public EntityId Target; public int Cooldown; public int Windup; public EntityId LastAttacker; }
public struct CargoComponent { public int Ore; public int Plasma; public int ResourceNode; }
public struct ProductionComponent { public int Definition; public int RemainingTicks; public int QueueCount; }
public struct VisionComponent { public int Radius; public bool Detector; }
public readonly record struct EntitySnapshot(EntityId Id, TransformComponent Transform, HealthComponent Health,
    OwnerComponent Owner, UnitTypeComponent Type, MovementComponent Movement, CombatComponent Combat,
    CargoComponent Cargo, ProductionComponent Production, VisionComponent Vision);
