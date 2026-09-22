// Generated from balance/*.csv by tools/import_balance.gd.
using System;
namespace RtsGame.Sim.Data;

public static class DefDatabase
{
    private static readonly UnitDef[] UnitData =
    {
        new UnitDef(0, "Engineer", Faction.Lumina, 68, 0, 5, 1, 3300, 1, 55, 0, 240, 7, AttackType.Normal, ArmorType.Light, false, true, 22, 3, 0, -1, 0, 0, true, 1),
        new UnitDef(1, "Trooper", Faction.Lumina, 104, 1, 13, 5, 3000, 2, 85, 0, 320, 8, AttackType.Normal, ArmorType.Light, false, false, 18, 3, 2, -1, 24, 0, true, 1),
        new UnitDef(2, "Lancer", Faction.Lumina, 172, 3, 31, 6, 2500, 3, 135, 65, 460, 8, AttackType.Explosive, ArmorType.Heavy, false, false, 32, 6, 2, 3, 24, 0, true, 1),
        new UnitDef(3, "Howitzer", Faction.Lumina, 210, 2, 48, 10, 1700, 4, 175, 115, 620, 9, AttackType.Explosive, ArmorType.Heavy, false, false, 56, 12, 2, 3, 24, 2400, false, 1),
        new UnitDef(4, "Wisp", Faction.Lumina, 86, 0, 7, 4, 5800, 2, 70, 65, 380, 12, AttackType.Normal, ArmorType.Light, true, false, 16, 2, 3, -1, 24, 0, true, 1),
        new UnitDef(5, "Tiller", Faction.Verge, 62, 0, 6, 1, 3500, 1, 50, 0, 220, 7, AttackType.Normal, ArmorType.Light, false, true, 20, 3, 6, -1, 0, 0, true, 1),
        new UnitDef(6, "Render", Faction.Verge, 118, 1, 16, 1, 4100, 2, 75, 0, 280, 7, AttackType.Piercing, ArmorType.Medium, false, false, 17, 3, 8, -1, 0, 0, true, 1),
        new UnitDef(7, "Spitter", Faction.Verge, 94, 0, 19, 5, 2800, 2, 95, 35, 340, 8, AttackType.Normal, ArmorType.Light, false, false, 25, 4, 8, -1, 24, 0, true, 1),
        new UnitDef(8, "Swarmling", Faction.Verge, 39, 0, 7, 1, 4600, 1, 30, 0, 210, 6, AttackType.Piercing, ArmorType.Light, false, false, 14, 2, 8, -1, 0, 0, true, 2),
        new UnitDef(9, "Wing", Faction.Verge, 136, 1, 22, 5, 4700, 3, 110, 90, 450, 10, AttackType.Normal, ArmorType.Medium, true, false, 26, 4, 9, -1, 24, 0, true, 1),
    };
    public static ReadOnlySpan<UnitDef> Units => UnitData;
    private static readonly BuildingDef[] BuildingData =
    {
        new BuildingDef(0, "Core", Faction.Lumina, 1600, 3, 360, 0, 1200, 12, 4, 11, -1),
        new BuildingDef(1, "Beacon", Faction.Lumina, 420, 1, 90, 0, 420, 9, 2, 7, 0),
        new BuildingDef(2, "Drill Hall", Faction.Lumina, 920, 2, 145, 0, 680, 0, 3, 8, 0),
        new BuildingDef(3, "Archive", Faction.Lumina, 720, 1, 170, 100, 820, 0, 3, 9, 2),
        new BuildingDef(4, "Sentinel", Faction.Lumina, 560, 2, 115, 45, 500, 0, 2, 10, 2),
        new BuildingDef(5, "Extractor", Faction.Lumina, 460, 1, 80, 0, 400, 0, 2, 6, 0),
        new BuildingDef(6, "Nexus Pod", Faction.Verge, 1450, 2, 330, 0, 1120, 12, 4, 11, -1),
        new BuildingDef(7, "Bloom", Faction.Verge, 380, 0, 85, 0, 390, 10, 2, 7, 6),
        new BuildingDef(8, "Hatchery", Faction.Verge, 860, 1, 135, 0, 630, 0, 3, 8, 6),
        new BuildingDef(9, "Gland", Faction.Verge, 690, 1, 160, 95, 780, 0, 3, 9, 8),
        new BuildingDef(10, "Thorn", Faction.Verge, 520, 1, 105, 40, 460, 0, 2, 10, 8),
        new BuildingDef(11, "Siphon", Faction.Verge, 430, 0, 75, 0, 370, 0, 2, 6, 6),
    };
    public static ReadOnlySpan<BuildingDef> Buildings => BuildingData;
    private static readonly UpgradeDef[] UpgradeData =
    {
        new UpgradeDef(0, "Weapons", 1, 90, 70, 800, 2),
        new UpgradeDef(1, "Weapons", 2, 140, 120, 1100, 2),
        new UpgradeDef(2, "Weapons", 3, 210, 175, 1400, 2),
        new UpgradeDef(3, "Armor", 1, 85, 65, 780, 1),
        new UpgradeDef(4, "Armor", 2, 135, 110, 1060, 1),
        new UpgradeDef(5, "Armor", 3, 200, 165, 1350, 1),
        new UpgradeDef(6, "Mobility", 1, 100, 90, 850, 150),
        new UpgradeDef(7, "Mobility", 2, 150, 135, 1150, 150),
        new UpgradeDef(8, "Mobility", 3, 220, 190, 1450, 150),
    };
    public static ReadOnlySpan<UpgradeDef> Upgrades => UpgradeData;
    private static readonly RuleDef[] RuleData =
    {
        new RuleDef(0, "StartingOre", 450),
        new RuleDef(1, "StartingPlasma", 0),
        new RuleDef(2, "OreNodeAmount", 1500),
        new RuleDef(3, "PlasmaNodeAmount", 3000),
        new RuleDef(4, "OrePerTrip", 5),
        new RuleDef(5, "PlasmaPerTrip", 4),
        new RuleDef(6, "GatherTicks", 40),
        new RuleDef(7, "NodeSlots", 2),
        new RuleDef(8, "RepairPerSecond", 8),
        new RuleDef(9, "RegenPerSecond", 2),
        new RuleDef(10, "WorkerRadiusMilli", 250),
    };
    public static ReadOnlySpan<RuleDef> Rules => RuleData;
}
