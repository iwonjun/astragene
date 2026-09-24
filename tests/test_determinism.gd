extends GdUnitTestSuite
## Same seed + same intent log => same state hash. Snapshots resume identically (reconnect/desync recovery).


func _config() -> Dictionary:
	var rules := Ruleset.load_default()
	var list := [{"name": "Human A", "kind": SimPlayer.HUMAN}, {"name": "Human B", "kind": SimPlayer.HUMAN}]
	for i in 3:
		list.append({"name": "Nation %d" % i, "kind": SimPlayer.NATION, "difficulty": i, "profile": i})
	for i in 6:
		list.append({"name": "Bot %d" % i, "kind": SimPlayer.BOT})
	return {"seed": 99, "width": 200, "height": 120, "players": list}


func _intents(sim: Sim, t: int) -> Array:
	if t == 1:
		return [{"type": "spawn", "p": 1, "tile": _land(sim, 1)}, {"type": "spawn", "p": 2, "tile": _land(sim, 2)}]
	if t > 100 and t % 40 == 0:
		return [{"type": "attack", "p": 1 + (t / 40) % 2, "target": 0, "ratio": 250}]
	if t == 400:
		return [{"type": "propose", "p": 1, "to": 3, "treaty": Diplomacy.NAP}]
	return []


func _land(sim: Sim, k: int) -> int:
	var seen := 0
	for t in range(sim.width * 30, sim.owner.size()):
		if sim.passable(t) and sim.owner[t] == Sim.NONE:
			seen += 1
			if seen == k * 700:
				return t
	return -1


func test_identical_inputs_give_identical_hashes() -> void:
	var a := Sim.create(_config())
	var b := Sim.create(_config())
	assert_int(a.state_hash()).is_equal(b.state_hash())
	for t in 1000:
		a.step(_intents(a, t))
		b.step(_intents(b, t))
		if t % 100 == 0:
			assert_int(a.state_hash()).is_equal(b.state_hash())
	assert_int(a.state_hash()).is_equal(b.state_hash())
	assert_int(a.players[1].tiles).is_greater(0)


func test_different_intents_diverge() -> void:
	var a := Sim.create(_config())
	var b := Sim.create(_config())
	for t in 200:
		a.step(_intents(a, t))
		b.step(_intents(b, t) if t != 150 else [{"type": "attack", "p": 1, "target": 0, "ratio": 900}])
	assert_int(a.state_hash()).is_not_equal(b.state_hash())


func test_snapshot_restores_and_continues_identically() -> void:
	var a := Sim.create(_config())
	for t in 600:
		a.step(_intents(a, t))
	var bytes := a.snapshot()
	var b := Sim.restore(bytes)
	assert_int(b.state_hash()).is_equal(a.state_hash())
	for t in range(600, 1200):
		a.step(_intents(a, t))
		b.step(_intents(b, t))
	assert_int(b.state_hash()).is_equal(a.state_hash())
	assert_int(bytes.size()).is_less(400000)
