class_name Strikes
extends RefCounted
## Late-era weapons: missiles and nukes fly from a silo, hacking and orbital strikes hit instantly.
## Nukes wipe land and cost the launcher trust with every nation (world diplomatic penalty).

const MISSILE := 0
const NUKE := 1
const HACK := 2
const ORBITAL := 3


static func launch(sim: Sim, pid: int, kind: int, tile: int) -> void:
	if kind < 0 or kind >= sim.rules.strikes.size() or tile < 0 or tile >= sim.owner.size():
		return
	var s: Dictionary = sim.rules.strikes[kind]
	var p: SimPlayer = sim.players[pid]
	if p.era < int(s["era"]) or p.gold_milli < int(s["cost"]) * 1000:
		return
	var origin := p.capital
	if s["needs_silo"]:
		origin = -1
		for b in sim.buildings:
			if b["alive"] and b["type"] == Economy.SILO and sim.owner[b["tile"]] == pid and b["disabled_until"] <= sim.tick:
				origin = b["tile"]
				break
		if origin < 0:
			return
	p.gold_milli -= int(s["cost"]) * 1000
	var victim: int = sim.owner[tile]
	if victim != Sim.NONE and victim != pid:
		Diplomacy.on_hostile(sim, pid, victim)
	if s.get("world_penalty", false):
		for other in sim.players:
			if other.pid != 0 and other.kind == SimPlayer.NATION and other.pid != pid:
				other.trust[pid] = maxi(0, other.trust[pid] - 400)
	var speed: int = s["speed_tiles_per_tick"]
	var impact := sim.tick
	if speed > 0 and origin >= 0:
		var dx: int = origin % sim.width - tile % sim.width
		var dy: int = origin / sim.width - tile / sim.width
		impact += maxi(1, isqrt(dx * dx + dy * dy) / speed)
	sim.emit({"type": "strike_launch", "pid": pid, "kind": kind, "tile": tile, "origin": origin, "impact": impact})
	if impact == sim.tick:
		resolve(sim, pid, kind, tile)
	else:
		sim.strikes.append({"kind": kind, "pid": pid, "tile": tile, "impact": impact})


static func isqrt(v: int) -> int:
	if v <= 0:
		return 0
	var x := v
	var y := (x + 1) / 2
	while y < x:
		x = y
		y = (x + v / x) / 2
	return x


static func tick(sim: Sim) -> void:
	if sim.strikes.is_empty():
		return
	var keep := []
	for s in sim.strikes:
		if sim.tick >= s["impact"]:
			resolve(sim, s["pid"], s["kind"], s["tile"])
		else:
			keep.append(s)
	sim.strikes = keep


static func resolve(sim: Sim, pid: int, kind: int, tile: int) -> void:
	var s: Dictionary = sim.rules.strikes[kind]
	var r: int = s["radius"]
	var cx := tile % sim.width
	var cy := tile / sim.width
	var hit := {}  # owner -> tiles in blast (read in sorted order below)
	for dy in range(-r, r + 1):
		for dx in range(-r, r + 1):
			var x := cx + dx
			var y := cy + dy
			if dx * dx + dy * dy > r * r or x < 0 or y < 0 or x >= sim.width or y >= sim.height:
				continue
			var t := sim.idx(x, y)
			var o: int = sim.owner[t]
			var b: int = sim.building_at[t]
			if kind == HACK:
				if b >= 0 and o != pid:
					sim.buildings[b]["disabled_until"] = sim.tick + int(s["disable_ticks"])
				continue
			if b >= 0:
				Economy.destroy_building(sim, b)
			if o != Sim.NONE:
				hit[o] = int(hit.get(o, 0)) + 1
			if s.get("wipes_land", false) and o != Sim.NONE:
				sim.set_owner(t, Sim.NONE)
	var owners := hit.keys()
	owners.sort()
	for o in owners:
		var victim: SimPlayer = sim.players[o]
		var tiles_hit: int = hit[o]
		var before_tiles: int = victim.tiles + (tiles_hit if s.get("wipes_land", false) else 0)
		var dens: int = victim.pop / maxi(1, before_tiles)
		victim.pop -= mini(victim.pop, dens * tiles_hit * int(s.get("kill_permille", 0)) / 1000)
	sim.emit({"type": "strike", "pid": pid, "kind": kind, "tile": tile})
