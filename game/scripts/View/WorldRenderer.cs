using Godot;
using System;
using RtsGame.Bridge;
using RtsGame.Sim.Entities;
using RtsGame.Sim.Data;
using RtsGame.Sim.Systems;

namespace RtsGame.View;

public partial class WorldRenderer : Node3D
{
    private MatchBridge _bridge=null!;
    private SelectionController _selection=null!;
    private readonly MultiMesh[] _batches=new MultiMesh[12];
    private MultiMesh _health=null!,_rings=null!,_shadows=null!,_resources=null!;
    private VfxPool _vfx=null!;
    private readonly int[] _counts=new int[12];
    private Vector3[] _previous=Array.Empty<Vector3>(),_current=Array.Empty<Vector3>();
    private float[] _hp=Array.Empty<float>(),_age=Array.Empty<float>(),_flash=Array.Empty<float>(),_rotation=Array.Empty<float>();
    private EntityId[] _ids=Array.Empty<EntityId>();
    private EntitySnapshot[] _snapshots=Array.Empty<EntitySnapshot>();
    private bool[] _visible=Array.Empty<bool>(),_ghost=Array.Empty<bool>();
    private Image _fog=null!;
    private ImageTexture _fogTexture=null!;
    private long _tick=-1;
    private RtsGame.Sim.World.SimWorld? _capturedWorld;
    private float _elapsed;
    public int VisibleEntities {get;private set;}
    public Vector3 LastEventPosition {get;private set;}=new(20,0,20);
    public override void _Ready()
    {
        _bridge=GetNode<MatchBridge>("../Bridge");_selection=GetNode<SelectionController>("../Selection");
        for(int i=0;i<12;i++)_batches[i]=Batch(i<10?$"unit_{i}":$"building_{i-10}",1024);
        _health=Batch("healthbar",1024);_rings=Batch("slash",200);_shadows=Batch("shadow",1024);
        _resources=Batch("resource",128);
        _vfx=new VfxPool();AddChild(_vfx);
        _fog=Image.CreateEmpty(128,128,false,Image.Format.R8);_fog.Fill(Colors.Black);
        _fogTexture=ImageTexture.CreateFromImage(_fog);
        var terrain=new MeshInstance3D {Mesh=GD.Load<Mesh>("res://game/assets/generated/terrain.mesh")};
        var material=(ShaderMaterial)terrain.Mesh.SurfaceGetMaterial(0).Duplicate();material.SetShaderParameter("fog_mask",_fogTexture);
        terrain.MaterialOverride=material;AddChild(terrain);
    }
    private MultiMesh Batch(string name,int capacity)
    {
        var mesh=new MultiMesh {TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,UseCustomData=true,
            Mesh=GD.Load<Mesh>($"res://game/assets/generated/{name}.mesh"),InstanceCount=capacity,VisibleInstanceCount=0,
            CustomAabb=new Aabb(new Vector3(-5,-5,-5),new Vector3(140,25,140))};
        AddChild(new MultiMeshInstance3D {Multimesh=mesh,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});return mesh;
    }
    public static Color Team(int player)=>player switch {0=>new Color(0.12f,0.85f,1),1=>new Color(1,0.18f,0.64f),2=>new Color(0.8f,0.95f,0.1f),_=>new Color(1,0.65f,0.2f)};
    public override void _Process(double delta)
    {
        if(_bridge.World==null)return;
        if(!ReferenceEquals(_capturedWorld,_bridge.World)){_capturedWorld=_bridge.World;_tick=-1;Array.Fill(_ids,EntityId.None);}
        int capacity=_bridge.View.Capacity;
        if(_ids.Length!=capacity){_ids=new EntityId[capacity];Array.Fill(_ids,EntityId.None);_previous=new Vector3[capacity];_current=new Vector3[capacity];_hp=new float[capacity];_age=new float[capacity];_flash=new float[capacity];_rotation=new float[capacity];_snapshots=new EntitySnapshot[capacity];_visible=new bool[capacity];_ghost=new bool[capacity];}
        if(_tick!=_bridge.World.TickNumber){Capture();_elapsed=0;_tick=_bridge.World.TickNumber;}
        _elapsed+=(float)delta;float alpha=Math.Clamp(_elapsed/0.05f,0,1);
        Array.Clear(_counts);int health=0,rings=0,shadows=0;VisibleEntities=0;
        var selected=_selection.Selected;
        for(int i=0;i<capacity;i++)
        {
            if(!_visible[i])continue;
            var e=_snapshots[i];VisibleEntities++;
            _age[i]=Math.Min(1,_age[i]+(float)delta*3);_flash[i]=Math.Max(0,_flash[i]-(float)delta);
            Vector3 p=_previous[i].Lerp(_current[i],_flash[i]>0?0:alpha);
            Vector3 direction=_current[i]-_previous[i];if(direction.LengthSquared()>0.0001f)_rotation[i]=Mathf.LerpAngle(_rotation[i],Mathf.Atan2(-direction.X,-direction.Z),Math.Min(1,(float)delta*15));
            float size=e.Type.IsBuilding?DefDatabase.Buildings[e.Type.Definition].Width/2f:1f;
            var basis=new Basis(Vector3.Up,_rotation[i]).Scaled(Vector3.One*size);
            if(e.Movement.Airborne)p.Y+=1.8f+Mathf.Sin((float)Time.GetTicksMsec()*0.002f+i)*0.08f;
            int bucket=e.Type.IsBuilding?10+(e.Type.Definition>=6?1:0):e.Type.Definition;
            Color team=Team(e.Owner.Player);team.A=_flash[i]>0?-1:_age[i];
            if(_ghost[i]){team.R*=0.2f;team.G*=0.2f;team.B*=0.2f;}
            _batches[bucket].SetInstanceTransform(_counts[bucket],new Transform3D(basis,p));_batches[bucket].SetInstanceCustomData(_counts[bucket]++,team);
            if(_ghost[i])continue;
            if(!e.Type.IsBuilding || _hp[i]<0.999f)
            {
                _health.SetInstanceTransform(health,new Transform3D(Basis.Identity,p+Vector3.Up*(e.Type.IsBuilding?size*2:1.7f)));
                _health.SetInstanceCustomData(health++,new Color(team.R,team.G,team.B,_hp[i]));
            }
            if(e.Movement.Airborne){_shadows.SetInstanceTransform(shadows,new Transform3D(new Basis(Vector3.Right,-Mathf.Pi/2).Scaled(Vector3.One*1.8f),new Vector3(p.X,_current[i].Y+0.04f,p.Z)));_shadows.SetInstanceCustomData(shadows++,new Color(0,0,0,1));}
            bool isSelected=false;foreach(var id in selected)if(id==e.Id){isSelected=true;break;}
            if(isSelected && rings<200){_rings.SetInstanceTransform(rings,new Transform3D(new Basis(Vector3.Right,-Mathf.Pi/2).Scaled(Vector3.One*(e.Type.IsBuilding?size*2.6f:1.4f)),_current[i]+Vector3.Up*0.04f));_rings.SetInstanceCustomData(rings++,new Color(team.R,team.G,team.B,0.85f));}
        }
        for(int i=0;i<12;i++)_batches[i].VisibleInstanceCount=_counts[i];
        _health.VisibleInstanceCount=health;_rings.VisibleInstanceCount=rings;_shadows.VisibleInstanceCount=shadows;
    }
    private void Capture()
    {
        for(int z=0;z<128;z++)for(int x=0;x<128;x++){float f=_bridge.View.At(x,z) switch {Visibility.Visible=>1,Visibility.Explored=>0.32f,_=>0};_fog.SetPixel(x,z,new Color(f,f,f));}
        _fogTexture.Update(_fog);
        int resourceCount=0;
        for(int z=0;z<128;z++)for(int x=0;x<128;x++)
        {
            var tile=_bridge.World.Map.Grid[x,z];if(tile.ResourceNodeId<0 || _bridge.View.At(x,z)==Visibility.Unexplored)continue;
            var p=new Vector3(x+0.5f,tile.Height*1.5f,z+0.5f);
            _resources.SetInstanceTransform(resourceCount,new Transform3D(Basis.Identity,p));
            _resources.SetInstanceCustomData(resourceCount++,tile.ResourceNodeId%10 is 5 or 9?new Color(0.55f,1,0.25f,1):new Color(0.1f,0.8f,1,1));
        }
        _resources.VisibleInstanceCount=resourceCount;
        for(int i=0;i<_ids.Length;i++)
        {
            var id=_bridge.View.IdAt(i);bool ghost=false;EntitySnapshot e;
            if(id==EntityId.None){if(!_bridge.View.TryGhost(i,out e)){_visible[i]=false;_ids[i]=EntityId.None;continue;}ghost=true;id=e.Id;}else e=_bridge.View.Get(id);
            Vector3 p=_bridge.SurfacePosition(e.Transform.Position);
            bool fresh=_ids[i]!=id;
            _previous[i]=fresh?p:_current[i];_current[i]=p;
            float hp=(float)((double)e.Health.Current.Raw/e.Health.Maximum.Raw);
            if(!fresh && !ghost && hp<_hp[i]){_flash[i]=0.035f;_vfx.Emit(p+Vector3.Up*0.6f,Team(e.Owner.Player));LastEventPosition=p;}
            if(!fresh && !ghost && !e.Type.IsBuilding && e.Combat.Cooldown>_snapshots[i].Combat.Cooldown && _bridge.View.TryGet(e.Combat.Target,out var target))
            {
                var def=DefDatabase.Units[e.Type.Definition];
                if(def.ProjectileSpeed==0)_vfx.Emit((p+_bridge.SurfacePosition(target.Transform.Position))/2+Vector3.Up*0.4f,Team(e.Owner.Player),2);
                else if(def.SplashRadiusMilli>0)_vfx.Emit(_bridge.SurfacePosition(target.Transform.Position)+Vector3.Up*0.1f,Team(e.Owner.Player),3);
            }
            if(fresh)_age[i]=0;
            _hp[i]=hp;_ids[i]=id;_visible[i]=true;_ghost[i]=ghost;_snapshots[i]=e;
        }
    }
}
