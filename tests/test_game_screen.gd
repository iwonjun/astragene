extends GdUnitTestSuite
## Drives the real GameScreen with a local server: clicking wilderness sends an attack intent that
## the server applies; build mode and the diplomacy popup produce the right intents.

var server: GameServer
var client: GameClient
var screen: GameScreen


func _pump(frames: int = 1, turns: bool = true) -> void:
	for i in frames:
		server._process(0.016)
		if turns:
			server.force_turn()
		client._process(0.016)
		screen._process(0.016)


func before_test() -> void:
	server = auto_free(GameServer.new())
	add_child(server)
	server.manual_turns = true
	client = auto_free(GameClient.new())
	add_child(client)
	client.begin(server.local_link(), "테스터")
	for i in 5:
		server._process(0.016)
		client._process(0.016)
	client.send_setup({"bots": 4, "nations": 1, "seed": 31})
	client.send_start()
	for i in 5:
		server._process(0.016)
		client._process(0.016)
	screen = auto_free(GameScreen.new())
	screen.client = client
	add_child(screen)


func _land_near(t: int, want_owner: int) -> int:
	var sim := client.sim
	for r in range(1, 40):
		for dy in range(-r, r + 1):
			for dx in range(-r, r + 1):
				var x := t % sim.width + dx
				var y := t / sim.width + dy
				if x < 0 or y < 0 or x >= sim.width or y >= sim.height:
					continue
				var c := sim.idx(x, y)
				if sim.passable(c) and sim.owner[c] == want_owner:
					return c
	return -1


func test_spawn_then_click_wilderness_expands() -> void:
	var sim := client.sim
	var spawn := -1
	for t in range(sim.owner.size() / 3, sim.owner.size()):
		if sim.spawn_ok(t, client.pid):
			spawn = t
			break
	screen._click(spawn)
	_pump(3)
	assert_bool(client.me().spawned).is_true()
	while client.sim.in_spawn_phase():
		_pump()
	var tiles := client.me().tiles
	var wild := _land_near(client.me().capital, 0)
	assert_int(wild).is_greater_equal(0)
	screen._click(wild)
	_pump(40)
	assert_int(client.me().tiles).is_greater(tiles)


func test_build_mode_and_popup_proposal() -> void:
	while client.sim.in_spawn_phase():
		_pump()
	var me := client.me()
	me.gold_milli = 100000000  # local view only; the server copy decides
	server.sim.players[client.pid].gold_milli = 100000000
	screen._set_mode("build", Economy.CITY)
	screen._click(me.capital)
	_pump(3)
	assert_int(server.sim.buildings_of(client.pid, Economy.CITY)).is_equal(1)
	var nation := 0
	for p in server.sim.players:
		if p.kind == SimPlayer.NATION:
			nation = p.pid
	screen._propose(nation, Diplomacy.NAP)
	_pump(2)
	var found := false
	for pr in server.sim.proposals:
		found = found or (pr["from"] == client.pid and pr["to"] == nation)
	for e in server.sim.events:
		found = found or (e["type"] == "treaty" or e["type"] == "rejected")
	assert_bool(found).is_true()
