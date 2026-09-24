class_name Economy
extends RefCounted
## Population growth, gold income, buildings, era gauges and perk choices.

const CITY := 0
const FORT := 1
const MARKET := 2
const LAB := 3
const PORT := 4
const SILO := 5
# Perk ids (data/rules.json "perks").
const P_GROWTH := 0
const P_GOLD := 1
const P_ATTACK := 2
const P_DEFENSE := 3
const P_BUILD := 4
const P_GAUGE := 5
const P_CAP := 6


## Counts of working (alive, not hacked) buildings per player and type: counts[pid][type].
static func building_counts(sim: Sim) -> Array:
	var counts := []
	for p in sim.players:
		var row := PackedInt32Array()
		row.resize(sim.rules.buildings.size())
		counts.append(row)
	for b in sim.buildings:
		if not b["alive"] or b["disabled_until"] > sim.tick:
			continue
		var o: int = sim.owner[b["tile"]]
		if o != Sim.NONE:
			counts[o][b["type"]] += 1
	return counts


static func bonus(sim: Sim, p: SimPlayer) -> int:
	if p.kind != SimPlayer.NATION:
		return 0
	return int(sim.rules.difficulty[clampi(p.difficulty, 0, sim.rules.difficulty.size() - 1)]["bonus_permille"])


static func tick(sim: Sim) -> void:
	var counts := building_counts(sim)
	var econ := sim.rules.section("economy")
	var perks := sim.rules.perks
	var city_bonus: int = sim.rules.buildings[CITY]["cap_bonus"]
	var market_gold: int = sim.rules.buildings[MARKET]["gold_milli"]
	for p in sim.players:
		if p.pid == 0 or not p.alive or not p.spawned:
			continue
		var era: Dictionary = sim.rules.eras[p.era]
		var c: PackedInt32Array = counts[p.pid]
		var cap: int = p.tiles * int(era["cap_per_tile"]) * (1000 + int(perks[P_CAP]["value"]) * p.perk(P_CAP)) / 1000 + c[CITY] * city_bonus
		p.cap = maxi(cap, 1)
		var extra := bonus(sim, p)
		if p.pop < p.cap:
			var g: int = p.pop * int(era["growth_permille"]) / 1000 * (p.cap - p.pop) / p.cap
			g = g * (1000 + int(perks[P_GROWTH]["value"]) * p.perk(P_GROWTH) + extra) / 1000
			p.pop = mini(p.cap, p.pop + maxi(int(econ["min_growth"]), g))
		elif p.pop > p.cap:
			p.pop -= maxi(1, (p.pop - p.cap) / 50)
		var trade := Diplomacy.count_treaties(sim, p.pid, Diplomacy.TRADE) * int(sim.rules.treaties[Diplomacy.TRADE]["value"])
		var income: int = p.tiles * int(econ["gold_per_tile_milli"]) * (1000 + int(perks[P_GOLD]["value"]) * p.perk(P_GOLD) + trade + extra) / 1000
		income += c[MARKET] * market_gold
		p.income_milli = income
		p.gold_milli += income
	# Tribute moves a share of the payer's income to the receiver.
	var share: int = sim.rules.treaties[Diplomacy.TRIBUTE]["value"]
	for t in sim.treaties:
		if t["type"] != Diplomacy.TRIBUTE:
			continue
		var payer: SimPlayer = sim.players[t["a"]]
		var receiver: SimPlayer = sim.players[t["b"]]
		var amount: int = mini(payer.gold_milli, payer.income_milli * share / 1000)
		payer.gold_milli -= amount
		receiver.gold_milli += amount


static func building_cost(sim: Sim, pid: int, kind: int) -> int:
	var b: Dictionary = sim.rules.buildings[kind]
	var owned := sim.buildings_of(pid, kind)
	var growth: int = sim.rules.data["building_cost_growth_permille"]
	var p: SimPlayer = sim.players[pid]
	var discount: int = mini(600, int(sim.rules.perks[P_BUILD]["value"]) * p.perk(P_BUILD))
	return int(b["cost"]) * (1000 + growth * owned) / 1000 * (1000 - discount) / 1000


static func can_build(sim: Sim, pid: int, kind: int, tile: int) -> bool:
	if kind < 0 or kind >= sim.rules.buildings.size() or tile < 0 or tile >= sim.owner.size():
		return false
	var b: Dictionary = sim.rules.buildings[kind]
	var p: SimPlayer = sim.players[pid]
	if p.era < int(b["era"]) or sim.owner[tile] != pid or sim.building_at[tile] >= 0 or not sim.passable(tile):
		return false
	if b.get("coastal", false) and not sim.coastal(tile):
		return false
	return p.gold_milli >= building_cost(sim, pid, kind) * 1000


static func build(sim: Sim, pid: int, kind: int, tile: int) -> void:
	if not can_build(sim, pid, kind, tile):
		return
	var cost := building_cost(sim, pid, kind)
	sim.players[pid].gold_milli -= cost * 1000
	sim.building_at[tile] = sim.buildings.size()
	sim.buildings.append({"type": kind, "tile": tile, "alive": true, "disabled_until": 0})
	sim.emit({"type": "build", "pid": pid, "kind": kind, "tile": tile})


static func destroy_building(sim: Sim, index: int) -> void:
	var b: Dictionary = sim.buildings[index]
	if not b["alive"]:
		return
	b["alive"] = false
	sim.building_at[b["tile"]] = -1


# ------------------------------------------------------------------ eras

static func eras(sim: Sim) -> void:
	var g := sim.rules.section("era_gauge")
	var last: int = sim.rules.eras.size() - 1
	var world := sim.world_era()
	var lowest := last
	for p in sim.players:
		if p.pid != 0 and p.alive and p.spawned:
			lowest = mini(lowest, p.era)
	var counts := building_counts(sim)
	var lab_gauge: int = sim.rules.buildings[LAB]["gauge"]
	var share: int = sim.rules.treaties[Diplomacy.TECHSHARE]["value"]
	for p in sim.players:
		if p.pid == 0 or not p.alive or not p.spawned:
			continue
		if p.perk_options.size() > 0 and sim.tick >= p.perk_deadline:
			choose_perk(sim, p.pid, 0)
		if p.era >= last:
			continue
		var gain: int = int(g["base"]) + mini(int(g["tiles_cap"]), p.tiles / int(g["tiles_divisor"])) + counts[p.pid][LAB] * lab_gauge
		var mult: int = 1000 + int(sim.rules.perks[P_GAUGE]["value"]) * p.perk(P_GAUGE) + bonus(sim, p)
		mult += Diplomacy.count_treaties(sim, p.pid, Diplomacy.TECHSHARE) * share
		if p.era == lowest and world > lowest:
			mult += int(g["lowest_bonus_permille"])
		if world - p.era >= 2:
			mult += int(g["behind_two_bonus_permille"])
		p.gauge += gain * mult / 1000
		var need: int = sim.rules.eras[p.era]["gauge_to_next"]
		if p.gauge >= need:
			p.gauge -= need
			p.era += 1
			_offer_perks(sim, p, int(g["perk_timeout_ticks"]))
			sim.emit({"type": "era", "pid": p.pid, "era": p.era})


static func _offer_perks(sim: Sim, p: SimPlayer, timeout: int) -> void:
	# A pending choice from the previous era is taken automatically first.
	if p.perk_options.size() > 0:
		choose_perk(sim, p.pid, 0)
	var pool := PackedInt32Array()
	for i in sim.rules.perks.size():
		pool.append(i)
	var options := PackedInt32Array()
	for k in mini(3, pool.size()):
		var j: int = sim.rng.below(pool.size())
		options.append(pool[j])
		pool.remove_at(j)
	p.perk_options = options
	p.perk_deadline = sim.tick + timeout


static func choose_perk(sim: Sim, pid: int, choice: int) -> void:
	var p: SimPlayer = sim.players[pid]
	if p.perk_options.is_empty() or choice < 0 or choice >= p.perk_options.size():
		return
	var perk_id: int = p.perk_options[choice]
	p.perks[perk_id] += 1
	p.perk_options = PackedInt32Array()
	sim.emit({"type": "perk", "pid": pid, "perk": perk_id})
