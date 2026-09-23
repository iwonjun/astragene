using System;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;
using RtsGame.Sim.World;

namespace RtsGame.Sim.Ai;

/// <summary>Keeps workers mining: idle workers go to the least crowded home deposit, geysers get their crews.</summary>
internal sealed class EconomyManager
{
    private readonly AiProfile _profile;
    private int[] _nodeX = Array.Empty<int>(), _nodeY = Array.Empty<int>(), _idleReports = Array.Empty<int>();
    private readonly int[] _assigned = new int[64];
    internal EconomyManager(AiProfile profile) { _profile = profile; }

    private void Index(MapData map)
    {
        if (_nodeX.Length > 0) return;
        int count = 0;
        for (int y = 0; y < Grid.Size; y++) for (int x = 0; x < Grid.Size; x++) count = Math.Max(count, map.Grid[x, y].ResourceNodeId + 1);
        _nodeX = new int[count]; _nodeY = new int[count]; _idleReports = new int[count];
        for (int y = 0; y < Grid.Size; y++) for (int x = 0; x < Grid.Size; x++) { int id = map.Grid[x, y].ResourceNodeId; if (id >= 0) { _nodeX[id] = x; _nodeY[id] = y; } }
    }
    internal int NodeCount => _nodeX.Length;
    internal int NodeX(int node) => _nodeX[node];
    internal int NodeY(int node) => _nodeY[node];
    /// <summary>Nearest geyser to home that is not known to be exhausted.</summary>
    internal int HomeGeyser(AiContext c)
    {
        Index(c.Map);
        int best = -1, bestDistance = 18 * 18;
        for (int n = 0; n < _nodeX.Length; n++)
        {
            if (!AiContext.IsPlasmaNode(n)) continue;
            int d = AiContext.Distance2(_nodeX[n], _nodeY[n], c.HomeX, c.HomeY);
            if (d < bestDistance) { bestDistance = d; best = n; }
        }
        return best;
    }

    internal void Run(AiContext c)
    {
        Index(c.Map);
        if (_assigned.Length < _nodeX.Length) return;
        Array.Clear(_assigned);
        foreach (var w in c.Workers)
        {
            if (c.State(w) != UnitState.Gathering || (uint)w.Cargo.ResourceNode >= (uint)_nodeX.Length) continue;
            _assigned[w.Cargo.ResourceNode]++;
            if (w.Cargo.Ore > 0 || w.Cargo.Plasma > 0) _idleReports[w.Cargo.ResourceNode] = 0; // still yielding
        }

        // Geyser crews once our extractor on the home geyser is complete.
        int gas = HomeGeyser(c);
        bool extractor = false;
        if (gas >= 0)
            foreach (var b in c.Buildings)
                if (b.Type.Definition == c.Extractor && AiContext.X(b) == _nodeX[gas] && AiContext.Y(b) == _nodeY[gas] && c.View.ConstructionTicks(b.Id) == 0) extractor = true;
        if (extractor && _assigned[gas] < _profile.GasWorkers && _idleReports[gas] < 6)
        {
            foreach (var w in c.Workers)
            {
                if (_assigned[gas] >= _profile.GasWorkers) break;
                // Only ore miners without cargo move to gas; idle workers are handled below.
                if (c.State(w) != UnitState.Gathering || w.Cargo.ResourceNode == gas || w.Cargo.Ore > 0) continue;
                c.Issue(CommandType.Gather, w.Id, EntityId.None, Fix2.Zero, gas); _assigned[gas]++;
            }
        }

        foreach (var w in c.Workers)
        {
            if (c.State(w) != UnitState.Idle) continue;
            int last = w.Cargo.ResourceNode;
            if ((uint)last < (uint)_nodeX.Length) _idleReports[last]++; // repeated idling marks a deposit exhausted
            int best = -1, bestScore = int.MaxValue;
            for (int n = 0; n < _nodeX.Length; n++)
            {
                if (AiContext.IsPlasmaNode(n) || _idleReports[n] >= 6) continue;
                int d = AiContext.Distance2(_nodeX[n], _nodeY[n], c.HomeX, c.HomeY);
                if (d > 16 * 16 && best >= 0) continue;
                int score = _assigned[n] * 4096 + d; // spread first, then nearest
                if (score < bestScore) { bestScore = score; best = n; }
            }
            if (best < 0) continue;
            c.Issue(CommandType.Gather, w.Id, EntityId.None, Fix2.Zero, best); _assigned[best]++;
        }
    }

    internal void Hash(ref WorldHasher h) { h.AddInt32(_idleReports.Length); foreach (int v in _idleReports) h.AddInt32(v); }
}
