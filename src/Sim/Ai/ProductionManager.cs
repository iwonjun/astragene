using System;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.Data;
using RtsGame.Sim.Entities;
using RtsGame.Sim.Systems;
using RtsGame.Sim.World;

namespace RtsGame.Sim.Ai;

/// <summary>
/// Executes the build order in priority order: workers, supply, production buildings, geyser, tech, army,
/// research. When a higher priority cannot be afforded the budget is held for it (no cheating, no debt).
/// </summary>
internal sealed class ProductionManager
{
    private readonly AiProfile _profile;
    private readonly int[] _badX = new int[32], _badY = new int[32];
    private int _badCount;
    private int _pendingDef = -1, _pendingX, _pendingY;
    private long _pendingTick;
    internal ProductionManager(AiProfile profile) { _profile = profile; }

    internal void Run(AiContext c, StrategyBrain brain, EconomyManager economy)
    {
        VerifyPendingBuild(c);
        bool saving = false;

        // 1. Workers from every completed HQ.
        int workers = c.UnitCount[c.Worker];
        foreach (var hq in c.Buildings)
        {
            if (hq.Type.Definition != c.Hq || c.View.ConstructionTicks(hq.Id) > 0 || workers >= _profile.WorkerTarget || hq.Production.QueueCount >= 2) continue;
            if (TryTrain(c, hq, c.Worker)) workers++;
        }

        // 2. Supply ahead of demand.
        int free = c.Resources.MaxSupply - c.Resources.UsedSupply;
        bool supplyBuilding = c.BuildingCount[c.Supply] > c.CompletedCount[c.Supply];
        if (free < 3 + 2 * c.CompletedCount[c.Barracks] && c.Resources.MaxSupply < 200 && !supplyBuilding)
            saving |= !TryBuild(c, c.Supply, economy);

        // 3. Production buildings, 4. geyser, 5. tech.
        int wantBarracks = workers >= 9 ? 1 : 0;
        if (workers >= 13) wantBarracks = _profile.Barracks;
        if (!saving && c.BuildingCount[c.Barracks] < wantBarracks) saving |= !TryBuild(c, c.Barracks, economy);
        if (!saving && c.BuildingCount[c.Barracks] > 0 && c.BuildingCount[c.Extractor] == 0 && economy.HomeGeyser(c) >= 0) saving |= !TryBuild(c, c.Extractor, economy);
        if (!saving && c.CompletedCount[c.Barracks] > 0 && workers >= _profile.TechWorkers && c.BuildingCount[c.Tech] == 0) saving |= !TryBuild(c, c.Tech, economy);
        if (saving) return;

        // 6. Army from every idle-ish production building, weighted by the brain's composition.
        foreach (var b in c.Buildings)
        {
            int def = b.Type.Definition;
            if ((def != c.Barracks && def != c.Tech) || c.View.ConstructionTicks(b.Id) > 0 || b.Production.QueueCount >= 2) continue;
            int unit = Choose(c, brain, def);
            if (unit >= 0) TryTrain(c, b, unit);
        }

        // 7. Research: cheapest missing level first, within the difficulty cap.
        foreach (var b in c.Buildings)
        {
            if (b.Type.Definition != c.Tech || c.View.ConstructionTicks(b.Id) > 0 || b.Production.QueueCount > 0) continue;
            int bestUpgrade = -1, bestCost = int.MaxValue;
            for (int kind = 0; kind < 3; kind++)
            {
                int level = c.View.UpgradeLevel(kind);
                if (level >= _profile.UpgradeLevels || level >= 3) continue;
                var u = DefDatabase.Upgrades[kind * 3 + level];
                if (u.Ore + u.Plasma < bestCost) { bestCost = u.Ore + u.Plasma; bestUpgrade = u.Id; }
            }
            if (bestUpgrade < 0) continue;
            var up = DefDatabase.Upgrades[bestUpgrade];
            if (!c.Afford(up.Ore, up.Plasma) || c.ArmySupply < 6) continue;
            c.Spend(up.Ore, up.Plasma); c.Issue(CommandType.Research, b.Id, EntityId.None, Fix2.Zero, bestUpgrade);
        }
    }

    private int Choose(AiContext c, StrategyBrain brain, int building)
    {
        int best = -1, bestScore = 0;
        foreach (var d in DefDatabase.Units)
        {
            if (d.Trainer != building || d.Worker || (d.RequiredTech >= 0 && c.CompletedCount[d.RequiredTech] == 0)) continue;
            int weight = brain.Weight(c.Faction, d.Id);
            if (weight <= 0 || (d.Plasma > 0 && c.Resources.Plasma < d.Plasma && c.BuildingCount[c.Extractor] == 0)) continue;
            int score = weight * 1000 / (1 + c.UnitCount[d.Id] * d.Supply);
            if (score > bestScore) { bestScore = score; best = d.Id; }
        }
        return best;
    }

    private static bool TryTrain(AiContext c, in EntitySnapshot building, int unit)
    {
        var d = DefDatabase.Units[unit];
        int supply = d.Supply * d.SpawnCount;
        if (!c.Afford(d.Ore, d.Plasma) || c.Resources.UsedSupply + supply > c.Resources.MaxSupply) return false;
        c.Spend(d.Ore, d.Plasma); c.UnitCount[unit] += d.SpawnCount;
        c.Issue(CommandType.Train, building.Id, EntityId.None, Fix2.Zero, unit);
        return true;
    }

    /// <summary>Returns false when the build is wanted but cannot happen now, so lower priorities keep saving.</summary>
    private bool TryBuild(AiContext c, int building, EconomyManager economy)
    {
        var d = DefDatabase.Buildings[building];
        if (d.RequiredTech >= 0 && c.CompletedCount[d.RequiredTech] == 0) return true; // not unlocked: do not block
        if (_pendingDef == building && c.Tick - _pendingTick < 60) return true;
        if (!c.Afford(d.Ore, d.Plasma)) return false;
        int x, y;
        if (building == c.Extractor)
        {
            int gas = economy.HomeGeyser(c); if (gas < 0) return true;
            x = economy.NodeX(gas); y = economy.NodeY(gas);
            if (IsBad(x, y)) return true;
        }
        else if (!FindSite(c, d.Width, out x, out y)) return true;
        EntityId builder = Builder(c, x, y);
        if (builder == EntityId.None) return false;
        c.Spend(d.Ore, d.Plasma); c.BuildingCount[building]++;
        c.Issue(CommandType.Build, builder, EntityId.None, new Fix2(Fix64.FromInt(x), Fix64.FromInt(y)), building);
        _pendingDef = building; _pendingX = x; _pendingY = y; _pendingTick = c.Tick;
        return true;
    }

    private void VerifyPendingBuild(AiContext c)
    {
        if (_pendingDef < 0 || c.Tick == _pendingTick) return;
        bool placed = false;
        foreach (var b in c.Buildings) if (b.Type.Definition == _pendingDef && AiContext.X(b) == _pendingX && AiContext.Y(b) == _pendingY) placed = true;
        if (!placed && _badCount < _badX.Length) { _badX[_badCount] = _pendingX; _badY[_badCount] = _pendingY; _badCount++; }
        _pendingDef = -1;
    }
    private bool IsBad(int x, int y) { for (int i = 0; i < _badCount; i++) if (_badX[i] == x && _badY[i] == y) return true; return false; }

    private static EntityId Builder(AiContext c, int x, int y)
    {
        EntityId best = EntityId.None; int bestScore = int.MaxValue;
        foreach (var w in c.Workers)
        {
            var state = c.State(w);
            if (state == UnitState.Building) continue;
            int score = AiContext.Distance2(AiContext.X(w), AiContext.Y(w), x, y) + (w.Cargo.Ore + w.Cargo.Plasma > 0 ? 400 : 0) + (state == UnitState.Gathering && w.Cargo.ResourceNode >= 0 && AiContext.IsPlasmaNode(w.Cargo.ResourceNode) ? 2000 : 0);
            if (score < bestScore) { bestScore = score; best = w.Id; }
        }
        return best;
    }

    /// <summary>Spiral search around home for a visible, buildable footprint that keeps mining paths and a walkable margin clear.</summary>
    private bool FindSite(AiContext c, int width, out int sx, out int sy)
    {
        sx = sy = 0;
        int cx = c.HomeX, cy = c.HomeY, towardX = c.EnemyX > cx ? 1 : -1, towardY = c.EnemyY > cy ? 1 : -1;
        for (int r = 4; r <= 16; r++)
        for (int a = -r; a <= r; a++)
        for (int side = 0; side < 4; side++)
        {
            // Ring order is fixed and biased toward the enemy side so buildings wall the approach, not the minerals.
            int dx = side switch { 0 => a, 1 => r, 2 => -a, _ => -r } * towardX;
            int dy = side switch { 0 => r, 1 => a, 2 => -r, _ => -a } * towardY;
            int x = cx + dx - width / 2, y = cy + dy - width / 2;
            if (IsBad(x, y) || !Fits(c, x, y, width)) continue;
            sx = x; sy = y; return true;
        }
        return false;
    }
    private static bool Fits(AiContext c, int x, int y, int width)
    {
        var grid = c.Map.Grid;
        for (int yy = y - 3; yy < y + width + 3; yy++)
        for (int xx = x - 3; xx < x + width + 3; xx++)
        {
            if (!Grid.Contains(xx, yy)) return false;
            var tile = grid[xx, yy];
            if (tile.ResourceNodeId >= 0) return false;
            bool footprint = xx >= x && xx < x + width && yy >= y && yy < y + width;
            bool margin = xx >= x - 1 && xx <= x + width && yy >= y - 1 && yy <= y + width;
            if (footprint && (!tile.Buildable || c.View.At(xx, yy) != Visibility.Visible)) return false;
            if (margin && !tile.Walkable) return false;
        }
        foreach (var b in c.Buildings)
        {
            int bx = AiContext.X(b), by = AiContext.Y(b), bw = DefDatabase.Buildings[b.Type.Definition].Width;
            if (x - 1 < bx + bw && bx < x + width + 1 && y - 1 < by + bw && by < y + width + 1) return false;
        }
        if (Occupied(c.Workers, x, y, width) || Occupied(c.Army, x, y, width) || Occupied(c.Enemies, x, y, width)) return false;
        foreach (var b in c.EnemyBuildings)
        {
            int bx = AiContext.X(b), by = AiContext.Y(b), bw = DefDatabase.Buildings[b.Type.Definition].Width;
            if (x < bx + bw && bx < x + width && y < by + bw && by < y + width) return false;
        }
        return true;
    }

    private static bool Occupied(System.Collections.Generic.List<EntitySnapshot> units, int x, int y, int width)
    {
        foreach (var u in units) { int ux = AiContext.X(u), uy = AiContext.Y(u); if (ux >= x && ux < x + width && uy >= y && uy < y + width) return true; }
        return false;
    }
    internal void Hash(ref WorldHasher h)
    {
        h.AddInt32(_badCount); for (int i = 0; i < _badCount; i++) { h.AddInt32(_badX[i]); h.AddInt32(_badY[i]); }
        h.AddInt32(_pendingDef); h.AddInt32(_pendingX); h.AddInt32(_pendingY); h.AddInt64(_pendingTick);
    }
}
