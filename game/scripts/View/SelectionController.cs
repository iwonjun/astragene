using Godot;
using System.Collections.Generic;
using RtsGame.Bridge;
using RtsGame.Sim.Entities;
using RtsGame.Sim.Commands;

namespace RtsGame.View;

public partial class SelectionController : Node2D
{
    [Signal] public delegate void CameraJumpRequestedEventHandler(Vector3 position);
    private readonly List<EntityId> _selected=new();
    private readonly List<EntityId>[] _groups=new List<EntityId>[10];
    private MatchBridge _bridge=null!;
    private Vector2 _dragStart;
    private bool _dragging;
    private bool _doubleClick;
    private int _lastGroup=-1;
    private ulong _lastGroupTime;
    public int SelectedCount=>_selected.Count;
    public IReadOnlyList<EntityId> Selected=>_selected;
    public void Prune()=>_selected.RemoveAll(id=>!IsFriendly(id));
    public void SelectOnly(EntityId id){_selected.Clear();if(IsFriendly(id))_selected.Add(id);}
    public override void _Process(double delta) { if(_dragging)QueueRedraw(); }
    public override void _Draw()
    {
        if(_dragging)DrawRect(new Rect2(_dragStart,GetViewport().GetMousePosition()-_dragStart).Abs(),new Color(0.2f,0.9f,1f,0.8f),false,1.5f);
    }
    public void IssueSelected(CommandType type,Vector3 position,EntityId target,bool queued=false)
    {
        foreach(var id in _selected)if(IsFriendly(id))_bridge.Issue(new Command(type,_bridge.LocalPlayer,id,target,MatchBridge.ToSim(position),Queued:queued));
    }
    public override void _Ready()
    {
        _bridge=GetNode<MatchBridge>("../Bridge");
        for(int i=0;i<10;i++)_groups[i]=new List<EntityId>();
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if(_bridge.World==null)return;
        var camera=GetViewport().GetCamera3D();
        if(input is InputEventMouseButton mouse && mouse.ButtonIndex==MouseButton.Left)
        {
            if(mouse.Pressed){_dragStart=mouse.Position;_dragging=true;_doubleClick=mouse.DoubleClick;}
            else if(_dragging && camera!=null)
            {
                _dragging=false;QueueRedraw();
                SelectScreenRect(new Rect2(_dragStart,mouse.Position-_dragStart).Abs(),camera,Input.IsKeyPressed(Key.Ctrl),_doubleClick);
            }
        }
        if(input is InputEventMouseButton right && right.ButtonIndex==MouseButton.Right && right.Pressed && camera!=null)
        {
            var hit=_bridge.PickGround(camera,right.Position);
            if(hit is Vector3 point)foreach(var id in _selected)if(IsFriendly(id))_bridge.IssueContext(id,point,Input.IsKeyPressed(Key.Shift));
        }
        if(input is InputEventKey key && key.Pressed && !key.Echo && key.PhysicalKeycode>=Key.Key0 && key.PhysicalKeycode<=Key.Key9)
        {
            int group=(int)key.PhysicalKeycode-(int)Key.Key0;
            if(key.CtrlPressed){_groups[group].Clear();_groups[group].AddRange(_selected);}
            else
            {
                _selected.Clear();foreach(var id in _groups[group])if(IsFriendly(id))_selected.Add(id);
                ulong now=Time.GetTicksMsec();
                if(group==_lastGroup && now-_lastGroupTime<350 && _selected.Count>0)
                    EmitSignal(SignalName.CameraJumpRequested,MatchBridge.ToView(_bridge.View.Get(_selected[0]).Transform.Position));
                _lastGroup=group;_lastGroupTime=now;
            }
        }
    }
    public void SelectScreenRect(Rect2 rectangle,Camera3D camera,bool toggle,bool sameType)
    {
        if(!toggle)_selected.Clear();
        int type=-1;bool building=false;
        bool click=rectangle.Size.Length()<6;
        var candidates=new List<EntityId>();
        foreach(int i in _bridge.FriendlyIds())
        {
            var id=_bridge.View.IdAt(i);var e=_bridge.View.Get(id);var position=_bridge.EntityPosition(e);
            if(camera.IsPositionBehind(position))continue;
            Vector2 point=camera.UnprojectPosition(position);
            if(click?point.DistanceTo(rectangle.Position)<14:rectangle.HasPoint(point))
            {candidates.Add(id);type=e.Type.Definition;building=e.Type.IsBuilding;if(click)break;}
        }
        if(sameType && type>=0)
        {
            candidates.Clear();
            foreach(int i in _bridge.FriendlyIds())
            {
                var id=_bridge.View.IdAt(i);var e=_bridge.View.Get(id);var p=_bridge.EntityPosition(e);
                if(e.Type.Definition==type && e.Type.IsBuilding==building && !camera.IsPositionBehind(p) && GetViewport().GetVisibleRect().HasPoint(camera.UnprojectPosition(p)))candidates.Add(id);
            }
        }
        foreach(var id in candidates){if(toggle && _selected.Contains(id))_selected.Remove(id);else if(_selected.Count<200 && !_selected.Contains(id))_selected.Add(id);}
    }
    private bool IsFriendly(EntityId id)=>_bridge.View.IsAlive(id) && _bridge.View.Get(id).Owner.Player==_bridge.LocalPlayer;
}
