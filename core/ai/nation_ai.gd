class_name NationAI
extends RefCounted
## Utility AI for computer nations (design.md 7.1). Every think it scores expand / attack / build /
## strike / sail / diplomacy options with its personality weights and picks the best one; difficulty
## sets the think interval and the chance of taking the second-best option. Proposals are judged by
## power ratio, a shared stronger rival, trust memory and personality. Output is ordinary intents.

const CONQUEROR := 0
const MERCHANT := 1
const COWARD := 2
const OPPORTUNIST := 3
const PERK_ORDER := [[2, 0, 6, 3, 5, 1, 4], [1, 4, 5, 0, 6, 3, 2], [3, 6, 0, 5, 1, 4, 2], [2, 5, 1, 0, 6, 3, 4]]

var decisions: int = 0


func to_dict() -> Dictionary:
	return {"decisions": decisions}


func from_dict(d: Dictionary) -> void:
	decisions = int(d.get("decisions", 0))


func think(sim: Sim) -> void:
	if sim.in_spawn_phase():
		return
	for pr in sim.proposals:
		var to: SimPlayer = sim.players[pr["to"]]
		if to.kind == SimPlayer.NATION and to.alive and (sim.tick + to.pid) % 20 == 0:
			var yes := judge(sim, to, pr["from"], pr["type"])
			sim.ai_queue.append({"type": "respond", "p": to.pid, "id": pr["id"], "accept": yes})
			sim.emit({"type": "ai_say", "pid": to.pid, "to": pr["from"], "key": ("accept_" if yes else "reject_") + str(pr["type"])})
	for p in sim.players:
		if p.pid == 0 or p.kind != SimPlayer.NATION or not p.alive or not p.spawned:
			continue
		var diff: Dictionary = sim.rules.difficulty[clampi(p.difficulty, 0, sim.rules.difficulty.size() - 1)]
		if p.perk_options.size() > 0:
			sim.ai_queue.append({"type": "perk", "p": p.pid, "choice": _perk_choice(p)})
		if (sim.tick + p.pid * 13) % int(diff["interval"]) != 0:
			continue
		_act(sim, p, int(diff["mistake_permille"]))


func _profile(sim: Sim, p: SimPlayer) -> Dictionary:
	return sim.rules.ai_profiles[clampi(p.profile, 0, sim.rules.ai_profiles.size() - 1)]


func _perk_choice(p: SimPlayer) -> int:
	var order: Array = PERK_ORDER[clampi(p.profile, 0, PERK_ORDER.size() - 1)]
	for want in order:
		var i := p.perk_options.find(want)
		if i >= 0:
			return i
	return 0


## Strength = home troops plus a territory term, so big empires count even when their army is out.
static func strength(p: SimPlayer) -> int:
	return p.pop + p.tiles * 4


func judge(sim: Sim, me: SimPlayer, from: int, type: int) -> bool:
	var them: SimPlayer = sim.players[from]
	var prof := _profile(sim, me)
	var trust: int = me.trust[from]
	if them.is_traitor(sim.tick) or trust < 500:
		return false
	var ratio: int = strength(them) * 1000 / maxi(1, strength(me))  # >1000: they are stronger
	var rival := _stronger_rival(sim, me, them)
	var diplomacy: int = prof["diplomacy"]
	match type:
		Diplomacy.NAP:
			return ratio * diplomacy / 1000 >= 600 or trust >= 1200
		Diplomacy.ALLIANCE:
			return trust >= 950 and (rival or ratio >= 900) and diplomacy >= 700
		Diplomacy.CEASEFIRE:
			return ratio >= 800 or trust >= 1100
		Diplomacy.TRADE:
			return trust >= 800 and diplomacy >= 900
		Diplomacy.TRIBUTE:
			return true  # the proposer pays us
		Diplomacy.TECHSHARE:
			return trust >= 950 and absi(them.era - me.era) <= 1
	return false


## Is there a living player stronger than both of us? (a common threat makes alliances attractive)
func _stronger_rival(sim: Sim, a: SimPlayer, b: SimPlayer) -> bool:
	var top := maxi(strength(a), strength(b))
	for p in sim.players:
		if p.pid != 0 and p.alive and p.pid != a.pid and p.pid != b.pid and strength(p) > top:
			return true
	return false


func _act(sim: Sim, p: SimPlayer, mistake: int) -> void:
	decisions += 1
	var prof := _profile(sim, p)
	var fill: int = p.pop * 1000 / maxi(1, p.cap)
	var info := sim.border_info(p.pid)
	var options := []  # [score, intent]
	var ratio := 500 if p.profile == CONQUEROR or p.profile == OPPORTUNIST else 350
	if info["wild"] and fill >= 350:
		options.append([int(prof["expand"]) * fill / 1000 * 3, {"type": "attack", "p": p.pid, "target": 0, "ratio": ratio}])
	for n in info["neighbors"]:
		var other: SimPlayer = sim.players[n]
		var power: int = strength(p) * 1000 / maxi(1, strength(other))
		# Denser armies attack; a capped empire also pushes into any clearly weaker neighbour.
		if fill < 500 or (Combat.density(p) <= Combat.density(other) and not (fill >= 900 and power >= 1100)):
			continue
		var score: int = int(prof["attack_weak"]) * mini(power, 4000) / 1000 * (2000 - p.trust[n]) / 1000
		if other.is_traitor(sim.tick):
			score = score * 13 / 10
		if Diplomacy.blocks_attack(sim, p.pid, n):
			# Betrayal only when overwhelmingly stronger, weighted by how little the personality values promises.
			if power < int(prof["betray_ratio_permille"]):
				continue
			score = score * (1500 - int(prof["keep_treaty"])) / 1500
		if score > 0:
			options.append([score, {"type": "attack", "p": p.pid, "target": n, "ratio": ratio + 100}])
	_build_option(sim, p, prof, fill, options)
	_strike_option(sim, p, info, options)
	_boat_option(sim, p, info, fill, options)
	_diplomacy_option(sim, p, prof, info, options)
	if options.is_empty():
		return
	options.sort_custom(func(x, y): return x[0] > y[0] or (x[0] == y[0] and str(x[1]) < str(y[1])))
	var pick: Array = options[0]
	if options.size() > 1 and sim.rng.chance(mistake):
		pick = options[1]
	sim.ai_queue.append(pick[1])


func _free_tile(sim: Sim, p: SimPlayer, need_coast: bool) -> int:
	if p.capital >= 0 and sim.building_at[p.capital] < 0 and not need_coast:
		return p.capital
	var w: int = p.max_x - p.min_x + 1
	var h: int = p.max_y - p.min_y + 1
	if w <= 0 or h <= 0:
		return -1
	for attempt in 40:
		var t := sim.idx(p.min_x + sim.rng.below(w), p.min_y + sim.rng.below(h))
		if sim.owner[t] == p.pid and sim.building_at[t] < 0 and (not need_coast or sim.coastal(t)):
			return t
	return -1


func _build_option(sim: Sim, p: SimPlayer, prof: Dictionary, fill: int, options: Array) -> void:
	var kind := -1
	var world := sim.world_era()
	var threatened := false
	for a in sim.attacks:
		if a.target == p.pid:
			threatened = true
	if p.era >= 6 and sim.buildings_of(p.pid, Economy.SILO) == 0:
		kind = Economy.SILO
	elif p.era >= 3 and sim.buildings_of(p.pid, Economy.PORT) == 0 and _free_tile(sim, p, true) >= 0:
		kind = Economy.PORT
	elif threatened and sim.buildings_of(p.pid, Economy.FORT) < 3:
		kind = Economy.FORT
	elif fill > 850:
		kind = Economy.CITY
	elif world > p.era:
		kind = Economy.LAB
	elif p.income_milli < 6000:
		kind = Economy.MARKET
	else:
		kind = [Economy.CITY, Economy.MARKET, Economy.LAB][sim.tick / 97 % 3]
	var tile := _free_tile(sim, p, kind == Economy.PORT)
	if tile >= 0 and Economy.can_build(sim, p.pid, kind, tile):
		options.append([int(prof["build"]) * 2, {"type": "build", "p": p.pid, "kind": kind, "tile": tile}])


func _strike_option(sim: Sim, p: SimPlayer, info: Dictionary, options: Array) -> void:
	if p.era < 6 or info["neighbors"].is_empty():
		return
	var target := -1
	var best := -1
	for n in info["neighbors"]:
		var other: SimPlayer = sim.players[n]
		if Diplomacy.blocks_attack(sim, p.pid, n) or other.capital < 0:
			continue
		if strength(other) > best:
			best = strength(other)
			target = n
	if target < 0:
		return
	var kind := Strikes.MISSILE
	if p.profile == CONQUEROR and p.gold_milli >= int(sim.rules.strikes[Strikes.NUKE]["cost"]) * 1200:
		kind = Strikes.NUKE
	elif p.era >= 7 and p.gold_milli >= int(sim.rules.strikes[Strikes.ORBITAL]["cost"]) * 1500:
		kind = Strikes.ORBITAL
	if p.gold_milli < int(sim.rules.strikes[kind]["cost"]) * 1500:
		return
	options.append([900, {"type": "strike", "p": p.pid, "kind": kind, "tile": sim.players[target].capital}])


func _boat_option(sim: Sim, p: SimPlayer, info: Dictionary, fill: int, options: Array) -> void:
	if info["wild"] or fill < 700 or p.era < 3:
		return
	var port := -1
	for b in sim.buildings:
		if b["alive"] and b["type"] == Economy.PORT and sim.owner[b["tile"]] == p.pid:
			port = b["tile"]
			break
	if port < 0:
		return
	# Weakest reachable non-partner by strength; land on one of its coastal tiles.
	var target: SimPlayer = null
	for other in sim.players:
		if other.pid == 0 or other.pid == p.pid or not other.alive or not other.spawned or Diplomacy.blocks_attack(sim, p.pid, other.pid):
			continue
		if target == null or strength(other) < strength(target):
			target = other
	if target == null or strength(target) * 2 > strength(p):
		return
	for attempt in 30:
		var w: int = target.max_x - target.min_x + 1
		var h: int = target.max_y - target.min_y + 1
		var t := sim.idx(target.min_x + sim.rng.below(maxi(1, w)), target.min_y + sim.rng.below(maxi(1, h)))
		if sim.owner[t] == target.pid and sim.coastal(t):
			options.append([700, {"type": "boat", "p": p.pid, "from": port, "tile": t, "ratio": 450}])
			return


func _diplomacy_option(sim: Sim, p: SimPlayer, prof: Dictionary, info: Dictionary, options: Array) -> void:
	if info["neighbors"].is_empty() or p.is_traitor(sim.tick):
		return
	var strongest := -1
	var top := -1
	for n in info["neighbors"]:
		var s := strength(sim.players[n])
		if s > top:
			top = s
			strongest = n
	var diplomacy: int = prof["diplomacy"]
	if top > strength(p) and not Diplomacy.blocks_attack(sim, p.pid, strongest):
		options.append([diplomacy * 2, {"type": "propose", "p": p.pid, "to": strongest, "treaty": Diplomacy.NAP}])
	for n in info["neighbors"]:
		if n == strongest or p.trust[n] < 1100 or Diplomacy.has(sim, p.pid, n, Diplomacy.ALLIANCE):
			continue
		options.append([diplomacy * p.trust[n] / 1000, {"type": "propose", "p": p.pid, "to": n, "treaty": Diplomacy.ALLIANCE}])
		break
	if p.profile == MERCHANT:
		for n in info["neighbors"]:
			if not Diplomacy.has(sim, p.pid, n, Diplomacy.TRADE) and p.trust[n] >= 900:
				options.append([diplomacy, {"type": "propose", "p": p.pid, "to": n, "treaty": Diplomacy.TRADE}])
				break
