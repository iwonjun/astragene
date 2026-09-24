class_name ChatPanel
extends PanelContainer
## Channels: 전체 / 동맹 / 관전(탈락자) / 1:1 DM per player. System lines come from simulation events.
## Map pings are clickable links; muted players are hidden locally.

signal ping_clicked(tile: int)

var client: GameClient
var history := RichTextLabel.new()
var input := LineEdit.new()
var channel := OptionButton.new()
var channels: Array = []     # [label, ch, to]
var lines: Array = []        # {ch, to, from, text}
var muted := {}
var pending_ping := -1


func _ready() -> void:
	custom_minimum_size = Vector2(430, 250)
	var v := UI.vbox(4)
	add_child(v)
	var top := UI.hbox(6)
	v.add_child(top)
	channel.focus_mode = Control.FOCUS_NONE
	channel.item_selected.connect(func(_i): _render())
	top.add_child(channel)
	var hint := UI.label("Enter 입력 · Alt+클릭 핑", 12, UI.MUTED)
	top.add_child(hint)
	history.bbcode_enabled = true
	history.scroll_following = true
	history.size_flags_vertical = Control.SIZE_EXPAND_FILL
	history.add_theme_font_size_override("normal_font_size", 14)
	history.meta_clicked.connect(func(meta): ping_clicked.emit(int(str(meta))))
	v.add_child(history)
	input.placeholder_text = "메시지…"
	input.max_length = 200
	input.text_submitted.connect(_submit)
	v.add_child(input)
	set_channels([])


## others: [[pid, name], ...] for DM targets.
func set_channels(others: Array) -> void:
	var current := channel.selected
	channels = [["전체", "global", 0], ["동맹", "ally", 0], ["관전", "dead", 0]]
	for o in others:
		channels.append(["DM " + str(o[1]), "dm", int(o[0])])
	channel.clear()
	for c in channels:
		channel.add_item(c[0])
	channel.select(clampi(current, 0, channels.size() - 1))


func select_dm(pid: int) -> void:
	for i in channels.size():
		if channels[i][1] == "dm" and channels[i][2] == pid:
			channel.select(i)
			_render()
			input.grab_focus()
			return


func _submit(text: String) -> void:
	var c: Array = channels[channel.selected]
	if text.strip_edges() != "" or pending_ping >= 0:
		client.send_chat(c[1], text if text.strip_edges() != "" else "여기!", c[2], pending_ping)
	pending_ping = -1
	input.clear()
	input.release_focus()


func send_ping(tile: int) -> void:
	var c: Array = channels[channel.selected]
	client.send_chat(c[1], "여기를 보세요!", c[2], tile)


func add_message(msg: Dictionary) -> void:
	if muted.has(int(msg.get("from", 0))):
		return
	lines.append({"ch": str(msg["ch"]), "to": int(msg.get("to", 0)), "from": int(msg["from"]), "name": str(msg["name"]), "text": str(msg["text"]), "ping": int(msg.get("ping", -1))})
	_trim()
	_render()


func add_system(text: String, alert: bool) -> void:
	lines.append({"ch": "system", "to": 0, "from": 0, "name": "", "text": text, "ping": -1, "alert": alert})
	_trim()
	_render()


func _trim() -> void:
	if lines.size() > 300:
		lines = lines.slice(lines.size() - 300)


func _render() -> void:
	if channels.is_empty():
		return
	var c: Array = channels[channel.selected]
	var me := client.pid
	var out := ""
	for l in lines:
		var ch: String = l["ch"]
		var show := false
		if ch == "system":
			show = c[1] == "global" or c[1] == "ally"
		elif c[1] == "dm":
			show = ch == "dm" and ((l["from"] == c[2] and l["to"] == me) or (l["from"] == me and l["to"] == c[2]))
		else:
			show = ch == c[1] or (c[1] == "global" and ch == "dm" and (l["to"] == me or l["from"] == me))
		if not show:
			continue
		var text := str(l["text"]).replace("[", "[lb]")
		if ch == "system":
			out += "[color=%s]%s[/color]\n" % ["#ff8a80" if l.get("alert", false) else "#8fb3c7", text]
			continue
		var tag := ""
		match ch:
			"dm": tag = "[color=#e0a0ff][lb]귓속말][/color] "
			"ally": tag = "[color=#7fe0a0][lb]동맹][/color] "
			"dead": tag = "[color=#999][lb]관전][/color] "
		var col := Palette.color(l["from"], client.sim.players[l["from"]].kind if client.sim != null and l["from"] < client.sim.players.size() else 0).to_html(false)
		var ping := ""
		if l["ping"] >= 0:
			ping = " [url=%d][color=#ffd54f][lb]위치 보기][/color][/url]" % l["ping"]
		out += "%s[color=#%s]%s[/color]: %s%s\n" % [tag, col, str(l["name"]).replace("[", "[lb]"), text, ping]
	history.text = ""
	history.append_text(out)
