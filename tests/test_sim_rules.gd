extends GdUnitTestSuite
## Rules of Blitz: spawning, growth, conquest, buildings, eras, treaties, betrayal, strikes, victory.


func test_spawn_phase_places_humans_and_auto_spawns_the_idle() -> void:
	var sim := SimFixture.humans(3)
	sim.step([{"type": "spawn", "p": 1, "tile": sim.idx(10, 10)}])
	assert_bool(sim.players[1].spawned).is_true()
	assert_int(sim.players[1].tiles).is_greater(60)
	# Too close to P1 is refused.
	sim.step([{"type": "spawn", "p": 2, "tile": sim.idx(12, 10)}])
	assert_bool(sim.players[2].spawned).is_false()
	SimFixture.run(sim, 120)
	assert_bool(sim.players[2].spawned and sim.players[3].spawned).is_true()


func test_population_grows_logistically_toward_cap() -> void:
	var sim := SimFixture.humans(2)
	SimFixture.place(sim, [[10, 10], [60, 40]])
	var p: SimPlayer = sim.players[1]
	var start := p.pop
	SimFixture.run(sim, 200)
	assert_int(p.pop).is_greater(start)
	assert_int(p.pop).is_less_equal(p.cap)
	SimFixture.run(sim, 3000)
	assert_int(p.pop * 100 / p.cap).is_greater_equal(95)


func test_wilderness_attack_spreads_and_conserves_troops() -> void:
	var sim := SimFixture.humans(2)
	SimFixture.place(sim, [[10, 10], [60, 40]])
	var p: SimPlayer = sim.players[1]
	p.pop = 20000
	var tiles := p.tiles
	sim.step([{"type": "attack", "p": 1, "target": 0, "ratio": 500}])
	assert_int(sim.attacks.size()).is_equal(1)
	# Half the army left home; what it spent is paid in plains tiles at the terrain cost.
	assert_int(p.pop).is_less_equal(10000 + 200)
	var cost: int = Combat.tile_cost(sim, sim.idx(40, 40), 1, 0)
	SimFixture.run(sim, 600)
	assert_int(sim.attacks.size()).is_equal(0)
	assert_int(p.tiles - tiles).is_between(10000 / cost - 60, 10000 / cost + 5)


func test_enemy_attack_captures_land_and_kills_defenders() -> void:
	var sim := SimFixture.humans(2)
	_neighbors(sim)
	assert_bool(sim.border_info(1)["neighbors"].has(2)).is_true()
	var defender: SimPlayer = sim.players[2]
	sim.players[1].pop = 200000
	var tiles := defender.tiles
	var pop := defender.pop
	sim.step([{"type": "attack", "p": 1, "target": 2, "ratio": 800}])
	SimFixture.run(sim, 50)
	assert_int(defender.tiles).is_less(tiles)
	assert_int(defender.pop).is_less(pop + 5000)


func test_buildings_cost_more_each_and_city_raises_cap() -> void:
	var sim := SimFixture.humans(2)
	SimFixture.place(sim, [[10, 10], [60, 40]])
	var p: SimPlayer = sim.players[1]
	p.gold_milli = 100000 * 1000
	var first := Economy.building_cost(sim, 1, Economy.CITY)
	sim.step([{"type": "build", "p": 1, "kind": Economy.CITY, "tile": sim.idx(10, 10)}])
	assert_int(sim.buildings.size()).is_equal(1)
	assert_int(Economy.building_cost(sim, 1, Economy.CITY)).is_greater(first)
	# Ports need a coast and an era; this flat map has none.
	sim.step([{"type": "build", "p": 1, "kind": Economy.PORT, "tile": sim.idx(11, 10)}])
	assert_int(sim.buildings.size()).is_equal(1)
	sim.step([])
	assert_int(p.cap).is_greater(p.tiles * int(sim.rules.eras[0]["cap_per_tile"]))


func test_era_up_offers_three_perks_and_choice_applies() -> void:
	var sim := SimFixture.humans(2)
	SimFixture.place(sim, [[10, 10], [60, 40]])
	var p: SimPlayer = sim.players[1]
	p.gauge = int(sim.rules.eras[0]["gauge_to_next"]) - 1
	sim.step([])
	assert_int(p.era).is_equal(1)
	assert_int(p.perk_options.size()).is_equal(3)
	var chosen: int = p.perk_options[1]
	sim.step([{"type": "perk", "p": 1, "choice": 1}])
	assert_int(p.perk(chosen)).is_equal(1)
	assert_int(p.perk_options.size()).is_equal(0)


func test_lagging_player_gets_catch_up_gauge() -> void:
	var sim := SimFixture.humans(2)
	SimFixture.place(sim, [[10, 10], [60, 40]])
	sim.players[2].era = 3
	var g1: int = sim.players[1].gauge
	sim.step([])
	var behind: int = sim.players[1].gauge - g1
	sim.players[2].era = 0
	g1 = sim.players[1].gauge
	sim.step([])
	assert_int(behind).is_greater(sim.players[1].gauge - g1)


## Two players flood the whole flat map from opposite sides until they share a border.
func _neighbors(sim: Sim) -> void:
	SimFixture.place(sim, [[20, 25], [40, 25]])
	sim.players[1].pop = 60000
	sim.players[2].pop = 60000
	sim.step([{"type": "attack", "p": 1, "target": 0, "ratio": 900}, {"type": "attack", "p": 2, "target": 0, "ratio": 900}])
	SimFixture.run(sim, 600)


func test_attacking_a_nap_partner_is_betrayal() -> void:
	var sim := SimFixture.humans(2)
	_neighbors(sim)
	sim.step([{"type": "propose", "p": 1, "to": 2, "treaty": Diplomacy.NAP}])
	assert_int(sim.proposals.size()).is_equal(1)
	sim.step([{"type": "respond", "p": 2, "id": sim.proposals[0]["id"], "accept": true}])
	assert_bool(Diplomacy.has(sim, 1, 2, Diplomacy.NAP)).is_true()
	sim.step([{"type": "attack", "p": 2, "target": 1, "ratio": 300}])
	assert_bool(sim.players[2].is_traitor(sim.tick)).is_true()
	assert_bool(Diplomacy.has(sim, 1, 2, Diplomacy.NAP)).is_false()
	var betrayal := false
	for e in sim.events:
		betrayal = betrayal or e["type"] == "betrayal"
	assert_bool(betrayal).is_true()
	# A traitor is easier to hit.
	var cost_traitor := Combat.tile_cost(sim, sim.idx(40, 25), 1, 2)
	sim.players[2].traitor_until = 0
	assert_int(Combat.tile_cost(sim, sim.idx(40, 25), 1, 2)).is_greater(cost_traitor)


func test_nap_notice_break_is_not_betrayal() -> void:
	var sim := SimFixture.humans(2)
	_neighbors(sim)
	Diplomacy.sign_treaty(sim, Diplomacy.NAP, 1, 2)
	var id: int = sim.treaties[0]["id"]
	sim.step([{"type": "break", "p": 1, "id": id}])
	assert_bool(Diplomacy.has(sim, 1, 2, Diplomacy.NAP)).is_true()
	SimFixture.run(sim, int(sim.rules.treaties[Diplomacy.NAP]["notice"]) + 1)
	assert_bool(Diplomacy.has(sim, 1, 2, Diplomacy.NAP)).is_false()
	sim.step([{"type": "attack", "p": 1, "target": 2, "ratio": 300}])
	assert_bool(sim.players[1].is_traitor(sim.tick)).is_false()


func test_alliance_stops_attacks_allows_donations_and_shares_victory() -> void:
	var sim := SimFixture.humans(3)
	SimFixture.place(sim, [[20, 25], [40, 25], [70, 45]])
	sim.step([{"type": "attack", "p": 1, "target": 0, "ratio": 400}])
	Diplomacy.sign_treaty(sim, Diplomacy.ALLIANCE, 1, 2)
	var gold: int = sim.players[2].gold_milli
	sim.step([{"type": "donate", "p": 1, "to": 2, "troops": 100, "gold": 50}])
	assert_int(sim.players[2].gold_milli).is_greater(gold)
	sim.step([{"type": "donate", "p": 1, "to": 3, "troops": 100, "gold": 50}])  # not allied: ignored
	var groups := Diplomacy.groups(sim)
	assert_int(groups.size()).is_equal(2)
	assert_bool(groups[0] == PackedInt32Array([1, 2])).is_true()


func test_domination_victory_for_alliance_group() -> void:
	var sim := SimFixture.humans(3)
	SimFixture.place(sim, [[10, 10], [40, 25], [70, 45]])
	Diplomacy.sign_treaty(sim, Diplomacy.ALLIANCE, 1, 2)
	for t in sim.owner.size():
		if sim.owner[t] == Sim.NONE:
			sim.set_owner(t, 1 if t % 2 == 0 else 2)
	SimFixture.run(sim, 10)
	assert_bool(sim.over).is_true()
	assert_bool(sim.winners == PackedInt32Array([1, 2])).is_true()


func test_timer_victory_goes_to_the_largest() -> void:
	var sim := SimFixture.humans(2, {"minutes": 1})
	SimFixture.place(sim, [[10, 10], [60, 40]])
	sim.step([{"type": "attack", "p": 2, "target": 0, "ratio": 600}])
	SimFixture.run(sim, 700)
	assert_bool(sim.over).is_true()
	assert_int(sim.winners[0]).is_equal(2)


func test_missile_destroys_buildings_and_nuke_wipes_land() -> void:
	var sim := SimFixture.humans(2)
	SimFixture.place(sim, [[15, 15], [60, 35]])
	var a: SimPlayer = sim.players[1]
	var b: SimPlayer = sim.players[2]
	a.era = 6; b.gold_milli = 10000000
	a.gold_milli = 100000000
	sim.step([{"type": "build", "p": 1, "kind": Economy.SILO, "tile": sim.idx(15, 15)}, {"type": "build", "p": 2, "kind": Economy.CITY, "tile": sim.idx(60, 35)}])
	assert_int(sim.buildings.size()).is_equal(2)
	var pop := b.pop
	sim.step([{"type": "strike", "p": 1, "kind": Strikes.MISSILE, "tile": sim.idx(60, 35)}])
	SimFixture.run(sim, 20)
	assert_bool(sim.buildings[1]["alive"]).is_false()
	assert_int(b.pop).is_less(pop + 2000)
	var tiles := b.tiles
	sim.step([{"type": "strike", "p": 1, "kind": Strikes.NUKE, "tile": sim.idx(60, 35)}])
	SimFixture.run(sim, 20)
	assert_int(b.tiles).is_less(tiles)


func test_hack_disables_enemy_buildings() -> void:
	var sim := SimFixture.humans(2)
	SimFixture.place(sim, [[15, 15], [60, 35]])
	sim.players[1].era = 7
	sim.players[1].gold_milli = 100000000
	sim.players[2].gold_milli = 100000000
	sim.step([{"type": "build", "p": 2, "kind": Economy.MARKET, "tile": sim.idx(60, 35)}])
	sim.step([{"type": "strike", "p": 1, "kind": Strikes.HACK, "tile": sim.idx(60, 35)}])
	assert_int(sim.buildings[0]["disabled_until"]).is_greater(sim.tick)
	assert_int(Economy.building_counts(sim)[2][Economy.MARKET]).is_equal(0)


func test_boat_lands_across_the_sea() -> void:
	var sim := SimFixture.humans(2, {"ocean_x0": 30, "ocean_x1": 45})
	SimFixture.place(sim, [[24, 25], [60, 25]])
	var p: SimPlayer = sim.players[1]
	p.era = 4
	p.gold_milli = 100000000
	p.pop = 50000
	sim.step([{"type": "attack", "p": 1, "target": 0, "ratio": 300}])
	SimFixture.run(sim, 150)
	var port := -1
	for y in sim.height:
		var t := sim.idx(29, y)
		if sim.owner[t] == 1:
			port = t
			break
	assert_int(port).is_greater_equal(0)
	sim.step([{"type": "build", "p": 1, "kind": Economy.PORT, "tile": port}])
	assert_int(sim.building_at[port]).is_greater_equal(0)
	var beach := sim.idx(45, port / sim.width)
	sim.step([{"type": "boat", "p": 1, "from": port, "tile": beach, "ratio": 400}])
	assert_int(sim.boats.size()).is_equal(1)
	SimFixture.run(sim, 40)
	assert_int(sim.owner[beach]).is_equal(1)
