class_name Combat
extends RefCounted
## Territory conquest. Troops committed to an attack flood the shared border tile by tile; each tile
## costs terrain x defender density / combat ratio. Naval landings start such an attack from a beach.

static var _nb := PackedInt32Array([0, 0, 0, 0])


static func start_attack(sim: Sim, pid: int, target: int, ratio: int, _tile: int) -> void:
	var p: SimPlayer = sim.players[pid]
	if target == pid or target < 0 or target >= sim.players.size():
		return
	if target != Sim.NONE:
		var d: SimPlayer = sim.players[target]
		if not d.alive or not d.spawned:
			return
		Diplomacy.on_hostile(sim, pid, target)
	var a_rules := sim.rules.section("attack")
	ratio = clampi(ratio, a_rules["min_ratio_permille"], a_rules["max_ratio_permille"])
	var troops: int = p.pop * ratio / 1000
	if troops < 1:
		return
	# Opposing waves meet head-on first: both lose the smaller force.
	if target != Sim.NONE:
		for other in sim.attacks:
			if not other.done and other.attacker == target and other.target == pid:
				var clash: int = mini(other.troops, troops)
				other.troops -= clash
				troops -= clash
				p.pop -= clash
				if other.troops <= 0:
					other.done = true
				sim.emit({"type": "clash", "a": pid, "b": target, "troops": clash})
				break
		if troops <= 0:
			return
	for a in sim.attacks:
		if not a.done and a.attacker == pid and a.target == target:
			a.troops += troops
			p.pop -= troops
			return
	var atk := SimAttack.new()
	atk.uid = sim.uid()
	atk.attacker = pid
	atk.target = target
	_seed_frontier(sim, atk, p)
	if atk.frontier.is_empty():
		return
	atk.troops = troops
	p.pop -= troops
	sim.attacks.append(atk)
	sim.emit({"type": "attack", "a": pid, "b": target, "troops": troops})


static func _seed_frontier(sim: Sim, atk: SimAttack, p: SimPlayer) -> void:
	var w := sim.width
	var x0: int = maxi(0, p.min_x)
	var x1: int = mini(w - 1, p.max_x)
	var y0: int = maxi(0, p.min_y)
	var y1: int = mini(sim.height - 1, p.max_y)
	for y in range(y0, y1 + 1):
		for x in range(x0, x1 + 1):
			var t := y * w + x
			if sim.owner[t] != p.pid:
				continue
			var n := sim.neighbors(t, _nb)
			for k in n:
				var c: int = _nb[k]
				if sim.owner[c] == atk.target and sim.passable(c) and sim.visit[c] != atk.uid:
					sim.visit[c] = atk.uid
					atk.frontier.append(c)


static func retreat(sim: Sim, pid: int, target: int) -> void:
	for a in sim.attacks:
		if not a.done and a.attacker == pid and a.target == target:
			_finish(sim, a)


static func tick(sim: Sim) -> void:
	var r := sim.rules.section("attack")
	for a in sim.attacks:
		if not a.done:
			_advance(sim, a, r)
	var keep := []
	for a in sim.attacks:
		if not a.done:
			keep.append(a)
	sim.attacks = keep
	_boats(sim)


static func _advance(sim: Sim, a: SimAttack, r: Dictionary) -> void:
	var attacker: SimPlayer = sim.players[a.attacker]
	if not attacker.alive or (a.target != Sim.NONE and not sim.players[a.target].alive):
		_finish(sim, a)
		return
	var budget: int = mini(r["max_rate"], r["base_rate"] + a.remaining() / r["frontier_divisor"])
	while budget > 0:
		if a.pos >= a.frontier.size():
			if a.next.is_empty():
				_finish(sim, a)
				return
			a.frontier = a.next
			a.next = PackedInt32Array()
			a.pos = 0
		var t: int = a.frontier[a.pos]
		a.pos += 1
		if sim.owner[t] != a.target or not sim.adjacent_to(t, a.attacker):
			continue
		var cost := tile_cost(sim, t, a.attacker, a.target)
		if a.troops < cost:
			_finish(sim, a)
			return
		a.troops -= cost
		_capture(sim, t, a.attacker, a.target, r)
		budget -= 1
		var n := sim.neighbors(t, _nb)
		for k in n:
			var c: int = _nb[k]
			if sim.owner[c] == a.target and sim.passable(c) and sim.visit[c] != a.uid:
				sim.visit[c] = a.uid
				a.next.append(c)


static func _capture(sim: Sim, t: int, attacker: int, target: int, r: Dictionary) -> void:
	if target != Sim.NONE:
		var d: SimPlayer = sim.players[target]
		var loss: int = mini(d.pop, density(d) * r["defender_loss_permille"] / 1000)
		d.pop -= loss
	sim.set_owner(t, attacker)


static func _finish(sim: Sim, a: SimAttack) -> void:
	if a.done:
		return
	a.done = true
	var p: SimPlayer = sim.players[a.attacker]
	if p.alive:
		p.pop += a.troops
	a.troops = 0


static func density(p: SimPlayer) -> int:
	return p.pop / maxi(1, p.tiles)


## Combat power ratio (permille) of attacker over defender: +25% per era of advantage, capped at +-60%.
static func power_ratio(sim: Sim, attacker: SimPlayer, defender: SimPlayer) -> int:
	var c := sim.rules.section("combat")
	var gap: int = clampi(int(c["per_era_permille"]) * (attacker.era - defender.era), -int(c["max_gap_permille"]), int(c["max_gap_permille"]))
	var perk_value: int = sim.rules.perks[2]["value"]
	return (1000 + gap) * (1000 + perk_value * attacker.perk(2)) / 1000


static func tile_cost(sim: Sim, t: int, attacker: int, target: int) -> int:
	var tr: Dictionary = sim.rules.terrain[sim.terrain[t]]
	var base: int = tr["cost"]
	var a: SimPlayer = sim.players[attacker]
	if target == Sim.NONE:
		var perk_value: int = sim.rules.perks[2]["value"]
		return maxi(1, base * 1000 / (1000 + perk_value * a.perk(2)))
	var d: SimPlayer = sim.players[target]
	var defense: int = tr["defense_permille"]
	defense = defense * fort_multiplier(sim, t, target) / 1000
	if d.capital >= 0:
		var dx: int = d.capital % sim.width - t % sim.width
		var dy: int = d.capital / sim.width - t / sim.width
		if dx * dx + dy * dy <= 9:
			defense = defense * int(sim.rules.section("economy")["capital_defense_permille"]) / 1000
	if d.is_traitor(sim.tick):
		defense = defense * (1000 - int(sim.rules.section("diplomacy")["traitor_defense_permille"])) / 1000
	defense = defense * (1000 + int(sim.rules.perks[3]["value"]) * d.perk(3)) / 1000
	var ratio := power_ratio(sim, a, d)
	var dens: int = density(d) * int(sim.rules.section("attack")["density_permille"]) / 1000
	return base + dens * defense / ratio


static func fort_multiplier(sim: Sim, t: int, pid: int) -> int:
	var fort: Dictionary = sim.rules.buildings[1]
	var radius: int = fort["radius"]
	var tx := t % sim.width
	var ty := t / sim.width
	for b in sim.buildings:
		if not b["alive"] or b["type"] != 1 or b["disabled_until"] > sim.tick or sim.owner[b["tile"]] != pid:
			continue
		var dx: int = b["tile"] % sim.width - tx
		var dy: int = b["tile"] / sim.width - ty
		if dx * dx + dy * dy <= radius * radius:
			return fort["defense_permille"]
	return 1000


# ------------------------------------------------------------------ naval

static func launch_boat(sim: Sim, pid: int, from_tile: int, target_tile: int, ratio: int) -> void:
	var p: SimPlayer = sim.players[pid]
	var n := sim.rules.section("naval")
	var port: Dictionary = sim.rules.buildings[4]
	if p.era < int(port["era"]) or from_tile < 0 or from_tile >= sim.owner.size() or target_tile < 0 or target_tile >= sim.owner.size():
		return
	var b: int = sim.building_at[from_tile]
	if b < 0 or sim.buildings[b]["type"] != 4 or not sim.buildings[b]["alive"] or sim.owner[from_tile] != pid or sim.buildings[b]["disabled_until"] > sim.tick:
		return
	if not sim.passable(target_tile) or sim.owner[target_tile] == pid or not sim.coastal(target_tile):
		return
	var troops: int = p.pop * clampi(ratio, 10, 1000) / 1000
	if troops < int(n["min_troops"]):
		return
	var limit: int = int(n["medieval_range"]) if p.era < int(n["ocean_era"]) else sim.width * sim.height
	var path := water_path(sim, from_tile, target_tile, limit)
	if path.is_empty():
		return
	var target: int = sim.owner[target_tile]
	if target != Sim.NONE:
		Diplomacy.on_hostile(sim, pid, target)
	p.pop -= troops
	sim.boats.append({"uid": sim.uid(), "pid": pid, "target": target_tile, "troops": troops, "path": path, "pos": 0})
	sim.emit({"type": "boat", "pid": pid, "tile": target_tile, "troops": troops})


## Breadth-first path over ocean from the port's coast to the target's coast (tile list, water only).
static func water_path(sim: Sim, from_tile: int, target_tile: int, limit: int) -> PackedInt32Array:
	var size := sim.owner.size()
	var prev := PackedInt32Array()
	prev.resize(size)
	prev.fill(-2)
	var queue := PackedInt32Array()
	var depth := PackedInt32Array()
	var n := sim.neighbors(from_tile, _nb)
	for k in n:
		var c: int = _nb[k]
		if not sim.passable(c):
			prev[c] = -1
			queue.append(c)
			depth.append(1)
	var goal := PackedInt32Array([0, 0, 0, 0])
	var gn := sim.neighbors(target_tile, goal)
	var head := 0
	var found := -1
	while head < queue.size():
		var t: int = queue[head]
		var dd: int = depth[head]
		head += 1
		for k in gn:
			if goal[k] == t:
				found = t
				break
		if found >= 0:
			break
		if dd >= limit:
			continue
		var m := sim.neighbors(t, _nb)
		for k in m:
			var c: int = _nb[k]
			if prev[c] == -2 and not sim.passable(c):
				prev[c] = t
				queue.append(c)
				depth.append(dd + 1)
	if found < 0:
		return PackedInt32Array()
	var path := PackedInt32Array()
	var cur := found
	while cur >= 0:
		path.append(cur)
		cur = prev[cur]
	path.reverse()
	return path


static func _boats(sim: Sim) -> void:
	if sim.boats.is_empty():
		return
	var speed: int = sim.rules.section("naval")["speed_tiles_per_tick"]
	var keep := []
	for b in sim.boats:
		var p: SimPlayer = sim.players[b["pid"]]
		if not p.alive:
			continue
		b["pos"] = int(b["pos"]) + speed
		var path: PackedInt32Array = b["path"]
		if b["pos"] < path.size():
			keep.append(b)
			continue
		_land(sim, p, b["target"], b["troops"])
	sim.boats = keep


static func _land(sim: Sim, p: SimPlayer, t: int, troops: int) -> void:
	var target: int = sim.owner[t]
	if target == p.pid:
		p.pop += troops
		return
	if target != Sim.NONE and (not sim.players[target].alive or Diplomacy.blocks_attack(sim, p.pid, target)):
		p.pop += troops
		return
	var cost := tile_cost(sim, t, p.pid, target)
	if troops < cost:
		sim.emit({"type": "landing_failed", "pid": p.pid, "tile": t})
		return
	troops -= cost
	_capture(sim, t, p.pid, target, sim.rules.section("attack"))
	sim.emit({"type": "landing", "pid": p.pid, "tile": t})
	var atk := SimAttack.new()
	atk.uid = sim.uid()
	atk.attacker = p.pid
	atk.target = target
	var n := sim.neighbors(t, _nb)
	for k in n:
		var c: int = _nb[k]
		if sim.owner[c] == target and sim.passable(c):
			sim.visit[c] = atk.uid
			atk.frontier.append(c)
	if atk.frontier.is_empty():
		p.pop += troops
		return
	atk.troops = troops
	sim.attacks.append(atk)
