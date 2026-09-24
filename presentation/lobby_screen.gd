class_name LobbyScreen
extends Control
## Room before the match: players, host settings, addresses to share, lobby chat.

var app: Node
var _players := VBoxContainer.new()
var _status: Label
var _chat := RichTextLabel.new()
var _chat_input := LineEdit.new()
var _settings_box := UI.vbox(8)
var _start: Button
var _bots: SpinBox
var _nations: SpinBox
var _minutes: SpinBox
var _difficulty: OptionButton
var _filter := CheckBox.new()
var _syncing := false


func _ready() -> void:
	theme = UI.theme()
	set_anchors_preset(Control.PRESET_FULL_RECT)
	var bg := ColorRect.new()
	bg.color = Color(0.03, 0.05, 0.08)
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(bg)
	var root := UI.hbox(18)
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.offset_left = 40; root.offset_top = 40; root.offset_right = -40; root.offset_bottom = -40
	add_child(root)
	var left := UI.vbox(12)
	left.custom_minimum_size = Vector2(520, 0)
	root.add_child(left)
	left.add_child(UI.label("대기실", 34, UI.ACCENT))
	_status = UI.label("호스트에 접속하는 중…", 16, UI.MUTED)
	_status.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	left.add_child(_status)
	left.add_child(UI.panel(_players))
	left.add_child(UI.panel(_settings_box))
	var rules := Ruleset.load_default()
	var names := []
	for d in rules.difficulty:
		names.append("AI " + str(d["name_ko"]))
	_bots = UI.spin(0, 60, 20)
	_nations = UI.spin(0, 20, 6)
	_minutes = UI.spin(5, 60, 20, 5)
	_difficulty = UI.option(names, 1)
	_filter.text = "욕설 필터 (공개 방 권장)"
	for row in [["봇 수", _bots], ["AI 국가 수", _nations], ["제한 시간(분)", _minutes], ["AI 난이도", _difficulty]]:
		var h := UI.hbox()
		var l := UI.label(row[0], 15, UI.MUTED)
		l.custom_minimum_size = Vector2(140, 0)
		h.add_child(l)
		h.add_child(row[1])
		_settings_box.add_child(h)
	_settings_box.add_child(_filter)
	for s in [_bots, _nations, _minutes]:
		s.value_changed.connect(func(_v): _push_settings())
	_difficulty.item_selected.connect(func(_i): _push_settings())
	_filter.toggled.connect(func(_b): _push_settings())
	var buttons := UI.hbox()
	_start = UI.button("경기 시작", func(): app.client.send_start())
	_start.visible = false
	buttons.add_child(_start)
	buttons.add_child(UI.button("나가기", func(): app.show_menu()))
	left.add_child(buttons)
	var right := UI.vbox(8)
	right.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	root.add_child(right)
	right.add_child(UI.label("대기실 채팅", 18, UI.GOLD))
	_chat.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_chat.custom_minimum_size = Vector2(420, 360)
	_chat.bbcode_enabled = true
	_chat.scroll_following = true
	right.add_child(UI.panel(_chat))
	_chat_input.placeholder_text = "메시지 입력 후 Enter"
	_chat_input.text_submitted.connect(_send_chat)
	right.add_child(_chat_input)
	var c: GameClient = app.client
	c.lobby_updated.connect(_on_lobby)
	c.welcomed.connect(func(host): _status.text = _address_hint(host))
	c.chat_received.connect(func(m): _chat.append_text("[color=#9fd]%s[/color]: %s\n" % [m["name"], _escape(m["text"])]))
	c.notice.connect(func(t): _chat.append_text("[color=#aaa]%s[/color]\n" % t))
	_settings_box.visible = false


static func _escape(t: String) -> String:
	return t.replace("[", "[lb]")


func _address_hint(host: bool) -> String:
	if not host:
		return "접속했습니다. 호스트가 경기를 시작하면 바로 시작됩니다."
	if app.server == null:
		return "당신이 이 방의 방장입니다. 친구들이 모이면 설정을 고르고 [경기 시작]을 누르세요."
	var ips := []
	for ip in IP.get_local_addresses():
		if ip.count(".") == 3 and not ip.begins_with("127.") and not ip.begins_with("169.254"):
			ips.append(ip)
	var text := "방을 만들었습니다. 친구에게 아래 주소를 알려 주세요.\n게임 접속 주소: %s  (포트 %d)" % [", ".join(ips), Protocol.DEFAULT_PORT]
	if app.server != null and app.server.web.tcp.is_listening() and ips.size() > 0:
		text += "\n설치 없이 브라우저로: http://%s:%d" % [ips[0], Protocol.DEFAULT_PORT + WebHost.PORT_OFFSET]
	return text + "\n인터넷 너머 친구는 공유기 포트포워딩 또는 Tailscale 같은 VPN이 필요합니다."


func _on_lobby(players: Array, settings: Dictionary) -> void:
	for child in _players.get_children():
		child.queue_free()
	_players.add_child(UI.label("참가자 %d명" % players.size(), 17, UI.GOLD))
	for p in players:
		_players.add_child(UI.label(("[호스트] " if p["host"] else "· ") + str(p["name"]) + ("" if p["online"] else " (끊김)"), 16))
	var host: bool = app.client.is_host
	_settings_box.visible = true
	_start.visible = host
	_syncing = true
	_bots.value = settings.get("bots", 20); _nations.value = settings.get("nations", 6)
	_minutes.value = settings.get("minutes", 20); _difficulty.select(int(settings.get("difficulty", 1)))
	_filter.button_pressed = bool(settings.get("filter", false))
	for sp: SpinBox in [_bots, _nations, _minutes]:
		sp.editable = host
	_difficulty.disabled = not host
	_filter.disabled = not host
	_syncing = false


func _send_chat(t: String) -> void:
	if t.strip_edges() != "":
		app.client.send_chat("global", t)
	_chat_input.clear()


func _push_settings() -> void:
	if _syncing or not app.client.is_host:
		return
	app.client.send_setup({"bots": int(_bots.value), "nations": int(_nations.value), "minutes": int(_minutes.value), "difficulty": _difficulty.selected, "filter": _filter.button_pressed})
