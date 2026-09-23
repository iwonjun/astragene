using Godot;
namespace RtsGame.View;
public partial class VfxPool : Node3D
{
    private const int Capacity=256;
    private readonly Vector3[] _positions=new Vector3[Capacity];
    private readonly Color[] _colors=new Color[Capacity];
    private readonly float[] _life=new float[Capacity];
    private MultiMesh _mesh=null!;
    private int _next;
    private readonly int[] _kind=new int[Capacity];
    public override void _Ready()
    {
        _mesh=new MultiMesh {TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,UseCustomData=true,Mesh=GD.Load<Mesh>("res://game/assets/generated/slash.mesh"),InstanceCount=Capacity,VisibleInstanceCount=0,CustomAabb=new Aabb(Vector3.Zero,new Vector3(128,20,128))};
        AddChild(new MultiMeshInstance3D {Multimesh=_mesh,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
    }
    public void Emit(Vector3 at,Color color,int kind=0){_kind[_next]=kind;_positions[_next]=at;_colors[_next]=color;_life[_next]=0.3f;_next=(_next+1)%Capacity;}
    public override void _Process(double delta)
    {
        int count=0;
        for(int i=0;i<Capacity;i++)if(_life[i]>0){_life[i]-=(float)delta;float t=Mathf.Max(0,_life[i]/0.3f);var c=_colors[i];c.A=t+_kind[i];_mesh.SetInstanceTransform(count,new Transform3D(new Basis(Vector3.Right,-Mathf.Pi/2).Scaled(Vector3.One*(2-t)),_positions[i]));_mesh.SetInstanceCustomData(count++,c);}
        _mesh.VisibleInstanceCount=count;
    }
}
