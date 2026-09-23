@tool
extends SceneTree
const OUT := "res://game/assets/generated/"
var surface: SurfaceTool

func material(shader_name: String) -> ShaderMaterial:
 var m := ShaderMaterial.new()
 m.shader=load("res://game/shaders/"+shader_name+".gdshader")
 save_checked(m,OUT+shader_name+".tres")
 return m

func part(mesh: PrimitiveMesh, at: Vector3, scale_by: Vector3, color: Color, rotate := Vector3.ZERO) -> void:
 var arrays: Array=mesh.get_mesh_arrays()
 var vertices: PackedVector3Array=arrays[Mesh.ARRAY_VERTEX]
 var normals: PackedVector3Array=arrays[Mesh.ARRAY_NORMAL]
 var indices: PackedInt32Array=arrays[Mesh.ARRAY_INDEX]
 var basis:=Basis.from_euler(rotate).scaled(scale_by)
 for index: int in indices:
  surface.set_color(color)
  surface.set_normal((basis.inverse().transposed()*normals[index]).normalized())
  surface.add_vertex(basis*vertices[index]+at)

func ball() -> SphereMesh:
 var m:=SphereMesh.new()
 m.radial_segments=12;m.rings=6
 return m

func unit(role: int, organic: bool, toon: Material) -> void:
 surface=SurfaceTool.new();surface.begin(Mesh.PRIMITIVE_TRIANGLES)
 var shell:=Color(0.72,0.77,0.85,0) if not organic else Color(0.32,0.17,0.43,0)
 var dark:=Color(0.10,0.12,0.22,0)
 var glow:=Color(0.45,0.75,0.75,1)
 var body: PrimitiveMesh=ball() if organic else BoxMesh.new()
 if organic and role==3:
  part(ball(),Vector3(0,0.3,0),Vector3(0.55,0.3,0.7),shell)
  for x in [-0.4,0.4]:
   part(body,Vector3(x,0.15,0),Vector3(0.3,0.14,0.6),dark,Vector3(0,x,0))
  part(body,Vector3(0,0.4,-0.3),Vector3(0.25,0.12,0.12),glow)
  surface.set_material(toon)
  save_checked(surface.commit(),OUT+"unit_8.mesh")
  return
 match role:
  0:
   part(ball(),Vector3(0,0.55,0),Vector3(0.68,0.6,0.55),shell)
   part(body,Vector3(0,0.62,0.35),Vector3(0.54,0.65,0.3),dark)
   part(body,Vector3(0,0.74,-0.29),Vector3(0.4,0.12,0.1),glow)
   for x in [-0.25,0.25]:part(body,Vector3(x,0.15,0),Vector3(0.2,0.28,0.3),dark)
  1,2:
   part(body,Vector3(0,0.77,0),Vector3(0.4,0.55,0.3),shell)
   part(ball(),Vector3(0,1.25,0),Vector3(0.33,0.32,0.3),shell)
   part(body,Vector3(0,1.27,-0.14),Vector3(0.25,0.10,0.06),glow)
   for x in [-0.30,0.30]:part(body,Vector3(x,0.99,0),Vector3(0.32,0.27,0.34),shell)
   for x in [-0.14,0.14]:part(body,Vector3(x,0.29,0),Vector3(0.16,0.53,0.23),dark)
   part(body,Vector3(0.38,0.72,-0.35),Vector3(0.15,0.17,1.4 if role==2 else 0.55),dark,Vector3(-0.25,0,-0.2))
   part(body,Vector3(0.38,0.9,-0.8),Vector3(0.1,0.1,0.15),glow)
  3:
   part(body,Vector3(0,0.25,0),Vector3(1.15,0.4,0.85),dark)
   part(body,Vector3(0,0.56,0),Vector3(0.85,0.4,0.65),shell)
   part(body,Vector3(0,0.65,-0.65),Vector3(0.25,0.23,1.4),shell)
   part(body,Vector3(0,0.65,-1.37),Vector3(0.20,0.15,0.06),glow)
  4:
   part(body,Vector3.ZERO,Vector3(1.25,0.15,1.25),shell,Vector3(0,PI/4,0))
   part(ball(),Vector3(0,0.16,0),Vector3(0.35,0.25,0.5),glow)
   for x in [-0.8,0.8]:part(body,Vector3(x,0,0),Vector3(0.15,0.12,0.4),glow)
 if organic:
  var spike:=CylinderMesh.new();spike.top_radius=0;spike.bottom_radius=0.5;spike.radial_segments=6
  part(spike,Vector3(-0.32,0.9,0.25),Vector3(0.22,0.45,0.22),glow,Vector3(0,0,0.4))
 surface.set_material(toon)
 save_checked(surface.commit(),OUT+"unit_%d.mesh" % (role+(5 if organic else 0)))

func _initialize() -> void:
 DirAccess.make_dir_recursive_absolute(OUT)
 var outline:=material("outline")
 var toon:=material("toon");toon.next_pass=outline;save_checked(toon,OUT+"toon.tres")
 for faction in 2:
  for role in 5:unit(role,faction==1,toon)
  surface=SurfaceTool.new();surface.begin(Mesh.PRIMITIVE_TRIANGLES)
  var shell:=Color(0.62,0.68,0.79,0) if faction==0 else Color(0.29,0.14,0.38,0)
  var body: PrimitiveMesh=BoxMesh.new() if faction==0 else ball()
  part(body,Vector3(0,0.5,0),Vector3(1.8,1.0,1.8),shell)
  part(body,Vector3(0,1.15,0),Vector3(1.2,0.5,1.2),shell)
  part(ball(),Vector3(0,1.6,0),Vector3(0.5,0.5,0.5),Color(0.5,0.8,0.8,1))
  for x in [-0.75,0.75]:part(body,Vector3(x,1.0,0),Vector3(0.14,1.6,0.14),shell)
  surface.set_material(toon);save_checked(surface.commit(),OUT+"building_%d.mesh" % faction)
 surface=SurfaceTool.new();surface.begin(Mesh.PRIMITIVE_TRIANGLES)
 var crystal:=CylinderMesh.new();crystal.top_radius=0;crystal.radial_segments=5
 for offset: Vector3 in [Vector3(-0.3,0.5,0),Vector3(0,0.8,0.1),Vector3(0.3,0.4,-0.1)]:
  part(crystal,offset,Vector3(0.5,offset.y,0.5),Color(0.18,0.4,0.5,0.8))
 surface.set_material(toon);save_checked(surface.commit(),OUT+"resource.mesh")
 for name: String in ["healthbar","slash","shadow"]:
  var quad:=QuadMesh.new();quad.size=Vector2(1,0.08) if name=="healthbar" else Vector2(1,1);quad.material=material(name)
  var mesh:=ArrayMesh.new();mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,quad.get_mesh_arrays());mesh.surface_set_material(0,quad.material)
  save_checked(mesh,OUT+name+".mesh")
 var environment:=Environment.new()
 environment.background_mode=Environment.BG_COLOR
 environment.background_color=Color(0.025,0.03,0.055)
 environment.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
 environment.ambient_light_color=Color(0.6,0.65,0.85)
 environment.ambient_light_energy=0.7
 environment.tonemap_mode=Environment.TONE_MAPPER_FILMIC
 environment.glow_enabled=true
 environment.glow_hdr_threshold=1.1
 environment.glow_intensity=0.5
 save_checked(environment,OUT+"world_environment.tres")
 var terrain:=material("terrain")
 surface=SurfaceTool.new();surface.begin(Mesh.PRIMITIVE_TRIANGLES)
 var bytes:=FileAccess.get_file_as_bytes("res://game/maps/duel.map")
 for z in 128:
  for x in 128:
   var h:=float(bytes[12+(z*128+x)*6+1])*1.5
   var shade:=0.12+0.035*h
   surface.set_color(Color(shade,shade*1.16,shade*1.4,0))
   face(Vector3(x,h,z),Vector3(x+1,h,z),Vector3(x+1,h,z+1),Vector3(x,h,z+1))
   if x<127:
    var next:=float(bytes[12+(z*128+x+1)*6+1])*1.5
    if next!=h:
     surface.set_color(Color(0.10,0.14,0.23,0))
     face(Vector3(x+1,h,z),Vector3(x+1,next,z),Vector3(x+1,next,z+1),Vector3(x+1,h,z+1))
     surface.set_color(Color(0.15,0.48,0.52,1))
     var top:=maxf(h,next)+0.012
     face(Vector3(x+0.96,top,z),Vector3(x+1.04,top,z),Vector3(x+1.04,top,z+1),Vector3(x+0.96,top,z+1))
   if z<127:
    var next:=float(bytes[12+((z+1)*128+x)*6+1])*1.5
    if next!=h:
     surface.set_color(Color(0.10,0.14,0.23,0))
     face(Vector3(x,h,z+1),Vector3(x+1,h,z+1),Vector3(x+1,next,z+1),Vector3(x,next,z+1))
 surface.generate_normals();surface.set_material(terrain)
 save_checked(surface.commit(),OUT+"terrain.mesh")
 print("Generated units, buildings, instanced effects and three-level terrain.")
 quit()

func face(a: Vector3,b: Vector3,c: Vector3,d: Vector3) -> void:
 for v: Vector3 in [a,c,b,a,d,c]:surface.add_vertex(v)

func save_checked(resource: Resource,path: String) -> void:
 var error:=ResourceSaver.save(resource,path)
 if error!=OK:
  push_error("Asset save failed: %s (%s)" % [path,error_string(error)])
  quit(1)