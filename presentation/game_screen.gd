class_name GameScreen
extends Node
## In-match screen: map + camera + HUD. Every player action becomes an intent sent to the server;
## nothing here changes the simulation directly (AGENTS.md R2).

var app: Node
var client: GameClient
var sim: Sim
var map := MapView.new()
var cam := Camera2D.new()
var hud := CanvasLayer.new()
var chat := ChatPanel.new()
var rules: Ruleset

var mode := "attack"          # attack | build | strike
var mode_kind := -1
var ratio := 300              # permille of home troops per attack
var event_cursor := 0
var hints_on := true

# HUD widgets
var top_label := RichTextLabel.new()
var gauge := ProgressBar.new()
var era_label := Label.new()
var ratio_slider := HSlider.new()
var ratio_label := Label.new()
var mode_label := Label.new()
var board := RichTextLabel.new()
var hint_label := Label.new()
var toasts := VBoxContainer.new()
var proposals_box := VBoxContainer.new()
var perk_box := PanelContainer.new()
var popup := PanelContainer.new()
var over_box := PanelContainer.new()
var help_box: PanelContainer
var confirm_box := PanelContainer.new()
var build_bar := HBoxContainer.new()
var strike_bar := HBoxContainer.new()
var _drag := false
var _drag_from := Vector2.ZERO
var _last_proposals := ""
var _last_perks := ""
var _board_timer := 0.0
var _dm_targets := ""
var _centered := false


func _ready() -> void:
	sim = client.sim
	rules = client.rules
	var world := Node2D.new()
	add_child(world)
	world.add_child(map)
	world.add_child(cam)
	map.setup(sim, client.pid)
	cam.enabled = true
	cam.position = Vector2(sim.width, sim.height) * MapView.TILE * 0.5
	cam.zoom = Vector2(0.55, 0.55)
	add_child(hud)
	_build_hud()
	client.chat_received.connect(_on_chat)
	client.notice.connect(func(t): _toast(t, false))
	client.resynced.connect(_on_resync)
	client.connection_lost.connect(func(): _toast("서버와 연결이 끊어졌습니다. 같은 주소로 다시 접속하면 이어서 할 수 있습니다.", true))
	chat.client = client
	chat.ping_clicked.connect(_jump_tile)
	event_cursor = sim.event_base


# ------------------------------------------------------------------ HUD layout

func _build_hud() -> void:
	var root := Control.new()
	root.theme = UI.theme()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hud.add_child(root)
	# Top bar: resources and era.
	var top := UI.hbox(14)
	var top_panel := UI.panel(top)
	UI.place(top_panel, 0, 0, 12, 10)
	root.add_child(top_panel)
	top_label.bbcode_enabled = true
	top_label.fit_content = true
	top_label.autowrap_mode = TextServer.AUTOWRAP_OFF
	top_label.custom_minimum_size = Vector2(720, 26)
	top_label.scroll_active = false
	top.add_child(top_label)
	var era_col := UI.vbox(2)
	era_label.add_theme_font_size_override("font_size", 14)
	era_col.add_child(era_label)
	gauge.custom_minimum_size = Vector2(220, 10)
	gauge.show_percentage = false
	era_col.add_child(gauge)
	top.add_child(era_col)
	# Leaderboard (right).
	board.bbcode_enabled = true
	board.fit_content = true
	board.custom_minimum_size = Vector2(330, 0)
	board.scroll_active = false
	board.meta_clicked.connect(func(meta): _focus_player(int(str(meta))))
	var board_panel := UI.panel(board)
	UI.place(board_panel, 1, 0, -12, 10, -1, 1)
	root.add_child(board_panel)
	proposals_box.add_theme_constant_override("separation", 6)
	UI.place(proposals_box, 1, 0, -12, 380, -1, 1)
	proposals_box.custom_minimum_size = Vector2(330, 0)
	root.add_child(proposals_box)
	# Toasts (top centre) and contextual hint.
	UI.place(toasts, 0.5, 0, 0, 74, 0, 1)
	toasts.custom_minimum_size = Vector2(600, 0)
	toasts.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(toasts)
	hint_label.add_theme_font_size_override("font_size", 16)
	hint_label.add_theme_color_override("font_color", UI.GOLD)
	hint_label.add_theme_color_override("font_outline_color", Color.BLACK)
	hint_label.add_theme_constant_override("outline_size", 6)
	UI.place(hint_label, 0.5, 1, 0, -118, 0, -1)
	hint_label.custom_minimum_size = Vector2(840, 0)
	hint_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	hint_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	hint_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(hint_label)
	# Bottom action bar: troop ratio, modes, build and weapon menus.
	var bar := UI.vbox(6)
	var bar_panel := UI.panel(bar)
	UI.place(bar_panel, 0.5, 1, 0, -10, 0, -1)
	bar_panel.custom_minimum_size = Vector2(600, 0)
	root.add_child(bar_panel)
	var ratio_row := UI.hbox(10)
	ratio_row.add_child(UI.label("보낼 병력", 15, UI.MUTED))
	ratio_slider.min_value = 5
	ratio_slider.max_value = 100
	ratio_slider.step = 5
	ratio_slider.value = ratio / 10
	ratio_slider.custom_minimum_size = Vector2(280, 0)
	ratio_slider.focus_mode = Control.FOCUS_NONE
	ratio_slider.value_changed.connect(func(v): ratio = int(v) * 10)
	ratio_row.add_child(ratio_slider)
	ratio_label.custom_minimum_size = Vector2(150, 0)
	ratio_row.add_child(ratio_label)
	bar.add_child(ratio_row)
	var modes := UI.hbox(6)
	modes.add_child(UI.button("공격/확장", _set_mode.bind("attack", -1), "좌클릭한 땅(빈 땅 또는 이웃 나라)으로 병력을 보냅니다. 바다 건너는 항구가 있으면 상륙선이 갑니다."))
	modes.add_child(UI.button("건설 (B)", _toggle_build))
	modes.add_child(UI.button("전략 무기", _toggle_strike, "현대 시대부터: 미사일·핵(사일로 필요), 미래 시대: 해킹·궤도 타격"))
	modes.add_child(UI.button("도움말 (F1)", _toggle_help))
	mode_label.add_theme_color_override("font_color", UI.ACCENT)
	modes.add_child(mode_label)
	bar.add_child(modes)
	build_bar.add_theme_constant_override("separation", 6)
	for b in rules.buildings:
		var kind: int = b["id"]
		var btn := UI.button(str(b["name_ko"]), _set_mode.bind("build", kind), "%s — %s" % [b["name_ko"], b["desc_ko"]])
		btn.name = "Build%d" % kind
		build_bar.add_child(btn)
	build_bar.visible = false
	bar.add_child(build_bar)
	for s in rules.strikes:
		var k: int = s["id"]
		var btn2 := UI.button("%s (%s금)" % [s["name_ko"], Fmt.num(s["cost"])], _set_mode.bind("strike", k), "%s 시대부터. 반경 %d" % [rules.eras[s["era"]]["name_ko"], s["radius"]])
		btn2.name = "Strike%d" % k
		strike_bar.add_child(btn2)
	strike_bar.visible = false
	bar.add_child(strike_bar)
	# Chat (bottom left).
	UI.place(chat, 0, 1, 12, -10, 1, -1)
	root.add_child(chat)
	# Modal-ish panels.
	for p in [perk_box, popup, over_box, confirm_box]:
		p.visible = false
		root.add_child(p)
	for c in [perk_box, over_box, confirm_box]:
		UI.place(c, 0.5, 0.45, 0, 0, 0, 0)
	help_box = HelpPanel.make()
	help_box.visible = false
	UI.place(help_box, 0.5, 0.5, 0, 0, 0, 0)
	root.add_child(help_box)
	_set_mode("attack", -1)


func _set_mode(m: String, kind: int) -> void:
	mode = m
	mode_kind = kind
	match m:
		"attack": mode_label.text = "모드: 공격/확장"
		"build": mode_label.text = "모드: %s 짓기 — 내 땅 클릭" % rules.buildings[kind]["name_ko"]
		"strike": mode_label.text = "모드: %s — 목표 클릭" % rules.strikes[kind]["name_ko"]


# ------------------------------------------------------------------ frame

func _process(delta: float) -> void:
	if client.sim != sim:
		_on_resync()
	var me := client.me()
	if me == null:
		return
	if not _centered and me.capital >= 0:
		_centered = true
		_jump_tile(me.capital)
	_camera(delta)
	_events()
	_top(me)
	_board_timer -= delta
	if _board_timer <= 0:
		_board_timer = 0.5
		_leaderboard(me)
		_refresh_dm_targets()
	_proposals(me)
	_perks(me)
	_hint(me)
	_buttons(me)
	if sim.over and not over_box.visible:
		_game_over(me)


func _on_resync() -> void:
	sim = client.sim
	map.rebind(sim)
	event_cursor = sim.event_base + sim.events.size()
	_toast("동기화를 복구했습니다.", false)


func _camera(delta: float) -> void:
	if chat.input.has_focus():
		return
	var dir := Vector2.ZERO
	if Input.is_key_pressed(KEY_A) or Input.is_key_pressed(KEY_LEFT): dir.x -= 1
	if Input.is_key_pressed(KEY_D) or Input.is_key_pressed(KEY_RIGHT): dir.x += 1
	if Input.is_key_pressed(KEY_W) or Input.is_key_pressed(KEY_UP): dir.y -= 1
	if Input.is_key_pressed(KEY_S) or Input.is_key_pressed(KEY_DOWN): dir.y += 1
	var vp := get_viewport()
	var m := vp.get_mouse_position()
	var size := vp.get_visible_rect().size
	if DisplayServer.window_is_focused() and Rect2(Vector2.ZERO, size).has_point(m):
		if m.x < 4: dir.x -= 1
		if m.x > size.x - 4: dir.x += 1
		if m.y < 4: dir.y -= 1
		if m.y > size.y - 4: dir.y += 1
	cam.position += dir * 900.0 * delta / cam.zoom.x
	var limit := Vector2(sim.width, sim.height) * MapView.TILE
	cam.position = cam.position.clamp(Vector2.ZERO, limit)


func _jump_tile(t: int) -> void:
	if t >= 0:
		cam.position = map.tile_center(t)
		map.ping(t)


func _focus_player(pid: int) -> void:
	map.focus_pid = pid
	var p := sim.player(pid)
	if p != null and p.tiles > 0:
		cam.position = map.centroid(p)


func _events() -> void:
	var start := maxi(event_cursor - sim.event_base, 0)
	for i in range(start, sim.events.size()):
		var e: Dictionary = sim.events[i]
		var shown := Fmt.event_text(sim, e, client.pid)
		if shown.size() > 0:
			chat.add_system(shown[0], shown[1] == 2)
			if shown[1] == 2:
				_toast(shown[0], true)
		if e["type"] == "strike":
			map.flash(e["tile"], int(rules.strikes[e["kind"]]["radius"]))
		elif e["type"] == "landing" and e["pid"] == client.pid:
			map.ping(e["tile"], UI.ACCENT)
	event_cursor = sim.event_base + sim.events.size()


func _top(me: SimPlayer) -> void:
	var share := me.tiles * 1000 / maxi(1, sim.land_total)
	var fill_text := "" if me.cap <= 1 else " (%d%%)" % (me.pop * 100 / me.cap)
	var remaining := sim.duration_ticks - sim.tick
	var traitor := "  [color=#ff6b61][배신자 %s][/color]" % Fmt.clock(me.traitor_until - sim.tick) if me.is_traitor(sim.tick) else ""
	top_label.text = "[b]%s[/b]   병력 [color=#55d9c7]%s[/color] / %s%s   금 [color=#f2cc59]%s[/color] (+%s/초)   영토 %d.%d%%   남은 시간 %s%s" % [
		me.name.replace("[", "[lb]"), Fmt.num(me.pop), Fmt.num(me.cap), fill_text, Fmt.num(me.gold), Fmt.num(me.income_milli * 10 / 1000),
		share / 10, share % 10, Fmt.clock(remaining), traitor]
	var last := rules.eras.size() - 1
	if me.era >= last:
		era_label.text = "%s 시대 (최종)" % rules.eras[me.era]["name_ko"]
		gauge.value = 100
	else:
		var need: int = rules.eras[me.era]["gauge_to_next"]
		era_label.text = "%s 시대 → %s  %d%%" % [rules.eras[me.era]["name_ko"], rules.eras[me.era + 1]["name_ko"], me.gauge * 100 / maxi(1, need)]
		gauge.value = me.gauge * 100.0 / maxi(1, need)
	ratio_label.text = "%d%% = %s명" % [ratio / 10, Fmt.num(me.pop * ratio / 1000)]


func _relation(me: int, pid: int) -> String:
	if pid == me:
		return "[color=#ffe082](나)[/color]"
	var tags := []
	for t in sim.treaties:
		if Diplomacy.between(t, me, pid):
			tags.append(str(rules.treaties[t["type"]]["name_ko"]))
	var p: SimPlayer = sim.players[pid]
	if p.is_traitor(sim.tick):
		tags.append("[color=#ff6b61]배신자[/color]")
	return " ".join(tags)


func _leaderboard(me: SimPlayer) -> void:
	var list: Array = sim.alive_players()
	list.sort_custom(func(a, b): return a.tiles > b.tiles or (a.tiles == b.tiles and a.pid < b.pid))
	var out := "[b]순위[/b]  (클릭: 위치 보기)\n"
	var rank_me := 0
	for i in list.size():
		var p: SimPlayer = list[i]
		if p.pid == me.pid:
			rank_me = i + 1
		if i >= 10 and p.pid != me.pid:
			continue
		var col := Palette.color(p.pid, p.kind).to_html(false)
		var kind: String = ["", " [color=#888]봇[/color]", " [color=#9ab]AI[/color]"][p.kind]
		out += "%d. [url=%d][color=#%s]%s[/color][/url]%s  %d.%d%%  %s  %s %s\n" % [i + 1, p.pid, col, p.name.replace("[", "[lb]"), kind,
			p.tiles * 1000 / maxi(1, sim.land_total) / 10, p.tiles * 1000 / maxi(1, sim.land_total) % 10, Fmt.num(p.pop), rules.eras[p.era]["name_ko"], _relation(me.pid, p.pid)]
	if not me.alive:
		out += "\n[color=#ff6b61]멸망했습니다. 관전 채널에서 대화할 수 있습니다.[/color]"
	else:
		out += "\n내 순위: %d / %d" % [rank_me, list.size()]
	board.text = ""
	board.append_text(out)


func _refresh_dm_targets() -> void:
	var others := []
	var key := ""
	for p in sim.players:
		if p.pid != 0 and p.pid != client.pid and p.kind != SimPlayer.BOT and p.alive:
			others.append([p.pid, p.name])
			key += str(p.pid) + ","
	if key != _dm_targets:
		_dm_targets = key
		chat.set_channels(others)


func _proposals(me: SimPlayer) -> void:
	var key := ""
	for pr in sim.proposals:
		if pr["to"] == me.pid:
			key += str(pr["id"]) + ","
	if key == _last_proposals:
		return
	_last_proposals = key
	for c in proposals_box.get_children():
		c.queue_free()
	for pr in sim.proposals:
		if pr["to"] != me.pid:
			continue
		var from: SimPlayer = sim.players[pr["from"]]
		var v := UI.vbox(4)
		var head := UI.label("%s의 제안: %s" % [from.name, rules.treaties[pr["type"]]["name_ko"]], 16, UI.GOLD)
		head.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		v.add_child(head)
		var desc := UI.label(str(rules.treaties[pr["type"]]["desc_ko"]) + ("  (상대는 배신자!)" if from.is_traitor(sim.tick) else ""), 13, UI.MUTED)
		desc.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		v.add_child(desc)
		var row := UI.hbox()
		var id: int = pr["id"]
		row.add_child(UI.button("수락", _respond.bind(id, true)))
		row.add_child(UI.button("거절", _respond.bind(id, false)))
		row.add_child(UI.button("대화", _dm.bind(from.pid)))
		v.add_child(row)
		proposals_box.add_child(UI.panel(v))


func _perks(me: SimPlayer) -> void:
	var key := str(me.perk_options)
	if key == _last_perks:
		if perk_box.visible and me.perk_options.size() > 0:
			var title: Label = perk_box.get_node_or_null("V/Title")
			if title != null:
				title.text = "%s 시대! 특성을 고르세요 (%s)" % [rules.eras[me.era]["name_ko"], Fmt.clock(me.perk_deadline - sim.tick)]
		return
	_last_perks = key
	for c in perk_box.get_children():
		c.queue_free()
	perk_box.visible = me.perk_options.size() > 0
	if not perk_box.visible:
		return
	var v := UI.vbox(10)
	v.name = "V"
	var title := UI.label("", 22, UI.GOLD)
	title.name = "Title"
	v.add_child(title)
	v.add_child(UI.label(str(rules.eras[me.era]["units_ko"]), 14, UI.MUTED))
	var row := UI.hbox(10)
	for i in me.perk_options.size():
		var perk: Dictionary = rules.perks[me.perk_options[i]]
		var choice := i
		var b := UI.button("%s\n%s" % [perk["name_ko"], perk["desc_ko"]], _choose_perk.bind(choice))
		b.custom_minimum_size = Vector2(170, 70)
		row.add_child(b)
	v.add_child(row)
	perk_box.add_child(v)


func _hint(me: SimPlayer) -> void:
	if not hints_on:
		hint_label.text = ""
		return
	var text := ""
	if sim.in_spawn_phase() and not me.spawned:
		text = "지도에서 시작할 땅을 클릭하세요!  (%d초 남음 · 다른 나라와 너무 가까우면 안 됩니다)" % ((int(rules.section("spawn")["phase_ticks"]) - sim.tick + 9) / 10)
	elif not me.alive:
		text = "멸망했습니다. 계속 관전하거나 메뉴로 나갈 수 있습니다."
	elif sim.tick % 50 == 0 or _hint_cache == "":
		var info := sim.border_info(me.pid)
		var attacking := false
		for a in sim.attacks:
			attacking = attacking or a.attacker == me.pid
		if info["wild"] and not attacking:
			text = "색이 없는 빈 땅을 좌클릭해 영토를 넓히세요. 지금 비율이면 병력 %s을 보냅니다. (Q/E로 조절)" % Fmt.num(me.pop * ratio / 1000)
		elif me.gold >= Economy.building_cost(sim, me.pid, Economy.CITY) and sim.buildings_of(me.pid, Economy.CITY) == 0:
			text = "금이 모였습니다. B → 도시를 내 땅에 지으면 병력 상한이 올라갑니다."
		elif not info["wild"] and info["neighbors"].size() > 0 and sim.treaties.filter(func(t): return t["a"] == me.pid or t["b"] == me.pid).is_empty():
			text = "빈 땅이 없습니다. 이웃을 우클릭해 조약을 맺거나, 약한 이웃을 좌클릭해 공격하세요."
		elif me.era >= int(rules.buildings[Economy.PORT]["era"]) and sim.buildings_of(me.pid, Economy.PORT) == 0 and not info["wild"]:
			text = "중세부터 해안에 항구를 지으면 바다 건너 땅을 클릭해 상륙할 수 있습니다."
		else:
			text = ""
		_hint_cache = text
	else:
		text = _hint_cache
	hint_label.text = text

var _hint_cache := ""


func _buttons(me: SimPlayer) -> void:
	if build_bar.visible:
		for b in rules.buildings:
			var btn: Button = build_bar.get_node("Build%d" % b["id"])
			var cost := Economy.building_cost(sim, me.pid, b["id"])
			var locked: bool = me.era < int(b["era"])
			btn.text = "%s %s" % [b["name_ko"], "(%s 시대)" % rules.eras[b["era"]]["name_ko"] if locked else "%s금" % Fmt.num(cost)]
			btn.disabled = locked or me.gold < cost
	if strike_bar.visible:
		for s in rules.strikes:
			var btn2: Button = strike_bar.get_node("Strike%d" % s["id"])
			btn2.disabled = me.era < int(s["era"]) or me.gold < int(s["cost"])


func _toast(text: String, alert: bool) -> void:
	var l := UI.label(text, 16, UI.DANGER if alert else Color(0.9, 0.95, 1))
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	var p := UI.panel(l)
	p.mouse_filter = Control.MOUSE_FILTER_IGNORE
	toasts.add_child(p)
	while toasts.get_child_count() > 4:
		toasts.get_child(0).queue_free()
	get_tree().create_timer(5.0).timeout.connect(_free_later.bind(p))


func _on_chat(msg: Dictionary) -> void:
	chat.add_message(msg)
	var ping := int(msg.get("ping", -1))
	if ping >= 0:
		map.ping(ping, Palette.color(int(msg["from"]), sim.players[int(msg["from"])].kind) if int(msg["from"]) < sim.players.size() else UI.GOLD)


# ------------------------------------------------------------------ input

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		_key(event)
		return
	if event is InputEventMouseButton:
		var mb: InputEventMouseButton = event
		if mb.button_index == MOUSE_BUTTON_WHEEL_UP and mb.pressed:
			_zoom(1.12, mb.position)
		elif mb.button_index == MOUSE_BUTTON_WHEEL_DOWN and mb.pressed:
			_zoom(1.0 / 1.12, mb.position)
		elif mb.button_index == MOUSE_BUTTON_MIDDLE:
			_drag = mb.pressed
			_drag_from = mb.position
		elif mb.pressed and mb.button_index == MOUSE_BUTTON_LEFT:
			popup.visible = false
			var t := map.tile_at(map.get_global_mouse_position())
			if t >= 0:
				if mb.alt_pressed:
					chat.send_ping(t)
				else:
					_click(t)
		elif mb.pressed and mb.button_index == MOUSE_BUTTON_RIGHT:
			var t2 := map.tile_at(map.get_global_mouse_position())
			if mode != "attack":
				_set_mode("attack", -1)
			elif t2 >= 0:
				_open_popup(t2, mb.position)
	elif event is InputEventMouseMotion and _drag:
		var mm: InputEventMouseMotion = event
		cam.position -= (mm.position - _drag_from) / cam.zoom.x
		_drag_from = mm.position


func _zoom(factor: float, at: Vector2) -> void:
	var before := map.get_global_mouse_position()
	cam.zoom = (cam.zoom * factor).clamp(Vector2(0.25, 0.25), Vector2(5, 5))
	await get_tree().process_frame
	cam.position += before - map.get_global_mouse_position()


func _key(k: InputEventKey) -> void:
	if chat.input.has_focus():
		if k.keycode == KEY_ESCAPE:
			chat.input.release_focus()
		return
	match k.keycode:
		KEY_ENTER, KEY_KP_ENTER: chat.input.grab_focus()
		KEY_Q: ratio_slider.value -= 10
		KEY_E: ratio_slider.value += 10
		KEY_B: build_bar.visible = not build_bar.visible
		KEY_F1: help_box.visible = not help_box.visible
		KEY_H: hints_on = not hints_on
		KEY_SPACE:
			var me := client.me()
			if me != null and me.capital >= 0:
				cam.position = map.tile_center(me.capital)
		KEY_ESCAPE:
			_set_mode("attack", -1)
			popup.visible = false
			help_box.visible = false
			build_bar.visible = false
			strike_bar.visible = false
			confirm_box.visible = false
		KEY_1, KEY_2, KEY_3, KEY_4, KEY_5, KEY_6:
			if build_bar.visible:
				_set_mode("build", k.keycode - KEY_1)


func _click(t: int) -> void:
	var me := client.me()
	if me == null:
		return
	if sim.in_spawn_phase() and not me.spawned:
		if sim.spawn_ok(t, me.pid):
			client.send_intent({"type": "spawn", "tile": t})
		else:
			_toast("여기는 시작할 수 없습니다 (바다이거나 다른 나라와 너무 가까움).", false)
		return
	if not me.alive:
		return
	match mode:
		"build":
			if sim.owner[t] != me.pid:
				_toast("건물은 내 땅에만 지을 수 있습니다.", false)
			elif not Economy.can_build(sim, me.pid, mode_kind, t):
				_toast("지을 수 없습니다 (금·시대·해안 조건 또는 이미 건물이 있음).", false)
			else:
				client.send_intent({"type": "build", "kind": mode_kind, "tile": t})
		"strike":
			var s: Dictionary = rules.strikes[mode_kind]
			if mode_kind == Strikes.NUKE:
				_confirm("핵을 발사하면 모든 AI 국가의 신뢰를 잃고, 땅이 황무지가 됩니다. 발사할까요?", func(): client.send_intent({"type": "strike", "kind": mode_kind, "tile": t}))
			else:
				client.send_intent({"type": "strike", "kind": mode_kind, "tile": t})
			if me.gold < int(s["cost"]) * 2:
				_set_mode("attack", -1)
		_:
			_attack_tile(me, t)


func _attack_tile(me: SimPlayer, t: int) -> void:
	var o: int = sim.owner[t]
	if not sim.passable(t) or o == me.pid:
		return
	var info := sim.border_info(me.pid)
	var by_land: bool = (o == 0 and info["wild"]) or (o != 0 and info["neighbors"].has(o))
	var send := func():
		if by_land:
			client.send_intent({"type": "attack", "target": o, "ratio": ratio})
		else:
			var port := _nearest_port(me, t)
			var beach := _beach(t)
			if port < 0 or beach < 0:
				_toast("국경이 맞닿아 있지 않습니다. (중세부터 해안에 항구를 지으면 상륙선을 보낼 수 있어요)", false)
				return
			client.send_intent({"type": "boat", "from": port, "tile": beach, "ratio": ratio})
			_toast("상륙선을 출항시켰습니다.", false)
	if o != 0 and Diplomacy.blocks_attack(sim, me.pid, o):
		_confirm("%s과(와) 조약 중입니다. 공격하면 모든 조약이 깨지고 60초 동안 배신자가 됩니다. 공격할까요?" % sim.players[o].name, send)
	else:
		send.call()


func _nearest_port(me: SimPlayer, t: int) -> int:
	if me.era < int(rules.buildings[Economy.PORT]["era"]):
		return -1
	var best := -1
	var best_d := 1 << 30
	for b in sim.buildings:
		if b["alive"] and b["type"] == Economy.PORT and sim.owner[b["tile"]] == me.pid:
			var dx: int = b["tile"] % sim.width - t % sim.width
			var dy: int = b["tile"] / sim.width - t / sim.width
			if dx * dx + dy * dy < best_d:
				best_d = dx * dx + dy * dy
				best = b["tile"]
	return best


## A coastal tile of the clicked owner near t (the clicked tile itself if it is a beach).
func _beach(t: int) -> int:
	var o: int = sim.owner[t]
	if sim.coastal(t):
		return t
	var cx := t % sim.width
	var cy := t / sim.width
	for r in range(1, 12):
		for dy in range(-r, r + 1):
			for dx in range(-r, r + 1):
				var x := cx + dx
				var y := cy + dy
				if x < 0 or y < 0 or x >= sim.width or y >= sim.height:
					continue
				var c := sim.idx(x, y)
				if sim.owner[c] == o and sim.passable(c) and sim.coastal(c):
					return c
	return -1


func _confirm(text: String, action: Callable) -> void:
	for c in confirm_box.get_children():
		c.queue_free()
	var v := UI.vbox(10)
	var l := UI.label(text, 17, UI.DANGER)
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	l.custom_minimum_size = Vector2(460, 0)
	v.add_child(l)
	var row := UI.hbox()
	_confirm_action = action
	row.add_child(UI.button("실행", _confirm_yes))
	row.add_child(UI.button("취소", _confirm_no))
	v.add_child(row)
	confirm_box.add_child(v)
	confirm_box.visible = true


# ------------------------------------------------------------------ player popup (diplomacy)

func _open_popup(t: int, at: Vector2) -> void:
	var o: int = sim.owner[t]
	var me := client.me()
	for c in popup.get_children():
		c.queue_free()
	if o == 0 or me == null:
		popup.visible = false
		return
	var p: SimPlayer = sim.players[o]
	var v := UI.vbox(6)
	var kind_name: String = ["플레이어", "봇", "AI 국가 · " + str(rules.ai_profiles[clampi(p.profile, 0, 3)]["name_ko"])][p.kind]
	v.add_child(UI.label("%s  (%s)" % [p.name, kind_name], 19, Palette.color(p.pid, p.kind).lightened(0.2)))
	v.add_child(UI.label("영토 %d.%d%%  병력 %s  %s 시대  %s" % [p.tiles * 1000 / sim.land_total / 10, p.tiles * 1000 / sim.land_total % 10, Fmt.num(p.pop), rules.eras[p.era]["name_ko"], _relation(me.pid, p.pid).replace("[color=#ff6b61]", "").replace("[/color]", "")], 14, UI.MUTED))
	if o == me.pid:
		var attacks := UI.vbox(4)
		for a in sim.attacks:
			if a.attacker == me.pid:
				var target: int = a.target
				var name: String = "빈 땅" if target == 0 else sim.players[target].name
				attacks.add_child(UI.button("퇴각: %s 공격 (병력 %s)" % [name, Fmt.num(a.troops)], _popup_intent.bind({"type": "retreat", "target": target})))
		if attacks.get_child_count() == 0:
			attacks.add_child(UI.label("진행 중인 공격이 없습니다.", 14, UI.MUTED))
		v.add_child(attacks)
	else:
		var grid := GridContainer.new()
		grid.columns = 2
		if p.kind != SimPlayer.BOT:
			for tr in rules.treaties:
				var type: int = tr["id"]
				if Diplomacy.has(sim, me.pid, o, type):
					continue
				var label := "%s 제안" % tr["name_ko"]
				if type == Diplomacy.TRIBUTE:
					label = "조공 바치기 제안"
				grid.add_child(UI.button(label, _propose.bind(o, type), str(tr["desc_ko"])))
		for t2 in sim.treaties:
			if Diplomacy.between(t2, me.pid, o) and t2["type"] != Diplomacy.CEASEFIRE:
				var id: int = t2["id"]
				var nm := str(rules.treaties[t2["type"]]["name_ko"])
				var txt := "%s 파기 예고" % nm if t2["type"] == Diplomacy.NAP else "%s 끝내기" % nm
				if t2["type"] == Diplomacy.NAP and t2["notice_end"] > 0:
					continue
				grid.add_child(UI.button(txt, _popup_intent.bind({"type": "break", "id": id})))
		v.add_child(grid)
		if Diplomacy.allied(sim, me.pid, o):
			var row := UI.hbox()
			row.add_child(UI.button("병력 10% 보내기", _popup_intent.bind({"type": "donate", "to": o, "troops": me.pop / 10})))
			row.add_child(UI.button("금 %s 보내기" % Fmt.num(me.gold / 4), _popup_intent.bind({"type": "donate", "to": o, "gold": me.gold / 4})))
			v.add_child(row)
		var row2 := UI.hbox()
		row2.add_child(UI.button("공격 (%d%%)" % (ratio / 10), _popup_attack.bind(t)))
		if p.kind != SimPlayer.BOT:
			row2.add_child(UI.button("대화", _dm.bind(o)))
			var muted: bool = chat.muted.has(o)
			row2.add_child(UI.button("음소거 해제" if muted else "음소거", _toggle_mute.bind(o)))
		v.add_child(row2)
	popup.add_child(v)
	var size := get_viewport().get_visible_rect().size
	popup.position = Vector2(minf(at.x, size.x - 420), minf(at.y, size.y - 320))
	popup.visible = true
	map.focus_pid = o


# ------------------------------------------------------------------ game over

func _game_over(me: SimPlayer) -> void:
	for c in over_box.get_children():
		c.queue_free()
	var v := UI.vbox(10)
	var won := sim.winners.has(me.pid)
	v.add_child(UI.label("승리!" if won else "경기 종료", 40, UI.GOLD if won else UI.MUTED))
	var names := []
	for w in sim.winners:
		names.append(sim.players[w].name)
	v.add_child(UI.label("승리: " + ", ".join(names), 18))
	var list: Array = sim.players.slice(1)
	list.sort_custom(func(a, b): return a.tiles > b.tiles or (a.tiles == b.tiles and a.eliminated_tick > b.eliminated_tick))
	for i in mini(8, list.size()):
		var p: SimPlayer = list[i]
		v.add_child(UI.label("%d. %s — 영토 %d%%, %s 시대%s" % [i + 1, p.name, p.tiles * 100 / maxi(1, sim.land_total), rules.eras[p.era]["name_ko"], "" if p.alive else " (멸망)"], 15, UI.ACCENT if p.pid == me.pid else Color.WHITE))
	var row := UI.hbox()
	row.add_child(UI.button("계속 보기", _close_over))
	row.add_child(UI.button("메뉴로", _to_menu))
	v.add_child(row)
	over_box.add_child(v)
	over_box.visible = true


# ------------------------------------------------------------------ button callbacks

func _toggle_build() -> void:
	build_bar.visible = not build_bar.visible
	strike_bar.visible = false


func _toggle_strike() -> void:
	strike_bar.visible = not strike_bar.visible
	build_bar.visible = false


func _toggle_help() -> void:
	help_box.visible = not help_box.visible


func _respond(id: int, accept: bool) -> void:
	client.send_intent({"type": "respond", "id": id, "accept": accept})


func _dm(pid: int) -> void:
	popup.visible = false
	chat.select_dm(pid)


func _choose_perk(choice: int) -> void:
	client.send_intent({"type": "perk", "choice": choice})
	perk_box.visible = false


func _free_later(node: Node) -> void:
	if is_instance_valid(node):
		node.queue_free()


var _confirm_action := Callable()
func _confirm_yes() -> void:
	confirm_box.visible = false
	if _confirm_action.is_valid():
		_confirm_action.call()


func _confirm_no() -> void:
	confirm_box.visible = false


func _popup_intent(intent: Dictionary) -> void:
	client.send_intent(intent)
	popup.visible = false


func _propose(to: int, type: int) -> void:
	client.send_intent({"type": "propose", "to": to, "treaty": type})
	popup.visible = false
	_toast("%s에게 %s을(를) 제안했습니다." % [sim.players[to].name, rules.treaties[type]["name_ko"]], false)


func _popup_attack(t: int) -> void:
	popup.visible = false
	var me := client.me()
	if me != null:
		_attack_tile(me, t)


func _toggle_mute(pid: int) -> void:
	if chat.muted.has(pid):
		chat.muted.erase(pid)
	else:
		chat.muted[pid] = true
	popup.visible = false


func _close_over() -> void:
	over_box.visible = false


func _to_menu() -> void:
	app.show_menu()
