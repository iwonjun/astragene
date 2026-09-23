using System;
using System.Collections.Generic;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.Data;
using RtsGame.Sim.Entities;
using RtsGame.Sim.Systems;
using RtsGame.Sim.World;

namespace RtsGame.Sim.Ai;

public enum AiDifficulty { Easy, Medium, Hard }
public readonly record struct AiSeat(int Player, AiDifficulty Difficulty);

/// <summary>Difficulty knobs. No knob grants resources, vision or speed: harder AIs only decide faster and better.</summary>
internal readonly record struct AiProfile(int ThinkInterval, int ReactionTicks, int WorkerTarget, int Barracks, int AttackSupply,
    bool FocusFire, bool Retreat, bool Scout, bool Counter, int UpgradeLevels, int GasWorkers, int TechWorkers)
{
    internal static AiProfile For(AiDifficulty d) => d switch
    {
        AiDifficulty.Easy => new(40, 160, 12, 1, 18, false, false, false, false, 0, 2, 99),
        AiDifficulty.Medium => new(20, 40, 16, 2, 14, true, false, false, false, 1, 3, 15),
        _ => new(10, 10, 19, 3, 12, true, true, true, true, 3, 3, 14),
    };
}

/// <summary>
/// What one AI player perceives this think: built only from its VisibilityFilter and the public map.
/// Lists are filled in entity index order, so every client derives identical decisions.
/// </summary>
internal sealed class AiContext
{
    internal readonly List<EntitySnapshot> Workers = new(), Army = new(), Buildings = new(), Enemies = new(), EnemyBuildings = new();
    internal VisibilityFilter View = null!;
    internal MapData Map = null!;
    internal long Tick;
    internal int Player;
    internal Faction Faction;
    internal PlayerResources Resources;
    internal int Ore, Plasma; // spendable budget during this think
    internal int HomeX = -1, HomeY = -1, EnemyX, EnemyY;
    internal int ArmySupply;
    internal readonly int[] BuildingCount = new int[DefDatabase.Buildings.Length];
    internal readonly int[] CompletedCount = new int[DefDatabase.Buildings.Length];
    internal readonly int[] UnitCount = new int[DefDatabase.Units.Length];
    internal readonly List<Command> Output = new();

    internal int Hq => Faction == Faction.Lumina ? 0 : 6;
    internal int Supply => Faction == Faction.Lumina ? 1 : 7;
    internal int Barracks => Faction == Faction.Lumina ? 2 : 8;
    internal int Tech => Faction == Faction.Lumina ? 3 : 9;
    internal int Extractor => Faction == Faction.Lumina ? 5 : 11;
    internal int Worker => Faction == Faction.Lumina ? 0 : 5;

    internal void Build(VisibilityFilter view, MapData map, long tick)
    {
        View = view; Map = map; Tick = tick; Player = view.Player;
        Workers.Clear(); Army.Clear(); Buildings.Clear(); Enemies.Clear(); EnemyBuildings.Clear();
        Array.Clear(BuildingCount); Array.Clear(CompletedCount); Array.Clear(UnitCount);
        ArmySupply = 0; HomeX = -1; HomeY = -1; bool factionKnown = false;
        for (int i = 0; i < view.Capacity; i++)
        {
            var id = view.IdAt(i);
            if (id == EntityId.None) { if (view.TryGhost(i, out var ghost) && ghost.Owner.Player >= 0 && ghost.Owner.Player != Player) EnemyBuildings.Add(ghost); continue; }
            var e = view.Get(id);
            if (e.Owner.Player != Player)
            {
                if (e.Owner.Player < 0) continue; // neutralized leavers are not threats
                if (e.Type.IsBuilding) EnemyBuildings.Add(e); else Enemies.Add(e);
                continue;
            }
            if (!factionKnown) { Faction = e.Type.IsBuilding ? DefDatabase.Buildings[e.Type.Definition].Faction : DefDatabase.Units[e.Type.Definition].Faction; factionKnown = true; }
            if (e.Type.IsBuilding)
            {
                Buildings.Add(e); BuildingCount[e.Type.Definition]++;
                if (view.ConstructionTicks(id) == 0) CompletedCount[e.Type.Definition]++;
                if (e.Type.Definition is 0 or 6 && HomeX < 0) { HomeX = e.Transform.Position.X.FloorToInt() + 2; HomeY = e.Transform.Position.Y.FloorToInt() + 2; }
                for (int q = 0; q < 5; q++) { var job = view.ProductionAt(id, q); if (job.Definition >= 0 && !job.Research) UnitCount[job.Definition] += DefDatabase.Units[job.Definition].SpawnCount; }
                continue;
            }
            var def = DefDatabase.Units[e.Type.Definition];
            UnitCount[def.Id]++;
            if (def.Worker) Workers.Add(e); else { Army.Add(e); ArmySupply += def.Supply; }
        }
        if (HomeX < 0 && Buildings.Count > 0) { HomeX = Buildings[0].Transform.Position.X.FloorToInt(); HomeY = Buildings[0].Transform.Position.Y.FloorToInt(); }
        if (HomeX < 0 && Workers.Count > 0) { HomeX = Workers[0].Transform.Position.X.FloorToInt(); HomeY = Workers[0].Transform.Position.Y.FloorToInt(); }
        // Start locations of the symmetric duel map are public knowledge, like any RTS map preview.
        EnemyX = Grid.Size - 1 - HomeX; EnemyY = Grid.Size - 1 - HomeY;
        Resources = view.Resources; Ore = Resources.Ore; Plasma = Resources.Plasma;
        Output.Clear();
    }
    internal bool Afford(int ore, int plasma) => Ore >= ore && Plasma >= plasma;
    internal void Spend(int ore, int plasma) { Ore -= ore; Plasma -= plasma; }
    internal void Issue(CommandType type, EntityId entity, EntityId target, Fix2 position, int definition = -1, bool queued = false)
    { if (Output.Count < CommandPacket.MaxCommands) Output.Add(new Command(type, Player, entity, target, position, definition, queued)); }
    internal static Fix2 Tile(int x, int y) => new(Fix64.FromRatio(x * 2 + 1, 2), Fix64.FromRatio(y * 2 + 1, 2));
    internal static int X(in EntitySnapshot e) => e.Transform.Position.X.FloorToInt();
    internal static int Y(in EntitySnapshot e) => e.Transform.Position.Y.FloorToInt();
    internal static int Distance2(int ax, int ay, int bx, int by) => (ax - bx) * (ax - bx) + (ay - by) * (ay - by);
    internal static bool IsPlasmaNode(int node) => EconomySystem.IsPlasmaNode(node);
    internal UnitState State(in EntitySnapshot e) => View.OwnState(e.Id);
}
