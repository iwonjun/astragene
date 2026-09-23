using Godot;
using System;
using RtsGame.Bridge;
using RtsGame.Sim.Entities;
using RtsGame.Sim.Systems;
using RtsGame.View;
using RtsGame.Sim.Commands;
namespace RtsGame.UI;
public partial class Minimap : Control
{
    private MatchBridge _bridge=null!;
    private RtsCamera _camera=null!;
    private SelectionController _selection=null!;
    private Image _image=null!;
    private ImageTexture _texture=null!;
    private long _tick=-1;
    private object? _world;
    private bool _drag;
    public Vector3 AlertPosition;
    public double AlertUntil;
    public override void _Ready()
    {
        _bridge=GetNode<MatchBridge>("../../../../Bridge");_camera=GetNode<RtsCamera>("../../../../Camera");_selection=GetNode<SelectionController>("../../../../Selection");
        ClipContents=true;_image=Image.CreateEmpty(128,128,false,Image.Format.Rgb8);_texture=ImageTexture.CreateFromImage(_image);
    }
    public override void _Process(double delta)
    {
        if(_tick!=_bridge.World.TickNumber || !ReferenceEquals(_world,_bridge.World))
        {
            _world=_bridge.World;_tick=_bridge.World.TickNumber;
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                var tile=_bridge.World.Map.Grid[x,y];float f=_bridge.View.At(x,y) switch {Visibility.Visible=>1,Visibility.Explored=>0.4f,_=>0.04f};
                var c=new Color(0.17f+tile.Height*0.065f,0.23f+tile.Height*0.055f,0.30f+tile.Height*0.04f);
                if(tile.ResourceNodeId>=0)c=new Color(0.15f,0.65f,0.8f);_image.SetPixel(x,y,c*f);
            }
            _texture.Update(_image);
        }
        QueueRedraw();
    }
    public override void _Draw()
    {
        if(_texture==null)return;DrawTextureRect(_texture,new Rect2(Vector2.Zero,Size),false);
        for(int i=0;i<_bridge.View.Capacity;i++)
        {
            var id=_bridge.View.IdAt(i);if(id==EntityId.None)continue;var e=_bridge.View.Get(id);var p=MatchBridge.ToView(e.Transform.Position);
            DrawCircle(new Vector2(p.X/128*Size.X,p.Z/128*Size.Y),e.Type.IsBuilding?3:1.7f,WorldRenderer.Team(e.Owner.Player));
        }
        Vector2 viewport=GetViewport().GetVisibleRect().Size;var points=new Vector2[5];var corners=new[]{Vector2.Zero,new Vector2(viewport.X,0),viewport,new Vector2(0,viewport.Y)};
        for(int i=0;i<4;i++){var hit=new Plane(Vector3.Up,0).IntersectsRay(_camera.ProjectRayOrigin(corners[i]),_camera.ProjectRayNormal(corners[i]));var p=hit??_camera.Focus;points[i]=new Vector2(p.X/128*Size.X,p.Z/128*Size.Y);}points[4]=points[0];DrawPolyline(points,new Color(0.85f,0.95f,1,0.85f),1.2f);
        if(Time.GetTicksMsec()<AlertUntil){var p=new Vector2(AlertPosition.X/128*Size.X,AlertPosition.Z/128*Size.Y);DrawArc(p,5+(float)(Time.GetTicksMsec()%600)/70,0,Mathf.Tau,24,new Color(1,0.25f,0.3f),2);}
    }
    public override void _GuiInput(InputEvent input)
    {
        if(input is InputEventMouseButton b)
        {
            if(b.ButtonIndex==MouseButton.Left){_drag=b.Pressed;if(_drag)Jump(b.Position);AcceptEvent();}
            if(b.ButtonIndex==MouseButton.Right && b.Pressed){_selection.IssueSelected(CommandType.Move,Point(b.Position),EntityId.None,Input.IsKeyPressed(Key.Shift));AcceptEvent();}
        }
        if(input is InputEventMouseMotion m && _drag){Jump(m.Position);AcceptEvent();}
    }
    private Vector3 Point(Vector2 p)=>new(Mathf.Clamp(p.X/Size.X*128,0,127.9f),0,Mathf.Clamp(p.Y/Size.Y*128,0,127.9f));
    private void Jump(Vector2 point)=>_camera.Jump(Point(point));
}
