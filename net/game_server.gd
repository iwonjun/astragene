class_name GameServer
extends Node
## Listen/dedicated server. Relays intents in fixed 100 ms turns, keeps its own simulation for hash
## checks, snapshots (reconnect/desync recovery) and AI chat replies. It never sends tile state
## except inside a snapshot to a (re)joining or desynced client.

signal lobby_changed
signal log_line(text: String)

const TICK_USEC := 100000
const HASH_EVERY := 50

var enet := ENetMultiplayerPeer.new()
var websocket := WebSocketMultiplayerPeer.new()
var rules: Ruleset
var settings := {"seed": 0, "minutes": 20, "bots": 20, "nations": 6, "difficulty": 1, "width": 400, "height": 240, "filter": false}
var seats: Array = []          # {key, name, pid, token, online}
var sim: Sim = null
var started := false
var host_key := ""
var pending: Array = []        # intents for the next turn (already stamped)
var hashes := {}               # tick -> hash (recent checkpoints)
var chat_limits := {}          # key -> PackedInt32Array of recent send ticks
var local_links := {}          # key -> NetLinks.LocalLink
var turn_log: Array = []       # [tick, intents] kept for diagnostics of the last minute
var hash_ok := 0
var desyncs := 0
var manual_turns := false      # tests drive turns with force_turn()
var _accum := 0
var _last_usec := 0
var _next_local := 1
var ai_voice := AiVoice.new()
var web := WebHost.new()
var chat_filter := ChatFilter.new()


func _init() -> void:
	rules = Ruleset.load_default()
	settings["seed"] = randi() & 0x7FFFFFFF


func listen(port: int) -> Error:
	var e := enet.create_server(port, 32)
	if e != OK:
		return e
	enet.transfer_mode = MultiplayerPeer.TRANSFER_MODE_RELIABLE
	var w := websocket.create_server(port + 1)
	if w != OK:
		push_warning("WebSocket server unavailable on %d: %s" % [port + 1, error_string(w)])
	for p in [enet, websocket]:
		p.peer_disconnected.connect(_on_disconnect.bind(p))
	if web.start(port + WebHost.PORT_OFFSET):
		_log("웹 클라이언트 제공: http://<이 PC의 IP>:%d" % (port + WebHost.PORT_OFFSET))
	_log("서버 시작: ENet %d / WebSocket %d" % [port, port + 1])
	return OK


## In-process seat for the host (or tests). Returns the client-side link.
func local_link() -> NetLinks.LocalLink:
	var link := NetLinks.LocalLink.new()
	link.server = self
	link.key = "l:%d" % _next_local
	_next_local += 1
	local_links[link.key] = link
	return link


func _process(_delta: float) -> void:
	web.poll()
	for peer: MultiplayerPeer in [enet, websocket]:
		if peer.get_connection_status() == MultiplayerPeer.CONNECTION_DISCONNECTED:
			continue
		peer.poll()
		while peer.get_available_packet_count() > 0:
			var id: int = peer.get_packet_peer()
			var msg := Protocol.decode(peer.get_packet())
			if not msg.is_empty():
				receive(("e:%d" if peer == enet else "w:%d") % id, msg)
	if started and not manual_turns:
		var now := Time.get_ticks_usec()
		_accum += now - _last_usec if _last_usec > 0 else 0
		_last_usec = now
		# Catch up at most 5 turns after a hitch, never run ahead of real time.
		var n := 0
		while _accum >= TICK_USEC and n < 5:
			_accum -= TICK_USEC
			_turn()
			n += 1
		if _accum > TICK_USEC * 5:
			_accum = 0


func _send(key: String, msg: Dictionary) -> void:
	if local_links.has(key):
		local_links[key].inbox.append(msg)
		return
	var peer: MultiplayerPeer = enet if key.begins_with("e:") else websocket
	peer.set_target_peer(int(key.substr(2)))
	peer.put_packet(Protocol.encode(msg))


func _broadcast(msg: Dictionary) -> void:
	for s in seats:
		if s["online"]:
			_send(s["key"], msg)


func _seat(key: String) -> Dictionary:
	for s in seats:
		if s["key"] == key:
			return s
	return {}


func _log(text: String) -> void:
	log_line.emit(text)
	print("[server] ", text)


# ------------------------------------------------------------------ messages

func receive(key: String, msg: Dictionary) -> void:
	match str(msg.get("m", "")):
		"hello": _hello(key, msg)
		"setup": _setup(key, msg)
		"start":
			if key == host_key:
				start_match()
		"intent": _intent(key, msg)
		"chat": _chat(key, msg)
		"hash": _hash(key, msg)


func _hello(key: String, msg: Dictionary) -> void:
	if int(msg.get("version", 0)) != Protocol.VERSION or int(msg.get("digest", 0)) != rules.digest():
		_send(key, {"m": "reject", "reason": "게임 버전 또는 규칙 데이터가 호스트와 다릅니다."})
		return
	var name := str(msg.get("name", "플레이어")).strip_edges().left(16)
	if name.is_empty():
		name = "플레이어"
	var token := str(msg.get("token", ""))
	if started:
		# Reconnect: same token takes back its nation and receives a snapshot.
		for s in seats:
			if token != "" and s["token"] == token:
				s["key"] = key
				s["online"] = true
				_send(key, {"m": "welcome", "key": key, "host": false})
				_send(key, {"m": "start", "config": _config(), "pid": s["pid"], "token": token})
				_send(key, {"m": "snapshot", "t": sim.tick, "data": sim.snapshot()})
				_log("%s 재접속" % s["name"])
				_broadcast({"m": "notice", "text": "%s 님이 다시 접속했습니다." % s["name"]})
				lobby_changed.emit()
				return
		_send(key, {"m": "reject", "reason": "이미 경기가 시작되었습니다."})
		return
	if seats.size() >= 16:
		_send(key, {"m": "reject", "reason": "방이 가득 찼습니다."})
		return
	if host_key == "":
		host_key = key
	seats.append({"key": key, "name": _unique(name), "pid": 0, "token": "%08x%08x" % [randi(), randi()], "online": true})
	_send(key, {"m": "welcome", "key": key, "host": key == host_key})
	_lobby()


func _unique(name: String) -> String:
	var taken := {}
	for s in seats:
		taken[s["name"]] = true
	var out := name
	var n := 2
	while taken.has(out):
		out = "%s%d" % [name, n]
		n += 1
	return out


func _setup(key: String, msg: Dictionary) -> void:
	if key != host_key or started:
		return
	for k in ["minutes", "bots", "nations", "difficulty"]:
		if msg.has(k):
			settings[k] = int(msg[k])
	settings["minutes"] = clampi(settings["minutes"], 5, 60)
	settings["bots"] = clampi(settings["bots"], 0, 60)
	settings["nations"] = clampi(settings["nations"], 0, 20)
	settings["difficulty"] = clampi(settings["difficulty"], 0, 2)
	if msg.has("filter"):
		settings["filter"] = bool(msg["filter"])
	if msg.has("seed"):
		settings["seed"] = int(msg["seed"]) & 0x7FFFFFFF
	_lobby()


func _lobby() -> void:
	var names := []
	for s in seats:
		names.append({"name": s["name"], "online": s["online"], "host": s["key"] == host_key})
	_broadcast({"m": "lobby", "players": names, "settings": settings})
	lobby_changed.emit()


func _config() -> Dictionary:
	var list := []
	for s in seats:
		list.append({"name": s["name"], "kind": SimPlayer.HUMAN})
	var rng := SimRng.new(int(settings["seed"]) ^ 0x1234567)
	var names: Dictionary = rules.data["names"]
	for i in int(settings["nations"]):
		list.append({"name": _ai_name(names, rng, "suffix_nation"), "kind": SimPlayer.NATION, "difficulty": settings["difficulty"], "profile": i % rules.ai_profiles.size()})
	for i in int(settings["bots"]):
		list.append({"name": _ai_name(names, rng, "suffix_bot"), "kind": SimPlayer.BOT})
	return {"seed": settings["seed"], "minutes": settings["minutes"], "width": settings["width"], "height": settings["height"], "players": list}


static func _ai_name(names: Dictionary, rng: SimRng, suffix: String) -> String:
	var first: Array = names["first"]
	var second: Array = names["second"]
	var suffixes: Array = names[suffix]
	return "%s%s %s" % [first[rng.below(first.size())], second[rng.below(second.size())], suffixes[rng.below(suffixes.size())]]


func start_match() -> void:
	if started or seats.is_empty():
		return
	for i in seats.size():
		seats[i]["pid"] = i + 1
	var config := _config()
	sim = Sim.create(config, rules)
	started = true
	_last_usec = 0
	_accum = 0
	chat_filter.enabled = settings["filter"]
	for s in seats:
		if s["online"]:
			_send(s["key"], {"m": "start", "config": config, "pid": s["pid"], "token": s["token"]})
	_log("경기 시작: 인간 %d, 국가 %d, 봇 %d, 시드 %d" % [seats.size(), settings["nations"], settings["bots"], settings["seed"]])
	lobby_changed.emit()


func _intent(key: String, msg: Dictionary) -> void:
	var s := _seat(key)
	if not started or s.is_empty():
		return
	var intent := Protocol.clean_intent(msg.get("i"), s["pid"])
	if not intent.is_empty() and pending.size() < 512:
		pending.append(intent)


func force_turn() -> void:
	if started:
		_turn()


func _turn() -> void:
	var intents := pending
	pending = []
	var t := sim.tick
	_broadcast({"m": "turn", "t": t, "i": intents})
	sim.step(intents)
	if t % HASH_EVERY == 0:
		hashes[t] = sim.state_hash()
		hashes.erase(t - HASH_EVERY * 20)
	turn_log.append([t, intents])
	if turn_log.size() > 600:
		turn_log.pop_front()
	_voice_events()


func _hash(key: String, msg: Dictionary) -> void:
	var t := int(msg.get("t", -1))
	if not hashes.has(t):
		return
	if int(msg.get("h", 0)) == hashes[t]:
		hash_ok += 1
		if hash_ok % 50 == 0:
			_log("해시 검사 %d회 일치, 디싱크 %d회 (틱 %d)" % [hash_ok, desyncs, t])
		return
	desyncs += 1
	if true:
		var s := _seat(key)
		_log("디싱크 감지: %s 틱 %d — 스냅샷으로 복구" % [s.get("name", key), t])
		_send(key, {"m": "snapshot", "t": sim.tick, "data": sim.snapshot()})


func _on_disconnect(id: int, peer: MultiplayerPeer) -> void:
	var key := ("e:%d" if peer == enet else "w:%d") % id
	var s := _seat(key)
	if s.is_empty():
		return
	if started:
		s["online"] = false
		_broadcast({"m": "notice", "text": "%s 님의 연결이 끊겼습니다. 같은 기기에서 다시 접속할 수 있습니다." % s["name"]})
	else:
		seats.erase(s)
		if key == host_key:
			host_key = seats[0]["key"] if seats.size() > 0 else ""
	_lobby()


# ------------------------------------------------------------------ chat

func _chat(key: String, msg: Dictionary) -> void:
	var s := _seat(key)
	if s.is_empty():
		return
	var text := str(msg.get("text", "")).strip_edges().left(int(rules.section("chat")["max_length"]))
	if text.is_empty():
		return
	var ch := str(msg.get("ch", "global"))
	var now := sim.tick if sim != null else Time.get_ticks_msec() / 100
	if not _rate_ok(key, ch, now):
		_send(key, {"m": "notice", "text": "채팅이 너무 빠릅니다. 잠시 후 다시 보내세요."})
		return
	text = chat_filter.clean(text)
	var out := {"m": "chat", "from": s["pid"], "name": s["name"], "ch": ch, "to": int(msg.get("to", 0)), "text": text, "ping": int(msg.get("ping", -1))}
	if not started:
		_broadcast(out)
		return
	var me := sim.player(s["pid"])
	if me != null and not me.alive and ch != "dm":
		out["ch"] = "dead"
	match out["ch"]:
		"dm":
			var target: int = out["to"]
			_send(key, out)
			var other := sim.player(target)
			if other != null and other.kind == SimPlayer.NATION:
				var reply := ai_voice.reply(sim, other, s["pid"], text)
				_send(key, {"m": "chat", "from": target, "name": other.name, "ch": "dm", "to": s["pid"], "text": reply, "ping": -1})
			for seat in seats:
				if seat["pid"] == target and seat["online"]:
					_send(seat["key"], out)
		"ally":
			for seat in seats:
				if seat["online"] and Diplomacy.allied(sim, s["pid"], seat["pid"]):
					_send(seat["key"], out)
		"dead":
			for seat in seats:
				var p := sim.player(seat["pid"])
				if seat["online"] and p != null and not p.alive:
					_send(seat["key"], out)
		_:
			_broadcast(out)


func _rate_ok(key: String, ch: String, now: int) -> bool:
	var c := rules.section("chat")
	var recent: PackedInt32Array = chat_limits.get(key, PackedInt32Array())
	var keep := PackedInt32Array()
	for t in recent:
		if now - t < int(c["burst_ticks"]):
			keep.append(t)
	if keep.size() >= int(c["burst"]):
		chat_limits[key] = keep
		return false
	if ch == "global" and keep.size() > 0 and now - keep[keep.size() - 1] < int(c["global_cooldown_ticks"]):
		chat_limits[key] = keep
		return false
	keep.append(now)
	chat_limits[key] = keep
	return true


## Nations speak in global chat when the simulation reports notable moments (betrayal, war, treaties).
var _event_cursor := 0
func _voice_events() -> void:
	var start := maxi(_event_cursor - sim.event_base, 0)
	for i in range(start, sim.events.size()):
		var e: Dictionary = sim.events[i]
		var line := ai_voice.on_event(sim, e)
		if line.is_empty():
			continue
		var out := {"m": "chat", "from": line[0], "name": sim.players[line[0]].name, "ch": line[1], "to": line[2], "text": line[3], "ping": -1}
		if line[1] == "dm":
			for seat in seats:
				if seat["pid"] == line[2] and seat["online"]:
					_send(seat["key"], out)
		else:
			_broadcast(out)
	_event_cursor = sim.event_base + sim.events.size()
