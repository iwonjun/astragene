extends SceneTree
## Headless AI-vs-AI match for balance and performance checks (design.md 12.4).
## godot --headless -s tools/sim_runner.gd -- --seed=42 --minutes=20 --bots=20 --nations=6 --difficulty=1

func arg(name: String, fallback: String) -> String:
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--" + name + "="):
			return a.substr(name.length() + 3)
	return fallback


static func roster(bots: int, nations: int, difficulty: int, rules: Ruleset, seed_value: int) -> Array:
	var names: Dictionary = rules.data["names"]
	var rng := SimRng.new(seed_value ^ 0x1234567)
	var list := []
	for i in nations:
		list.append({"name": _name(names, rng, "suffix_nation"), "kind": SimPlayer.NATION, "difficulty": difficulty, "profile": i % rules.ai_profiles.size()})
	for i in bots:
		list.append({"name": _name(names, rng, "suffix_bot"), "kind": SimPlayer.BOT})
	return list


static func _name(names: Dictionary, rng: SimRng, suffix: String) -> String:
	var first: Array = names["first"]
	var second: Array = names["second"]
	var suffixes: Array = names[suffix]
	return "%s%s %s" % [first[rng.below(first.size())], second[rng.below(second.size())], suffixes[rng.below(suffixes.size())]]


func _initialize() -> void:
	var seed_value := int(arg("seed", "42"))
	var minutes := int(arg("minutes", "20"))
	var rules := Ruleset.load_default()
	var t0 := Time.get_ticks_msec()
	var sim := Sim.create({"seed": seed_value, "minutes": minutes,
		"players": roster(int(arg("bots", "20")), int(arg("nations", "6")), int(arg("difficulty", "1")), rules, seed_value)}, rules)
	print("created in %d ms, land=%d tiles, players=%d" % [Time.get_ticks_msec() - t0, sim.land_total, sim.players.size() - 1])
	var ticks := minutes * 60 * int(rules.data["tick_rate"])
	var worst := 0
	var total := 0
	var report := int(arg("report", "600"))
	for i in ticks:
		var s := Time.get_ticks_usec()
		sim.step([])
		var us := Time.get_ticks_usec() - s
		total += us
		worst = maxi(worst, us)
		if report > 0 and (i + 1) % report == 0:
			var top: Array = sim.alive_players()
			top.sort_custom(func(a, b): return a.tiles > b.tiles)
			var line := "t=%ds alive=%d avg=%.2fms worst=%.1fms" % [(i + 1) / 10, top.size(), total / 1000.0 / (i + 1), worst / 1000.0]
			for k in mini(3, top.size()):
				var p: SimPlayer = top[k]
				line += " | %s era%d %d%% pop%d" % [p.name, p.era + 1, p.tiles * 100 / sim.land_total, p.pop]
			print(line)
		if sim.over:
			break
	var names := []
	for w in sim.winners:
		names.append(sim.players[w].name)
	print("RESULT tick=%d over=%s winners=%s hash=%08x avg=%.2fms worst=%.1fms treaties=%d" % [sim.tick, sim.over, names, sim.state_hash(), total / 1000.0 / maxi(1, sim.tick), worst / 1000.0, sim.treaties.size()])
	quit(0)
