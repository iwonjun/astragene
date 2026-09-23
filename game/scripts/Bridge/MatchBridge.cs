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
    public override void _Ready()=>ResetMatch();
    public void ResetMatch()
    {
        var bytes=FileAccess.GetFileAsBytes("res://game/maps/duel.map");
        var spawns=new List<SpawnSpec>();
        for(int player=0;player<2;player++)
        {
            int baseCoordinate=player==0?20:107;
            spawns.Add(new SpawnSpec(player,player==0?0:6,true,new Fix2(Fix64.FromInt(baseCoordinate),Fix64.FromInt(baseCoordinate))));
            for(int i=0;i<6;i++)spawns.Add(new SpawnSpec(player,player==0?0:5,false,new Fix2(Fix64.FromInt(baseCoordinate-4+i),Fix64.FromInt(baseCoordinate+4))));
        }
        Initialize(new SimWorld(MapLoader.Load(bytes),spawns.ToArray()));
    }
    public override void _PhysicsProcess(double delta)
    {
        if(World==null)return;
        World.Tick(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_pending));_pending.Clear();
    }
    internal void Initialize(SimWorld world) { World=world; _view=null; _pending.Clear(); }
    public void Issue(Command command)=>_pending.Add(command);
    public Vector3 SurfacePosition(Fix2 position)
    {
        var p=ToView(position);int x=Math.Clamp((int)p.X,0,127),z=Math.Clamp((int)p.Z,0,127);
        p.Y=World.Map.Grid[x,z].Height*1.5f;return p;
    }
    public Vector3? PickGround(Camera3D camera,Vector2 screen)
    {
        Vector3 origin=camera.ProjectRayOrigin(screen),direction=camera.ProjectRayNormal(screen);
        for(int level=2;level>=0;level--){var hit=new Plane(Vector3.Up,level*1.5f).IntersectsRay(origin,direction);if(hit is Vector3 p && p.X>=0 && p.Z>=0 && p.X<128 && p.Z<128 && World.Map.Grid[(int)p.X,(int)p.Z].Height==level)return p;}
        return null;
    }
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
