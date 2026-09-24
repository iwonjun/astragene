class_name GameClient
extends Node
## One seat. Receives the turn stream, runs its own copy of the simulation, sends intents/chat and
## reports a state hash every 50 ticks. Late turns are caught up in bursts; a snapshot from the
## server replaces the local state after a desync or on reconnect.

signal lobby_updated(players: Array, settings: Dictionary)
signal welcomed(is_host: bool)
signal match_started
signal chat_received(msg: Dictionary)
signal notice(text: String)
signal rejected(reason: String)
signal connection_lost
signal resynced

const SESSION_FILE := "user://session.cfg"

var link: NetLinks.Link
var sim: Sim = null
var rules: Ruleset
var pid := 0
var token := ""
var is_host := false
var lobby_players: Array = []
var settings: Dictionary = {}
var turns: Array = []            # [t, intents] waiting to run
var player_name := "플레이어"
var address := ""
var _lost := false
var _started_emitted := false
var steps_this_frame := 0


func _init() -> void:
	rules = Ruleset.load_default()


func begin(new_link: NetLinks.Link, name: String, server_address: String = "local") -> void:
	link = new_link
	player_name = name
	address = server_address
	_lost = false
	var cfg := ConfigFile.new()
	var saved_token := ""
	if cfg.load(SESSION_FILE) == OK:
		saved_token = cfg.get_value("sessions", _session_key(), "")
	_hello_token = saved_token
	_hello_sent = false


var _hello_token := ""
var _hello_sent := false


func _process(_delta: float) -> void:
	if link == null:
		return
	if not _hello_sent and link.connected():
		_hello_sent = true
		link.send({"m": "hello", "name": player_name, "version": Protocol.VERSION, "digest": rules.digest(), "token": _hello_token})
	for msg in link.poll():
		_handle(msg)
	if link.closed() and not _lost:
		_lost = true
		connection_lost.emit()
	_advance()


func _handle(msg: Dictionary) -> void:
	match str(msg.get("m", "")):
		"welcome":
			is_host = bool(msg.get("host", false))
			welcomed.emit(is_host)
		"lobby":
			lobby_players = msg.get("players", [])
			settings = msg.get("settings", {})
			lobby_updated.emit(lobby_players, settings)
		"start":
			pid = int(msg["pid"])
			token = str(msg.get("token", ""))
			var cfg := ConfigFile.new()
			cfg.load(SESSION_FILE)
			cfg.set_value("sessions", _session_key(), token)
			cfg.save(SESSION_FILE)
			sim = Sim.create(msg["config"], rules)
			turns = turns.filter(func(t): return t[0] >= sim.tick)
			if not _started_emitted:
				_started_emitted = true
				match_started.emit()
		"turn":
			turns.append([int(msg["t"]), msg.get("i", [])])
		"snapshot":
			var restored := Sim.restore(msg["data"], rules)
			if restored != null:
				sim = restored
				turns = turns.filter(func(t): return t[0] >= sim.tick)
				resynced.emit()
		"chat":
			chat_received.emit(msg)
		"notice":
			notice.emit(str(msg.get("text", "")))
		"reject":
			rejected.emit(str(msg.get("reason", "")))


## Runs queued turns in order. Normally a few per frame; a backlog (reconnect, hitch) runs in bursts.
func _advance() -> void:
	steps_this_frame = 0
	if sim == null:
		return
	var budget := 3 if turns.size() < 10 else 60
	while budget > 0 and not turns.is_empty():
		var t: int = turns[0][0]
		if t < sim.tick:
			turns.pop_front()
			continue
		if t > sim.tick:
			break  # a gap: wait for the snapshot or the missing turn
		var intents: Array = turns.pop_front()[1]
		sim.step(intents)
		steps_this_frame += 1
		if t % GameServer.HASH_EVERY == 0:
			link.send({"m": "hash", "t": t, "h": sim.state_hash()})
		budget -= 1


## One saved seat per server address and player name (several windows on one PC stay distinct).
func _session_key() -> String:
	return (address + "|" + player_name).uri_encode()


func send_intent(intent: Dictionary) -> void:
	if link != null and sim != null:
		link.send({"m": "intent", "i": intent})


func send_chat(channel: String, text: String, to: int = 0, ping: int = -1) -> void:
	if link != null:
		link.send({"m": "chat", "ch": channel, "to": to, "text": text, "ping": ping})


func send_setup(values: Dictionary) -> void:
	var msg := {"m": "setup"}
	msg.merge(values)
	link.send(msg)


func send_start() -> void:
	link.send({"m": "start"})


func me() -> SimPlayer:
	return sim.player(pid) if sim != null else null


func backlog() -> int:
	return turns.size()
