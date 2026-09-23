using System.Collections.Generic;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.Data;
using RtsGame.Sim.Entities;
using RtsGame.Sim.World;

namespace RtsGame.Net;

/// <summary>
/// Unattended network soak driver. It reads only the player's VisibilityFilter and emits Commands through
/// the lockstep queue, exactly like a human. Its own random stream is local and never enters the simulation.
/// </summary>
public sealed class SoakBot
{
    private readonly int _player;
    private readonly List<EntitySnapshot> _own = new();
    private ulong _state;
    public SoakBot(int player, ulong seed) { _player = player; _state = seed | 1; }
    private int Next(int max) { _state ^= _state << 13; _state ^= _state >> 7; _state ^= _state << 17; return (int)(_state % (ulong)max); }
    public void Think(LockstepSession session)
    {
        var view = session.World.ViewFor(_player);
        _own.Clear();
        for (int i = 0; i < view.Capacity; i++) { var id = view.IdAt(i); if (id != EntityId.None && view.Get(id).Owner.Player == _player) _own.Add(view.Get(id)); }
        if (_own.Count == 0) return;
        for (int n = 0; n < 3; n++)
        {
            var e = _own[Next(_own.Count)];
            if (e.Type.IsBuilding)
            {
                foreach (var unit in DefDatabase.Units)
                    if (unit.Trainer == e.Type.Definition && Next(4) == 0) { session.QueueLocal(new Command(CommandType.Train, _player, e.Id, EntityId.None, Fix2.Zero, unit.Id)); break; }
                continue;
            }
            var unitDef = DefDatabase.Units[e.Type.Definition];
            int enemy = _player == 0 ? 107 : 20;
            Fix2 target = Next(3) == 0 ? new Fix2(Fix64.FromInt(enemy), Fix64.FromInt(enemy)) : new Fix2(Fix64.FromInt(16 + Next(96)), Fix64.FromInt(16 + Next(96)));
            if (unitDef.Worker && Next(2) == 0)
            {
                int node = NearestOre(session.World.Map, e.Transform.Position);
                if (node >= 0) { session.QueueLocal(new Command(CommandType.Gather, _player, e.Id, EntityId.None, target, node)); continue; }
            }
            session.QueueLocal(new Command(Next(2) == 0 ? CommandType.AttackMove : CommandType.Move, _player, e.Id, EntityId.None, target, -1, Next(6) == 0));
        }
    }
    private static int NearestOre(MapData map, Fix2 position)
    {
        int px = position.X.FloorToInt(), py = position.Y.FloorToInt(), best = -1, bestDistance = int.MaxValue;
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
        {
            int id = map.Grid[x, y].ResourceNodeId; if (id < 0 || id % 10 is 5 or 9) continue;
            int d = (x - px) * (x - px) + (y - py) * (y - py); if (d < bestDistance) { bestDistance = d; best = id; }
        }
        return best;
    }
}
