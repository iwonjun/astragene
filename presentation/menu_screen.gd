class_name MenuScreen
extends Control
## Title menu over a slowly drifting generated world.

var app: Node
var message := ""
var _map: MapView
var _cam := Camera2D.new()
var _name := LineEdit.new()
var _address := LineEdit.new()
var _difficulty: OptionButton
var _bots: SpinBox
var _nations: SpinBox
var _help: PanelContainer
var _t := 0.0


func _ready() -> void:
	theme = UI.theme()
	set_anchors_preset(Control.PRESET_FULL_RECT)
	# Background world: a real generated map with random territories, purely decorative.
	var world := Node2D.new()
	add_child(world)
	var sim := Sim.create({"seed": randi() & 0xFFFF, "players": _demo_players()})
	for i in 900:
		sim.step([])
	_map = MapView.new()
	world.add_child(_map)
	_map.setup(sim, 0)
	world.add_child(_cam)
	_cam.zoom = Vector2(0.9, 0.9)
	_cam.position = Vector2(sim.width, sim.height) * MapView.TILE * 0.5
	_cam.enabled = true
	var shade := ColorRect.new()
	shade.color = Color(0.02, 0.03, 0.05, 0.55)
	shade.set_anchors_preset(Control.PRESET_FULL_RECT)
	shade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var layer := CanvasLayer.new()
	add_child(layer)
	layer.add_child(shade)
	var root := UI.vbox(14)
	root.custom_minimum_size = Vector2(560, 0)
	UI.place(root, 0.5, 0.5, 0, 0, 0, 0)
	layer.add_child(root)
	root.add_child(UI.label("AGE OF DOMINION", 52, UI.ACCENT))
	root.add_child(UI.label("에이지 오브 도미니언 — 돌도끼 부족에서 우주 문명까지, 말이 곧 무기다", 17, UI.MUTED))
	if message != "":
		root.add_child(UI.label(message, 16, UI.DANGER))
	var box := UI.vbox(10)
	root.add_child(UI.panel(box))
	var name_row := UI.hbox()
	name_row.add_child(UI.label("이름", 16, UI.MUTED))
	_name.text = app.player_name
	_name.max_length = 16
	_name.custom_minimum_size = Vector2(320, 0)
	name_row.add_child(_name)
	box.add_child(name_row)
	box.add_child(UI.label("혼자 하기 — 봇·AI 국가와 한 판 (브라우저에서도 가능)", 15, UI.GOLD))
	var solo_row := UI.hbox()
	var rules := Ruleset.load_default()
	var names := []
	for d in rules.difficulty:
		names.append("AI " + str(d["name_ko"]))
	_difficulty = UI.option(names, 1)
	_bots = UI.spin(0, 60, 20)
	_nations = UI.spin(0, 20, 6)
	solo_row.add_child(_difficulty)
	solo_row.add_child(UI.label("봇", 15, UI.MUTED))
	solo_row.add_child(_bots)
	solo_row.add_child(UI.label("국가", 15, UI.MUTED))
	solo_row.add_child(_nations)
	box.add_child(solo_row)
	box.add_child(UI.button("혼자 시작", _solo))
	box.add_child(HSeparator.new())
	box.add_child(UI.label("친구와 하기 — 한 명이 방을 만들고 나머지는 그 PC의 IP로 접속", 15, UI.GOLD))
	if not UI.is_web():
		box.add_child(UI.button("방 만들기 (내 PC가 호스트)", _host, "포트 %d(ENet)·%d(웹)를 엽니다. 같은 공유기면 바로, 인터넷이면 포트포워딩이나 Tailscale 같은 VPN이 필요합니다." % [Protocol.DEFAULT_PORT, Protocol.DEFAULT_PORT + 1]))
	var join_row := UI.hbox()
	_address.placeholder_text = "호스트 IP (예: 192.168.0.12)"
	_address.custom_minimum_size = Vector2(300, 0)
	join_row.add_child(_address)
	join_row.add_child(UI.button("접속", _join))
	box.add_child(join_row)
	var bottom := UI.hbox()
	bottom.add_child(UI.button("게임 방법", func(): _help.visible = not _help.visible))
	if not UI.is_web():
		bottom.add_child(UI.button("종료", func(): get_tree().quit()))
	root.add_child(bottom)
	_help = HelpPanel.make()
	_help.visible = false
	root.add_child(_help)


static func _demo_players() -> Array:
	var list := []
	for i in 14:
		list.append({"name": "", "kind": SimPlayer.BOT})
	return list


func _process(delta: float) -> void:
	_t += delta
	_cam.position += Vector2(cos(_t * 0.05), sin(_t * 0.04)) * 6 * delta
	_map.sim.step([])


func _solo() -> void:
	app.save_name(_name.text)
	app.start_solo({"bots": int(_bots.value), "nations": int(_nations.value), "difficulty": _difficulty.selected})


func _host() -> void:
	app.save_name(_name.text)
	app.host_game()


func _join() -> void:
	if _address.text.strip_edges() == "":
		_address.placeholder_text = "호스트 IP를 입력하세요!"
		return
	app.save_name(_name.text)
	app.join_game(_address.text)
