class_name Diplomacy
extends RefCounted
## Treaties, proposals, betrayal ("traitor"), eliminations and victory.
## Betrayal is allowed but costly: attacking a treaty partner breaks every treaty between the two
## and brands the attacker a traitor (defense -20%, AI trust drop, proposal cooldown).

const NAP := 0
const ALLIANCE := 1
const CEASEFIRE := 2
const TRADE := 3
const TRIBUTE := 4
const TECHSHARE := 5


static func between(t: Dictionary, a: int, b: int) -> bool:
	return (t["a"] == a and t["b"] == b) or (t["a"] == b and t["b"] == a)


static func find(sim: Sim, a: int, b: int, type: int) -> Dictionary:
	for t in sim.treaties:
		if t["type"] == type and between(t, a, b):
			return t
	return {}


static func has(sim: Sim, a: int, b: int, type: int) -> bool:
	return not find(sim, a, b, type).is_empty()


static func allied(sim: Sim, a: int, b: int) -> bool:
	return a == b or has(sim, a, b, ALLIANCE)


static func blocks_attack(sim: Sim, a: int, b: int) -> bool:
	for t in sim.treaties:
		if t["type"] <= CEASEFIRE and between(t, a, b):
			return true
	return false


static func count_treaties(sim: Sim, pid: int, type: int) -> int:
	var n := 0
	for t in sim.treaties:
		if t["type"] == type and (t["a"] == pid or t["b"] == pid):
			n += 1
	return n


static func _adjust_trust(sim: Sim, about: int, amount: int, only: int = -1) -> void:
	for p in sim.players:
		if p.pid == 0 or p.kind != SimPlayer.NATION or p.pid == about:
			continue
		if only >= 0 and p.pid != only:
			continue
		p.trust[about] = clampi(p.trust[about] + amount, 0, 2000)


## Called before any hostile act (attack, landing, strike). Returns true when it was a betrayal.
static func on_hostile(sim: Sim, attacker: int, target: int) -> bool:
	if target == Sim.NONE or target == attacker:
		return false
	var betrayal := blocks_attack(sim, attacker, target)
	var keep := []
	for t in sim.treaties:
		if not between(t, attacker, target):
			keep.append(t)
	sim.treaties = keep
	var pending := []
	for pr in sim.proposals:
		if not ((pr["from"] == attacker and pr["to"] == target) or (pr["from"] == target and pr["to"] == attacker)):
			pending.append(pr)
	sim.proposals = pending
	# Every hostile act lowers the victim's trust; betrayal alarms every nation.
	_adjust_trust(sim, attacker, -120, target)
	if betrayal:
		var d := sim.rules.section("diplomacy")
		sim.players[attacker].traitor_until = sim.tick + int(d["traitor_ticks"])
		_adjust_trust(sim, attacker, -300)
		_adjust_trust(sim, attacker, -300, target)
		sim.emit({"type": "betrayal", "a": attacker, "b": target})
	return betrayal


static func propose(sim: Sim, from: int, to: int, type: int) -> void:
	if type < 0 or type >= sim.rules.treaties.size() or to == from:
		return
	var target := sim.player(to)
	var p: SimPlayer = sim.players[from]
	if target == null or not target.alive or not target.spawned:
		return
	var d := sim.rules.section("diplomacy")
	if p.is_traitor(sim.tick) and sim.tick - p.last_proposal < int(d["proposal_cooldown"]):
		return
	if has(sim, from, to, type) or (type == NAP and has(sim, from, to, ALLIANCE)):
		return
	var mine := 0
	for pr in sim.proposals:
		if pr["from"] == from:
			mine += 1
			if pr["to"] == to and pr["type"] == type:
				return
	if mine >= int(d["max_pending"]):
		return
	p.last_proposal = sim.tick
	var pr := {"id": sim.uid(), "type": type, "from": from, "to": to, "expires": sim.tick + int(d["proposal_ticks"])}
	sim.proposals.append(pr)
	sim.emit({"type": "proposal", "id": pr["id"], "treaty": type, "a": from, "b": to})


static func respond(sim: Sim, pid: int, id: int, accept: bool) -> void:
	for i in sim.proposals.size():
		var pr: Dictionary = sim.proposals[i]
		if pr["id"] != id or pr["to"] != pid:
			continue
		sim.proposals.remove_at(i)
		if accept and sim.players[pr["from"]].alive:
			sign_treaty(sim, pr["type"], pr["from"], pr["to"])
		else:
			sim.emit({"type": "rejected", "treaty": pr["type"], "a": pr["from"], "b": pr["to"]})
		return


static func sign_treaty(sim: Sim, type: int, a: int, b: int) -> void:
	var rule: Dictionary = sim.rules.treaties[type]
	if type == ALLIANCE:
		sim.treaties = sim.treaties.filter(func(t): return not (t["type"] == NAP and between(t, a, b)))
	if type == CEASEFIRE or type == ALLIANCE:
		for atk in sim.attacks:
			if not atk.done and ((atk.attacker == a and atk.target == b) or (atk.attacker == b and atk.target == a)):
				Combat._finish(sim, atk)
	var end: int = sim.tick + int(rule["duration"]) if int(rule["duration"]) > 0 else 0
	# Tribute is directional: a (the proposer) pays b.
	sim.treaties.append({"id": sim.uid(), "type": type, "a": a, "b": b, "end": end, "notice_end": 0})
	_adjust_trust(sim, a, 60, b)
	_adjust_trust(sim, b, 60, a)
	sim.emit({"type": "treaty", "treaty": type, "a": a, "b": b})


static func break_treaty(sim: Sim, pid: int, id: int) -> void:
	for i in sim.treaties.size():
		var t: Dictionary = sim.treaties[i]
		if t["id"] != id or (t["a"] != pid and t["b"] != pid):
			continue
		if t["type"] == CEASEFIRE:
			return  # a ceasefire can only be broken by attacking (betrayal)
		if t["type"] == NAP:
			if t["notice_end"] == 0:
				t["notice_end"] = sim.tick + int(sim.rules.treaties[NAP]["notice"])
				sim.emit({"type": "nap_notice", "a": pid, "b": t["b"] if t["a"] == pid else t["a"], "until": t["notice_end"]})
			return
		sim.treaties.remove_at(i)
		sim.emit({"type": "treaty_ended", "treaty": t["type"], "a": t["a"], "b": t["b"], "by": pid})
		return


static func donate(sim: Sim, pid: int, to: int, troops: int, gold: int) -> void:
	var p: SimPlayer = sim.players[pid]
	var target := sim.player(to)
	if target == null or not target.alive or not allied(sim, pid, to) or to == pid:
		return
	troops = clampi(troops, 0, p.pop)
	var gold_milli: int = clampi(gold, 0, p.gold) * 1000
	p.pop -= troops
	target.pop += troops
	p.gold_milli -= gold_milli
	target.gold_milli += gold_milli
	if troops > 0 or gold_milli > 0:
		sim.emit({"type": "donate", "a": pid, "b": to, "troops": troops, "gold": gold_milli / 1000})


static func surrender(_sim: Sim, _pid: int) -> void:
	pass


static func tick(sim: Sim) -> void:
	var keep := []
	for t in sim.treaties:
		var ended: bool = (t["end"] > 0 and sim.tick >= t["end"]) or (t["notice_end"] > 0 and sim.tick >= t["notice_end"])
		if ended or not sim.players[t["a"]].alive or not sim.players[t["b"]].alive:
			sim.emit({"type": "treaty_ended", "treaty": t["type"], "a": t["a"], "b": t["b"], "by": 0})
		else:
			keep.append(t)
	sim.treaties = keep
	var pending := []
	for pr in sim.proposals:
		if sim.tick < pr["expires"] and sim.players[pr["to"]].alive:
			pending.append(pr)
		else:
			sim.emit({"type": "expired", "treaty": pr["type"], "a": pr["from"], "b": pr["to"]})
	sim.proposals = pending
	# Kept promises slowly rebuild trust; memories fade toward neutral.
	if sim.tick % 100 == 0:
		for p in sim.players:
			if p.pid == 0 or p.kind != SimPlayer.NATION:
				continue
			for other in sim.players.size():
				if other == p.pid or other == 0:
					continue
				var v: int = p.trust[other]
				if blocks_attack(sim, p.pid, other) or has(sim, p.pid, other, TRADE):
					v += 8
				v += signi(1000 - v) * 2
				p.trust[other] = clampi(v, 0, 2000)


static func eliminations(sim: Sim) -> void:
	if sim.in_spawn_phase():
		return
	for p in sim.players:
		if p.pid == 0 or not p.alive or p.tiles > 0:
			continue
		p.alive = false
		p.eliminated_tick = sim.tick
		p.perk_options = PackedInt32Array()
		for a in sim.attacks:
			if a.attacker == p.pid:
				a.done = true
		sim.emit({"type": "eliminated", "pid": p.pid})


## Alliance groups (connected components of alliance treaties) among living players, sorted.
static func groups(sim: Sim) -> Array:
	var root := PackedInt32Array()
	root.resize(sim.players.size())
	for i in root.size():
		root[i] = i
	for t in sim.treaties:
		if t["type"] == ALLIANCE:
			var ra := _find(root, t["a"])
			var rb := _find(root, t["b"])
			root[maxi(ra, rb)] = mini(ra, rb)
	var by_root := {}
	for p in sim.players:
		if p.pid == 0 or not p.alive:
			continue
		var r := _find(root, p.pid)
		if not by_root.has(r):
			by_root[r] = PackedInt32Array()
		by_root[r].append(p.pid)
	var keys := by_root.keys()
	keys.sort()
	var out := []
	for k in keys:
		out.append(by_root[k])
	return out


static func _find(root: PackedInt32Array, i: int) -> int:
	while root[i] != i:
		i = root[i]
	return i


static func check_victory(sim: Sim) -> void:
	if sim.over or sim.in_spawn_phase():
		return
	var need: int = sim.land_total * int(sim.rules.section("victory")["land_permille"]) / 1000
	var best := PackedInt32Array()
	var best_tiles := -1
	var all_groups := groups(sim)
	for g in all_groups:
		var total := 0
		for pid in g:
			total += sim.players[pid].tiles
		if total >= need or all_groups.size() == 1:
			_finish(sim, g, "domination")
			return
		if total > best_tiles:
			best_tiles = total
			best = g
	if sim.tick >= sim.duration_ticks and best.size() > 0:
		_finish(sim, best, "time")


static func _finish(sim: Sim, group: PackedInt32Array, reason: String) -> void:
	sim.over = true
	sim.winners = group
	sim.emit({"type": "game_over", "winners": group, "reason": reason})
