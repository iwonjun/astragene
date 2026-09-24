extends Node
## App entry. Modes: menu (default), solo (in-process server, works in the browser too), host
## (listen server + own seat), join (ENet on desktop, WebSocket in the browser), and --server
## (headless dedicated server; the first player to join becomes the room host).

var server: GameServer = null
var client: GameClient = null
var screen: Node = null
var player_name := "플레이어"


func arg(name: String, fallback: String = "") -> String:
	for a in OS.get_cmdline_user_args():
		if a == "--" + name:
			return "1"
		if a.begins_with("--" + name + "="):
			return a.substr(name.length() + 3)
	return fallback


func _ready() -> void:
	var cfg := ConfigFile.new()
	if cfg.load("user://profile.cfg") == OK:
		player_name = cfg.get_value("profile", "name", player_name)
	player_name = arg("name", player_name)
	var port := int(arg("port", str(Protocol.DEFAULT_PORT)))
	if arg("server") != "":
		server = GameServer.new()
		add_child(server)
		if server.listen(port) != OK:
			push_error("Cannot listen on %d" % port)
			get_tree().quit(1)
		return
	if arg("solo") != "":
		start_solo({"bots": int(arg("bots", "20")), "nations": int(arg("nations", "6")), "difficulty": int(arg("difficulty", "1"))})
	elif arg("host") != "":
		host_game(port)
	elif arg("join") != "":
		join_game(arg("join"), port)
	else:
		show_menu()


func save_name(name: String) -> void:
	player_name = name.strip_edges().left(16) if name.strip_edges() != "" else "플레이어"
	var cfg := ConfigFile.new()
	cfg.set_value("profile", "name", player_name)
	cfg.save("user://profile.cfg")


func _swap(node: Node) -> void:
	if screen != null:
		screen.queue_free()
	screen = node
	add_child(node)


func _reset_net() -> void:
	if client != null:
		if client.link != null:
			client.link.close()
		client.queue_free()
		client = null
	if server != null:
		server.enet.close()
		server.websocket.close()
		server.queue_free()
		server = null


func show_menu(message: String = "") -> void:
	_reset_net()
	var m := MenuScreen.new()
	m.app = self
	m.message = message
	_swap(m)


func _new_client() -> void:
	client = GameClient.new()
	add_child(client)
	client.match_started.connect(show_game)
	client.rejected.connect(show_menu)
	# Automation for multi-process checks: --autostart (host starts at once), --exit-after-ticks=N.
	if arg("autostart") != "":
		client.lobby_updated.connect(_on_auto_lobby)
	_exit_after = int(arg("exit-after-ticks", "0"))


var _exit_after := 0
var _auto_configured := false
func _on_auto_lobby(players: Array, _settings: Dictionary) -> void:
	if not client.is_host or client.sim != null:
		return
	if not _auto_configured:
		_auto_configured = true
		client.send_setup({"bots": int(arg("bots", "10")), "nations": int(arg("nations", "3")), "seed": int(arg("seed", "4242"))})
	if players.size() >= maxi(1, int(arg("autostart", "1"))):
		client.send_start()


func _process(_delta: float) -> void:
	if _exit_after > 0 and client != null and client.sim != null and client.sim.tick >= _exit_after:
		print("CLIENT HASH tick=%d hash=%08x pid=%d" % [client.sim.tick, client.sim.state_hash(), client.pid])
		get_tree().quit(0)


func start_solo(settings: Dictionary) -> void:
	_reset_net()
	server = GameServer.new()
	add_child(server)
	_new_client()
	client.begin(server.local_link(), player_name)
	_solo_settings = settings
	client.welcomed.connect(_on_solo_welcome, CONNECT_ONE_SHOT)
	_swap(LoadingScreen.new())


var _solo_settings := {}
func _on_solo_welcome(_host: bool) -> void:
	client.send_setup(_solo_settings)
	client.send_start()


func host_game(port: int = Protocol.DEFAULT_PORT) -> void:
	_reset_net()
	server = GameServer.new()
	add_child(server)
	var e := server.listen(port)
	if e != OK:
		show_menu("방을 만들 수 없습니다 (포트 %d 사용 중일 수 있음): %s" % [port, error_string(e)])
		return
	_new_client()
	client.begin(server.local_link(), player_name)
	_show_lobby()


func join_game(address: String, port: int = Protocol.DEFAULT_PORT) -> void:
	_reset_net()
	var host := address.strip_edges()
	if ":" in host:
		port = int(host.get_slice(":", 1))
		host = host.get_slice(":", 0)
	var link: NetLinks.Link = NetLinks.connect_websocket(host, port + 1) if UI.is_web() else NetLinks.connect_enet(host, port)
	if link == null:
		show_menu("주소가 올바르지 않습니다: " + address)
		return
	_new_client()
	client.begin(link, player_name, "%s:%d" % [host, port])
	client.connection_lost.connect(_on_lost_in_lobby)
	_show_lobby()


func _on_lost_in_lobby() -> void:
	if screen is LobbyScreen:
		show_menu("호스트와 연결이 끊어졌습니다. 주소·포트·방화벽을 확인하세요.")


func _show_lobby() -> void:
	var l := LobbyScreen.new()
	l.app = self
	_swap(l)


func show_game() -> void:
	var g := GameScreen.new()
	g.app = self
	g.client = client
	_swap(g)


class LoadingScreen extends Control:
	func _ready() -> void:
		theme = UI.theme()
		set_anchors_preset(Control.PRESET_FULL_RECT)
		var l := UI.label("세계를 만드는 중…", 28, UI.ACCENT)
		l.set_anchors_preset(Control.PRESET_CENTER)
		add_child(l)
