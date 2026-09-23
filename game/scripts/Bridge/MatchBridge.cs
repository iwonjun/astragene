using Godot;
using System;
using System.Collections.Generic;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;
using RtsGame.Sim.World;

namespace RtsGame.Bridge;

public partial class MatchBridge : Node
{
    public SimWorld World {get;private set;}=null!;
    private VisibilityFilter? _view;
    public VisibilityFilter View=>_view!=null&&_view.Player==LocalPlayer?_view:(_view=World.ViewFor(LocalPlayer));
    private readonly List<Command> _pending=new();
    public int LocalPlayer {get;set;}
    public override void _Ready()
    {
        var bytes=FileAccess.GetFileAsBytes("res://game/maps/duel.map");
        World=new SimWorld(MapLoader.Load(bytes),Array.Empty<SpawnSpec>());
    }
    public override void _PhysicsProcess(double delta)
    {
        if(World==null)return;
        World.Tick(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_pending));_pending.Clear();
    }
    internal void Initialize(SimWorld world) { World=world; _view=null; _pending.Clear(); }
    public void Issue(Command command)=>_pending.Add(command);
    public static Vector3 ToView(Fix2 p)=>new((float)((double)p.X.Raw/Fix64.Scale),0,(float)((double)p.Y.Raw/Fix64.Scale));
    public static Fix2 ToSim(Vector3 p)=>new(Fix64.FromRaw(checked((long)(p.X*Fix64.Scale))),Fix64.FromRaw(checked((long)(p.Z*Fix64.Scale))));
    public Godot.Collections.Array<int> FriendlyIds()
    {
        var result=new Godot.Collections.Array<int>();
        if(World==null)return result;
        for(int i=0;i<View.Capacity;i++)
        {var id=View.IdAt(i);if(id!=EntityId.None && View.Get(id).Owner.Player==LocalPlayer)result.Add(i);}
        return result;
    }
}
