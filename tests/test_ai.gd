extends GdUnitTestSuite
## Bots expand; nations build, sign treaties and fight; a full AI match finishes with a winner.


func test_full_ai_match_ends_with_winner_within_budget() -> void:
	var rules := Ruleset.load_default()
	var sim := Sim.create({"seed": 42, "minutes": 20, "players": load("res://tools/sim_runner.gd").roster(16, 6, 1, rules, 42)}, rules)
	var worst := 0
	var total := 0
	var built := false
	var treaty := false
	var era_up := false
	while not sim.over and sim.tick < sim.duration_ticks + 20:
		var s := Time.get_ticks_usec()
		sim.step([])
		var us := Time.get_ticks_usec() - s
		total += us
		worst = maxi(worst, us)
		if sim.tick % 50 == 0:
			for e in sim.events:
				built = built or e["type"] == "build"
				treaty = treaty or e["type"] == "treaty"
				era_up = era_up or e["type"] == "era"
	assert_bool(sim.over).is_true()
	assert_int(sim.winners.size()).is_greater(0)
	assert_bool(built).is_true()
	assert_bool(era_up).is_true()
	# 10 ticks/s gives a 100 ms budget; the average must leave most of it to rendering.
	assert_int(total / maxi(1, sim.tick)).is_less(10000)


func test_nation_judges_proposals_by_trust_and_power() -> void:
	var sim := Sim.create({"seed": 3, "width": 120, "height": 80, "flat": true, "players": [
		{"name": "H", "kind": SimPlayer.HUMAN}, {"name": "N", "kind": SimPlayer.NATION, "profile": NationAI.MERCHANT}]})
	var nation: SimPlayer = sim.players[2]
	var human: SimPlayer = sim.players[1]
	human.pop = nation.pop * 3
	assert_bool(sim.nation_brain.judge(sim, nation, 1, Diplomacy.NAP)).is_true()
	assert_bool(sim.nation_brain.judge(sim, nation, 1, Diplomacy.TRIBUTE)).is_true()
	human.traitor_until = sim.tick + 100
	assert_bool(sim.nation_brain.judge(sim, nation, 1, Diplomacy.NAP)).is_false()
	human.traitor_until = 0
	nation.trust[1] = 200
	assert_bool(sim.nation_brain.judge(sim, nation, 1, Diplomacy.TRADE)).is_false()
