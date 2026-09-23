using Godot;
using System;
using System.Collections.Generic;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;
using RtsGame.Sim.World;
using RtsGame.Net;
using RtsGame.Sim.Ai;

namespace RtsGame.Bridge;

public partial class MatchBridge : Node
{
    public SimWorld World {get;private set;}=null!;
    private VisibilityFilter? _view;
    public VisibilityFilter View=>_view!=null&&_view.Player==LocalPlayer?_view:(_view=World.ViewFor(LocalPlayer));
    private readonly List<Command> _pending=new();
    public int LocalPlayer {get;set;}
    public bool IsPaused {get;set;}
    public MatchMode Mode {get;private set;}=MatchMode.Local;
    public LockstepRunner? Runner {get;private set;}
    /// <summary>Local matches record every tick, so they can be replayed exactly like network matches.</summary>
    public ReplayLog? LocalReplay {get;private set;}
    public override void _Ready()=>ResetMatch();
    public void ResetMatch()
    {
        var map=MapLoader.Load(FileAccess.GetFileAsBytes(MatchLaunch.MapPath));
        Mode=MatchLaunch.Mode;Runner=GetNodeOrNull<LockstepRunner>("../Net");
        if(Mode==MatchMode.Replay)return; // ReplayController supplies worlds.
        var spawns=MatchSetup.Spawns(MatchLaunch.Factions);
        if(Mode==MatchMode.Network && MatchLaunch.Session is NetworkSession session && Runner!=null)
        {
            LocalPlayer=session.LocalPlayer;var world=new SimWorld(map,spawns,MatchLaunch.Seed);
            Initialize(world);Runner.Begin(world,session);return;
        }
        Mode=MatchMode.Local;LocalPlayer=MatchLaunch.LocalPlayer;
        // Local matches seat the computer opponent inside the simulation, so replays reproduce it exactly.
        var ai=MatchLaunch.AiDifficulty is >=0 and <=2?new[]{new AiSeat(1-LocalPlayer,(AiDifficulty)MatchLaunch.AiDifficulty)}:null;
        Initialize(new SimWorld(map,spawns,MatchLaunch.Seed,ai:ai));
        LocalReplay=new ReplayLog(map,spawns,MatchLaunch.Seed,ai:ai);
    }
    public override void _PhysicsProcess(double delta)
    {
        if(World==null || IsPaused || Mode!=MatchMode.Local)return;
        var commands=System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_pending);
        World.Tick(commands);
        if(LocalReplay!=null && ReferenceEquals(LocalReplay.Map,World.Map) && LocalReplay.TickCount==World.TickNumber-1)LocalReplay.Append(commands,World.Hash());
        _pending.Clear();
    }
    internal void Initialize(SimWorld world) { World=world; _view=null; _pending.Clear(); LocalReplay=null; }
    internal void InitializeReplay(SimWorld world,int viewer) { Mode=MatchMode.Replay; LocalPlayer=viewer; Initialize(world); }
    public void Issue(Command command)
    {
        if(Mode==MatchMode.Replay)return;
        if(Mode==MatchMode.Network){Runner?.QueueLocal(command);return;}
        _pending.Add(command);
    }
    public string SaveLocalReplay()=>LocalReplay==null || LocalReplay.TickCount==0?"":MatchLaunch.SaveReplay(LocalReplay);
    public override void _ExitTree(){if(Mode==MatchMode.Local && !MatchLaunch.Flag("render-benchmark") && !MatchLaunch.Flag("art-capture"))SaveLocalReplay();}
    public EntityId PickEntity(Vector3 point)
    {
        EntityId result=EntityId.None;float best=1.8f;
        for(int i=0;i<View.Capacity;i++){var id=View.IdAt(i);if(id==EntityId.None)continue;var e=View.Get(id);var p=EntityPosition(e);float distance=new Vector2(p.X-point.X,p.Z-point.Z).Length();float radius=e.Type.IsBuilding?RtsGame.Sim.Data.DefDatabase.Buildings[e.Type.Definition].Width*0.7f:0.7f;if(distance<radius && distance<best){best=distance;result=id;}}
        return result;
    }
    public void IssueContext(EntityId id,Vector3 point,bool queued)
    {
        if(!View.TryGet(id,out var source))return;
        int definition=-1;var target=PickEntity(point);CommandType type=source.Type.IsBuilding?CommandType.Rally:CommandType.Move;
        bool worker=!source.Type.IsBuilding && RtsGame.Sim.Data.DefDatabase.Units[source.Type.Definition].Worker;
        if(target!=EntityId.None && !source.Type.IsBuilding)
        {
            var e=View.Get(target);type=e.Owner.Player!=LocalPlayer?CommandType.Attack:worker&&e.Type.IsBuilding&&e.Type.Definition<6?CommandType.Repair:CommandType.Follow;
        }
        else if(worker)
        {
            int x=(int)point.X,z=(int)point.Z;
            if((uint)x<128&&(uint)z<128&&World.Map.Grid[x,z].ResourceNodeId>=0)type=CommandType.Gather;
        }
                if(worker && target!=EntityId.None && View.Get(target).Type.IsBuilding && View.Get(target).Type.Definition is 5 or 11){type=CommandType.Gather;point=SurfacePosition(View.Get(target).Transform.Position);}
        if(type==CommandType.Gather)definition=ResourceNodeAt(point);
        if(type is CommandType.Move or CommandType.Rally or CommandType.Gather)target=EntityId.None;
        Issue(new Command(type,LocalPlayer,id,target,ToSim(point),definition,queued));
    }
    public int ResourceNodeAt(Vector3 point)
    {
        int x=(int)point.X,z=(int)point.Z;return (uint)x<128&&(uint)z<128?World.Map.Grid[x,z].ResourceNodeId:-1;
    }
    public Vector3 EntityPosition(EntitySnapshot e)
    {
        var p=SurfacePosition(e.Transform.Position);
        if(e.Type.IsBuilding){float half=RtsGame.Sim.Data.DefDatabase.Buildings[e.Type.Definition].Width*0.5f;p+=new Vector3(half,0,half);}
        return p;
    }
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
