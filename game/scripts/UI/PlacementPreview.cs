using Godot;
using RtsGame.Sim.Data;
using RtsGame.Sim.Entities;
using RtsGame.Sim.Systems;
namespace RtsGame.UI;
public partial class PlacementPreview : Control
{
    private MatchHud _hud=null!;
    public override void _Ready(){_hud=GetNode<MatchHud>("../..");MouseFilter=MouseFilterEnum.Ignore;}
    public override void _Process(double delta)=>QueueRedraw();
    public override void _Draw()
    {
        if(_hud.Bridge==null || _hud.BuildingPreview<0)return;
        var camera=GetViewport().GetCamera3D();if(camera==null)return;var hit=_hud.Bridge.PickGround(camera,GetViewport().GetMousePosition());if(hit is not Vector3 p)return;
        int x=(int)p.X,z=(int)p.Z;var definition=DefDatabase.Buildings[_hud.BuildingPreview];bool valid=true;
        for(int dz=0;dz<definition.Width;dz++)for(int dx=0;dx<definition.Width;dx++)
            if(x+dx>=128 || z+dz>=128 || !_hud.Bridge.World.Map.Grid[x+dx,z+dz].Buildable || _hud.Bridge.View.At(x+dx,z+dz)!=Visibility.Visible)valid=false;
        for(int i=0;i<_hud.Bridge.View.Capacity;i++)
        {
            var id=_hud.Bridge.View.IdAt(i);if(id==EntityId.None || (_hud.Selection.Selected.Count>0 && id==_hud.Selection.Selected[0]))continue;
            var e=_hud.Bridge.View.Get(id);int ex=e.Transform.Position.X.FloorToInt(),ez=e.Transform.Position.Y.FloorToInt();int width=e.Type.IsBuilding?DefDatabase.Buildings[e.Type.Definition].Width:1;
            if(x<ex+width&&x+definition.Width>ex&&z<ez+width&&z+definition.Width>ez)valid=false;
        }
        var points=new Vector2[5];var corners=new[]{new Vector3(x,p.Y+0.06f,z),new Vector3(x+definition.Width,p.Y+0.06f,z),new Vector3(x+definition.Width,p.Y+0.06f,z+definition.Width),new Vector3(x,p.Y+0.06f,z+definition.Width)};
        for(int i=0;i<4;i++)points[i]=GetGlobalTransformWithCanvas().AffineInverse()*camera.UnprojectPosition(corners[i]);points[4]=points[0];
        Color color=valid?new Color(0.15f,1,0.8f):new Color(1,0.2f,0.25f);DrawColoredPolygon(new[]{points[0],points[1],points[2],points[3]},new Color(color.R,color.G,color.B,0.16f));DrawPolyline(points,color,2);
    }
}
