extends GdUnitTestSuite
## Real ENet loopback + in-process host seat: lobby, start, turn relay, hash agreement, chat routing,
## desync recovery by snapshot and reconnect with the session token.

var server: GameServer
var host: GameClient
var guest: GameClient


func _pump(frames: int = 1) -> void:
	for i in frames:
		server._process(0.016)
		host._process(0.016)
		guest._process(0.016)
		OS.delay_msec(2)


func _until(cond: Callable, frames: int = 600) -> bool:
	for i in frames:
		_pump()
		if cond.call():
			return true
	return false


func before_test() -> void:
	server = auto_free(GameServer.new())
	add_child(server)
	server.manual_turns = true
	assert_int(server.listen(27560)).is_equal(OK)
	host = auto_free(GameClient.new())
	guest = auto_free(GameClient.new())
	add_child(host)
	add_child(guest)
	host.begin(server.local_link(), "호스트")
	guest.begin(NetLinks.connect_enet("127.0.0.1", 27560), "손님", "test-%d" % Time.get_ticks_msec())


func after_test() -> void:
	server.enet.close()
	server.websocket.close()


func test_lobby_start_turns_hashes_and_chat() -> void:
	assert_bool(_until(func(): return server.seats.size() == 2)).is_true()
	assert_bool(host.is_host).is_true()
	host.send_setup({"bots": 2, "nations": 1, "minutes": 10, "seed": 77})
	assert_bool(_until(func(): return int(guest.settings.get("seed", 0)) == 77)).is_true()
	host.send_start()
	assert_bool(_until(func(): return host.sim != null and guest.sim != null)).is_true()
	assert_int(guest.pid).is_equal(2)
	var land := -1
	for t in range(guest.sim.owner.size() / 2, guest.sim.owner.size()):
		if guest.sim.spawn_ok(t, 2):
			land = t
			break
	guest.send_intent({"type": "spawn", "tile": land, "p": 1})  # the server overrides "p" with the sender
	for i in 160:
		_pump()
		server.force_turn()
	assert_bool(_until(func(): return guest.sim.tick == server.sim.tick and host.sim.tick == server.sim.tick)).is_true()
	assert_int(guest.sim.players[2].capital).is_equal(land)
	assert_int(guest.sim.state_hash()).is_equal(server.sim.state_hash())
	assert_int(host.sim.state_hash()).is_equal(server.sim.state_hash())
	var got := []
	guest.chat_received.connect(func(m): got.append(m))
	host.send_chat("global", "안녕하세요!")
	host.send_chat("dm", "비밀 이야기", 3)  # pid 3 is an AI nation: it answers only the sender
	assert_bool(_until(func(): return got.size() >= 1)).is_true()
	_pump(20)
	assert_str(got[0]["text"]).is_equal("안녕하세요!")
	for m in got:
		assert_str(m["ch"]).is_not_equal("dm")


func test_desynced_client_is_repaired_by_snapshot() -> void:
	assert_bool(_until(func(): return server.seats.size() == 2)).is_true()
	host.send_setup({"bots": 3, "nations": 0, "seed": 5})
	_pump(5)
	host.send_start()
	assert_bool(_until(func(): return guest.sim != null)).is_true()
	for i in 20:
		_pump()
		server.force_turn()
	_pump(20)
	guest.sim.players[3].pop += 12345  # corrupt the guest locally
	var repaired := [false]
	guest.resynced.connect(func(): repaired[0] = true)
	for i in 60:
		_pump()
		server.force_turn()
	assert_bool(_until(func(): return repaired[0])).is_true()
	assert_bool(_until(func(): return guest.sim.tick == server.sim.tick)).is_true()
	assert_int(guest.sim.state_hash()).is_equal(server.sim.state_hash())


func test_reconnect_with_session_token_resumes_the_same_nation() -> void:
	var address := "reconnect-%d" % Time.get_ticks_msec()
	guest.link.close()
	guest.set_process(false)
	guest = auto_free(GameClient.new())
	add_child(guest)
	guest.begin(NetLinks.connect_enet("127.0.0.1", 27560), "손님", address)
	assert_bool(_until(func(): return server.seats.size() == 2)).is_true()
	host.send_setup({"bots": 2, "nations": 0, "seed": 9})
	_pump(5)
	host.send_start()
	assert_bool(_until(func(): return guest.sim != null)).is_true()
	for i in 120:
		_pump()
		server.force_turn()
	var pid := guest.pid
	guest.link.close()
	assert_bool(_until(func(): return not server.seats[1]["online"])).is_true()
	for i in 50:
		server.force_turn()
	var back: GameClient = auto_free(GameClient.new())
	add_child(back)
	back.begin(NetLinks.connect_enet("127.0.0.1", 27560), "손님", address)
	guest = back
	assert_bool(_until(func(): return back.sim != null and back.sim.tick == server.sim.tick)).is_true()
	assert_int(back.pid).is_equal(pid)
	for i in 30:
		_pump()
		server.force_turn()
	assert_bool(_until(func(): return back.sim.tick == server.sim.tick)).is_true()
	assert_int(back.sim.state_hash()).is_equal(server.sim.state_hash())


func test_eight_humans_thirty_bots_stay_in_sync() -> void:
	var clients: Array = [host, guest]
	for i in 6:
		var c: GameClient = auto_free(GameClient.new())
		add_child(c)
		c.begin(server.local_link(), "P%d" % (i + 3))
		clients.append(c)
	var all_joined := func():
		for c in clients:
			c._process(0.016)
		return server.seats.size() == 8
	assert_bool(_until(all_joined)).is_true()
	host.send_setup({"bots": 30, "nations": 6, "seed": 1234, "minutes": 20})
	_pump(5)
	host.send_start()
	var started := func():
		for c in clients:
			c._process(0.016)
		for c in clients:
			if c.sim == null:
				return false
		return true
	assert_bool(_until(started)).is_true()
	var rng := RandomNumberGenerator.new()
	rng.seed = 42
	var resyncs := [0]
	for c in clients:
		c.resynced.connect(func(): resyncs[0] += 1)
	for t in 1500:
		server._process(0.016)
		server.force_turn()
		for c in clients:
			c._process(0.016)
			var me: SimPlayer = c.me()
			if me == null or not me.alive:
				continue
			if c.sim.in_spawn_phase() and not me.spawned and t % 7 == c.pid:
				c.send_intent({"type": "spawn", "tile": rng.randi_range(0, c.sim.owner.size() - 1)})
			elif t % 25 == c.pid:
				c.send_intent({"type": "attack", "target": 0 if rng.randf() < 0.7 else rng.randi_range(1, c.sim.players.size() - 1), "ratio": 300})
			elif t % 97 == c.pid:
				c.send_intent({"type": "propose", "to": rng.randi_range(1, 8), "treaty": rng.randi_range(0, 5)})
		guest._process(0.016)
	var synced := func():
		for c in clients:
			c._process(0.016)
			if c.sim.tick != server.sim.tick:
				return false
		return true
	assert_bool(_until(synced)).is_true()
	for c in clients:
		assert_int(c.sim.state_hash()).is_equal(server.sim.state_hash())
	assert_int(resyncs[0]).is_equal(0)
	assert_int(server.sim.players.size() - 1).is_equal(44)
