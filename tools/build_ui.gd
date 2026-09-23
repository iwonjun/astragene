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
[ext_resource type="Script" path="res://game/scripts/UI/Tutorial.cs" id="8"]

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
 panel("Tutorial",330,86,780,196)
 node("Accent","ColorRect","Root/Tutorial",rect(0,0,6,196)+'color = Color(0.3,0.9,1,1)
mouse_filter = 2')
 label("Step","Root/Tutorial","",24,10,200,24,14)
 label("Title","Root/Tutorial","",24,32,730,34,24)
 label("Body","Root/Tutorial","",24,70,730,80,17)
 label("Progress","Root/Tutorial","",24,156,420,28,16)
 button("Next","Root/Tutorial","다음",512,150,120,36)
 button("Close","Root/Tutorial","끝내기",642,150,120,36)
 node("Director","Node","Root/Tutorial",'script = ExtResource("8")')
 panel("Pause",510,215,420,400)
 label("Title","Root/Pause","일시 정지",28,23,360,42,28)
 button("Resume","Root/Pause","계속하기",30,93,360,48)
 button("Keys","Root/Pause","단축키 편집",30,153,360,48)
 button("Surrender","Root/Pause","항복",30,213,360,48)
 button("Quit","Root/Pause","로비로 나가기",30,273,360,48)
 label("Status","Root/Pause","",30,334,360,48,14)
 panel("KeyEditor",420,178,600,460)
 label("Title","Root/KeyEditor","단축키 설정 · 칸을 눌러 키 입력",24,20,552,38,22)
 for i in 12:button("Key%d" % i,"Root/KeyEditor","",24+(i%4)*138,82+(i/4)*78,130,62)
 button("Save","Root/KeyEditor","저장하고 닫기",24,355,552,55)
 var output:=FileAccess.open("res://game/scenes/hud.tscn",FileAccess.WRITE)
 output.store_string(content);output.close()
 build_lobby()
 var wave:=AudioStreamWAV.new();wave.format=AudioStreamWAV.FORMAT_16_BITS;wave.mix_rate=22050
 var bytes:=PackedByteArray();bytes.resize(4410*2)
 for i in 4410:bytes.encode_s16(i*2,int(sin(TAU*740*i/22050.0)*5000*(1.0-i/4410.0)))
 wave.data=bytes
 var error:=ResourceSaver.save(wave,"res://game/assets/generated/alert.tres")
 if error!=OK:push_error(error_string(error));quit(1);return
 print("Generated HUD, licensed font references and alert placeholder.")
 quit()

func build_lobby() -> void:
 serial=3000
 content='''[gd_scene format=3]

[ext_resource type="Script" path="res://game/scripts/UI/Lobby.cs" id="1"]
[ext_resource type="Shader" path="res://game/shaders/hologram_ui.gdshader" id="3"]
[ext_resource type="FontFile" path="res://game/assets/fonts/Pretendard-Regular.otf" id="4"]

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
default_font_size = 18
Button/styles/normal = SubResource("Button")
Button/styles/hover = SubResource("Hover")
Button/styles/pressed = SubResource("Hover")
Button/styles/disabled = SubResource("Button")
OptionButton/styles/normal = SubResource("Button")
OptionButton/styles/hover = SubResource("Hover")
LineEdit/styles/normal = SubResource("Button")

[node name="LobbyUI" type="Control" unique_id=2999]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
script = ExtResource("1")
'''
 node("Backdrop","ColorRect",".",'layout_mode = 1
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
color = Color(0.035,0.05,0.085,1)')
 node("Copy","BackBufferCopy",".",'copy_mode = 2')
 node("Root","Control",".",'anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
theme = SubResource("Theme")')
 label("Title","Root","ASTRAGENE",80,54,700,74,58)
 label("Subtitle","Root","LUMINA 연합 × VERGE 군체  ·  결정론적 락스텝 RTS",84,128,900,32,18)
 panel("Menu",80,188,560,660)
 label("SoloTitle","Root/Menu","싱글 플레이",24,12,500,26,15)
 button("Tutorial","Root/Menu","▶ 튜토리얼  ·  처음이라면 여기부터",24,40,512,48)
 button("Local","Root/Menu","AI와 대전",24,98,330,46)
 node("Difficulty","OptionButton","Root/Menu",rect(366,98,170,46)+'focus_mode = 0')
 label("NetTitle","Root/Menu","멀티플레이 (직접 IP)",24,158,500,26,15)
 node("Address","LineEdit","Root/Menu",rect(24,186,330,44)+'text = "127.0.0.1"
placeholder_text = "호스트 IP"')
 node("Port","LineEdit","Root/Menu",rect(366,186,170,44)+'text = "27415"
placeholder_text = "포트"')
 button("Host","Root/Menu","방 만들기 (호스트)",24,240,250,46)
 button("Join","Root/Menu","접속",286,240,250,46)
 label("FactionLabel","Root/Menu","진영",24,298,120,28,15)
 node("Faction","OptionButton","Root/Menu",rect(24,326,250,44)+'focus_mode = 0')
 label("MapLabel","Root/Menu","맵",286,298,120,28,15)
 node("Map","OptionButton","Root/Menu",rect(286,326,250,44)+'focus_mode = 0')
 node("Ready","CheckButton","Root/Menu",rect(24,382,250,44)+'text = "준비 완료"
focus_mode = 0
disabled = true')
 button("Start","Root/Menu","경기 시작",286,382,250,44)
 button("Leave","Root/Menu","방 나가기",24,436,512,40)
 button("Replay","Root/Menu","마지막 리플레이 보기",24,496,250,44)
 button("Settings","Root/Menu","설정",286,496,250,44)
 button("Quit","Root/Menu","종료",24,552,512,44)
 panel("Room",668,188,692,660)
 label("RoomTitle","Root/Room","대기실",24,16,640,34,22)
 label("Players","Root/Room","방을 만들거나 호스트에 접속하세요.",24,64,640,300,20)
 label("Seed","Root/Room","",24,380,640,30,15)
 label("Status","Root/Room","",24,420,640,210,17)
 var output:=FileAccess.open("res://game/scenes/lobby_ui.tscn",FileAccess.WRITE)
 output.store_string(content);output.close()
