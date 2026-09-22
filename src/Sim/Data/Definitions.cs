using RtsGame.Sim.Core;

namespace RtsGame.Sim.Data;

public enum Faction { Lumina, Verge }
public enum ArmorType { Light, Medium, Heavy }
public enum AttackType { Normal, Piercing, Explosive }
public readonly record struct UnitDef(int Id, string Name, Faction Faction, int Health, int Armor, int Damage,
    int Range, int SpeedMilli, int Supply, int Ore, int Plasma, int TrainTicks, int Vision,
    AttackType Attack, ArmorType Defense, bool Airborne, bool Worker, int CooldownTicks, int WindupTicks, int Trainer, int RequiredTech, int ProjectileSpeed, int SplashRadiusMilli, bool AttackWhileMoving, int SpawnCount)
{
    public Fix64 Speed => Fix64.FromRatio(SpeedMilli, 1000);
}
public readonly record struct BuildingDef(int Id, string Name, Faction Faction, int Health, int Armor,
    int Ore, int Plasma, int BuildTicks, int Supply, int Width, int Vision, int RequiredTech);
public readonly record struct UpgradeDef(int Id, string Name, int Level, int Ore, int Plasma, int TrainTicks, int Amount);

public readonly record struct RuleDef(int Id,string Name,int Value);
