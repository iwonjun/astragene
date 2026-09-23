@tool
extends SceneTree
var content: String
var serial:=2000
func node(name: String,type: String,parent: String,properties: String="") -> void:
 serial+=1
 content+='\n[node name="%s" type="%s" parent="%s" unique_id=%d]\n%s\n' % [name,type,parent,serial,properties]
func rect(x: int,y: int,w: int,h: int) -> String:
 return 'offset_left = %d.0\noffset_top = %d.0\noffset_right = %d.0\noffset_bottom = %d.0\n' % [x,y,x+w,y+h]
func panel(name: String,x: int,y: int,w: int,h: int) -> void:
 node(name,"Control","Root",rect(x,y,w,h))
 node("Glass","ColorRect","Root/"+name,'layout_mode = 1\nanchors_preset = 15\nanchor_right = 1.0\nanchor_bottom = 1.0\nmouse_filter = 2\nmaterial = SubResource("Glass")')
func label(name: String,parent: String,text: String,x: int,y: int,w: int,h: int,font_size:=18) -> void:
 node(name,"Label",parent,rect(x,y,w,h)+'text = "%s"\ntheme_override_font_sizes/font_size = %d\nmouse_filter = 2' % [text,font_size])
func button(name: String,parent: String,text: String,x: int,y: int,w: int,h: int) -> void:
 node(name,"Button",parent,rect(x,y,w,h)+'text = "%s"\nfocus_mode = 0' % text)
func _initialize() -> void:
 content='''[gd_scene format=3]

[ext_resource type="Script" path="res://game/scripts/UI/MatchHud.cs" id="1"]
[ext_resource type="Script" path="res://game/scripts/UI/Minimap.cs" id="2"]
[ext_resource type="Shader" path="res://game/shaders/hologram_ui.gdshader" id="3"]
[ext_resource type="FontFile" path="res://game/assets/fonts/Pretendard-Regular.otf" id="4"]
[ext_resource type="FontFile" path="res://game/assets/fonts/RobotoMono.ttf" id="5"]
[ext_resource type="Script" path="res://game/scripts/UI/Portrait.cs" id="6"]
[ext_resource type="Script" path="res://game/scripts/UI/PlacementPreview.cs" id="7"]

[sub_resource type="ShaderMaterial" id="Glass"]
shader = ExtResource("3")
[sub_resource type="StyleBoxFlat" id="Button"]
bg_color = Color(0.045,0.095,0.145,0.94)
border_width_left = 1
border_width_top = 1
border_width_right = 1
border_width_bottom = 1
border_color = Color(0.15,0.39,0.49,0.75)
corner_radius_top_left = 5
corner_radius_bottom_right = 5
[sub_resource type="StyleBoxFlat" id="Hover"]
bg_color = Color(0.09,0.25,0.31,1)
border_width_left = 2
border_width_bottom = 2
border_color = Color(0.25,0.85,1,1)
[sub_resource type="Theme" id="Theme"]
default_font = ExtResource("4")
default_font_size = 16
Button/styles/normal = SubResource("Button")
Button/styles/hover = SubResource("Hover")
Button/styles/pressed = SubResource("Hover")

[node name="HUD" type="CanvasLayer" unique_id=1999]
layer = 10
script = ExtResource("1")
'''
 node("Copy","BackBufferCopy",".",'copy_mode = 2')
 node("Root","Control",".",'anchors_preset = 15\nanchor_right = 1.0\nanchor_bottom = 1.0\nmouse_filter = 2\ntheme = SubResource("Theme")')
 node("Placement","Control","Root",'mouse_filter = 2
script = ExtResource("7")')
 panel("Top",18,16,1404,58)
 label("Brand","Root/Top","ASTRAGENE  /  LUMINA",20,5,340,44,20)
 label("Resources","Root/Top","ORE 450   PLASMA 0   SUPPLY 6 / 15",370,5,780,44,19)
 node("NumericFont","Label","Root/Top",rect(0,0,0,0)+'visible = false\ntheme_override_fonts/font = ExtResource("5")')
 button("Menu","Root/Top","메뉴  ESC",1265,10,120,38)
 panel("MapPanel",18,634,252,248)
 label("Title","Root/MapPanel","전술 지도",14,8,210,22,14)
 node("Minimap","Control","Root/MapPanel",rect(18,38,214,194)+'script = ExtResource("2")')
 panel("SelectionPanel",286,634,676,248)
 label("Title","Root/SelectionPanel","부대를 선택하세요",20,10,640,32,20)
 node("Portrait","Control","Root/SelectionPanel",rect(20,53,128,155)+'script = ExtResource("6")\nmouse_filter = 2')
 label("Stats","Root/SelectionPanel","드래그 선택 · 우클릭 명령",170,55,480,72)
 node("UnitScroll","ScrollContainer","Root/SelectionPanel",rect(165,50,490,156)+'visible = false')
 node("Units","GridContainer","Root/SelectionPanel/UnitScroll",'columns = 10\nsize_flags_horizontal = 3')
 for i in 5:button("Queue%d" % i,"Root/SelectionPanel","",170+i*88,163,82,47)
 panel("CommandPanel",978,634,444,248)
 label("Title","Root/CommandPanel","작전 명령",18,7,410,24,14)
 for i in 12:button("Slot%d" % i,"Root/CommandPanel","",16+(i%4)*105,39+(i/4)*65,99,59)
 label("Notice","Root","",350,95,740,38,21)
 label("Help","Root","WASD 이동  ·  휠 줌  ·  Ctrl+숫자 그룹  ·  Enter 채팅",350,591,740,28,14)
 label("ChatLog","Root","",24,395,560,150,16)
 node("ChatInput","LineEdit","Root",rect(24,553,560,34)+'visible = false\nmax_length = 160\nplaceholder_text = "전체 메시지 (Tab: 팀/전체)"')
 panel("Pause",510,245,420,340)
 label("Title","Root/Pause","일시 정지",28,23,360,42,28)
 button("Resume","Root/Pause","계속하기",30,93,360,48)
 button("Keys","Root/Pause","단축키 편집",30,153,360,48)
 button("Surrender","Root/Pause","항복",30,213,360,48)
 label("Status","Root/Pause","",30,274,360,48,14)
 panel("KeyEditor",420,178,600,460)
 label("Title","Root/KeyEditor","단축키 설정 · 칸을 눌러 키 입력",24,20,552,38,22)
 for i in 12:button("Key%d" % i,"Root/KeyEditor","",24+(i%4)*138,82+(i/4)*78,130,62)
 button("Save","Root/KeyEditor","저장하고 닫기",24,355,552,55)
 var output:=FileAccess.open("res://game/scenes/hud.tscn",FileAccess.WRITE)
 output.store_string(content);output.close()
 var wave:=AudioStreamWAV.new();wave.format=AudioStreamWAV.FORMAT_16_BITS;wave.mix_rate=22050
 var bytes:=PackedByteArray();bytes.resize(4410*2)
 for i in 4410:bytes.encode_s16(i*2,int(sin(TAU*740*i/22050.0)*5000*(1.0-i/4410.0)))
 wave.data=bytes
 var error:=ResourceSaver.save(wave,"res://game/assets/generated/alert.tres")
 if error!=OK:push_error(error_string(error));quit(1);return
 print("Generated HUD, licensed font references and alert placeholder.")
 quit()
