class_name Sim
extends RefCounted
## Deterministic Blitz simulation. Input: intents applied at tick boundaries. Output: state + events.
## Never touches Nodes, time, rendering or the network (AGENTS.md R1/R2).

const NONE := 0
const MAX_PLAYERS := 250

var rules: Ruleset
var width: int
var height: int
var terrain: PackedByteArray
var owner: PackedByteArray
var building_at: PackedInt32Array      # tile -> building index or -1
var visit: PackedInt32Array            # tile -> last attack uid that queued it
var players: Array = []                # index = pid; players[0] is a placeholder
var buildings: Array = []              # {type, tile, alive, disabled_until}
var attacks: Array = []                # SimAttack
var boats: Array = []                  # {uid, pid, target, troops, path, pos}
var strikes: Array = []                # {kind, pid, tile, impact_tick}
var treaties: Array = []               # {id, type, a, b, end, notice_end}
var proposals: Array = []              # {id, type, from, to, expires}
var tick: int = 0
var rng: SimRng
var seed_value: int
var tile_hash: int = 0
var land_total: int = 0
var next_uid: int = 1
var duration_ticks: int = 12000
var over: bool = false
var winners: PackedInt32Array = PackedInt32Array()
var events: Array = []                 # {tick, type, ...} appended; consumers keep a cursor
var event_base: int = 0                # index of events[0] in the global event stream
var dirty: PackedInt32Array = PackedInt32Array()
var ai_queue: Array = []               # intents produced by AI, applied at the next tick
var bot_brain: BotAI
var nation_brain: NationAI


## config: {seed, width?, height?, minutes?, players:[{name, kind, difficulty?, profile?}]}
static func create(config: Dictionary, rule_set: Ruleset = null) -> Sim:
	var s := Sim.new()
	s.rules = rule_set if rule_set != null else Ruleset.load_default()
	s.seed_value = int(config.get("seed", 1))
	s.rng = SimRng.new(s.seed_value)
	var map_rules := s.rules.section("map")
	s.width = int(config.get("width", map_rules["width"]))
	s.height = int(config.get("height", map_rules["height"]))
	s.duration_ticks = int(config.get("minutes", s.rules.section("victory")["default_minutes"])) * 60 * s.rules.data["tick_rate"]
	s.terrain = MapGen.generate(s.seed_value, s.width, s.height, map_rules["land_permille"])
	var n := s.width * s.height
	if config.get("flat", false):
		# Test/sandbox maps: all plains, or plains with an ocean band at x in [ocean_x0, ocean_x1).
		s.terrain.fill(MapGen.PLAINS)
		var ox0 := int(config.get("ocean_x0", -1))
		var ox1 := int(config.get("ocean_x1", -1))
		for y in s.height:
			for x in range(maxi(ox0, 0), maxi(ox1, 0)):
				s.terrain[y * s.width + x] = MapGen.OCEAN
	s.owner = PackedByteArray(); s.owner.resize(n)
	s.building_at = PackedInt32Array(); s.building_at.resize(n); s.building_at.fill(-1)
	s.visit = PackedInt32Array(); s.visit.resize(n)
	for t in s.terrain:
		if t != MapGen.OCEAN:
			s.land_total += 1
	s.players.append(SimPlayer.new(0, s.rules.perks.size()))
	var list: Array = config.get("players", [])
	for i in mini(list.size(), MAX_PLAYERS):
		var pc: Dictionary = list[i]
		var p := SimPlayer.new(i + 1, s.rules.perks.size())
		p.name = str(pc.get("name", "P%d" % (i + 1)))
		p.kind = int(pc.get("kind", SimPlayer.HUMAN))
		p.difficulty = int(pc.get("difficulty", 1))
		p.profile = int(pc.get("profile", 0))
		p.pop = s.rules.section("spawn")["pop"]
		p.gold_milli = int(s.rules.section("spawn")["gold"]) * 1000
		s.players.append(p)
	for p in s.players:
		p.trust.resize(s.players.size())
		p.trust.fill(1000)
	s.bot_brain = BotAI.new()
	s.nation_brain = NationAI.new()
	# Computer players take their place immediately; humans pick during the spawn phase.
	for p in s.players:
		if p.pid != 0 and p.kind != SimPlayer.HUMAN:
			s.spawn_random(p)
	return s


# ------------------------------------------------------------------ tiles

func idx(x: int, y: int) -> int:
	return y * width + x


func passable(t: int) -> bool:
	return terrain[t] != MapGen.OCEAN


## 4-neighbours of tile t written into out (returns count). No wrap-around.
func neighbors(t: int, out: PackedInt32Array) -> int:
	var x := t % width
	var n := 0
	if x > 0:
		out[n] = t - 1
		n += 1
	if x < width - 1:
		out[n] = t + 1
		n += 1
	if t >= width:
		out[n] = t - width
		n += 1
	if t < width * (height - 1):
		out[n] = t + width
		n += 1
	return n


func adjacent_to(t: int, pid: int) -> bool:
	var x := t % width
	if x > 0 and owner[t - 1] == pid: return true
	if x < width - 1 and owner[t + 1] == pid: return true
	if t >= width and owner[t - width] == pid: return true
	if t < width * (height - 1) and owner[t + width] == pid: return true
	return false


func coastal(t: int) -> bool:
	var x := t % width
	if x > 0 and terrain[t - 1] == MapGen.OCEAN: return true
	if x < width - 1 and terrain[t + 1] == MapGen.OCEAN: return true
	if t >= width and terrain[t - width] == MapGen.OCEAN: return true
	if t < width * (height - 1) and terrain[t + width] == MapGen.OCEAN: return true
	return false


static func _tile_key(t: int, o: int) -> int:
	return SimRng.mix(t * 131 + o * 0x9E3779B1 + 7)


## The only way tile ownership changes. Keeps tile counts, bounding boxes, the incremental
## ownership hash and the render dirty list consistent.
func set_owner(t: int, pid: int) -> void:
	var old: int = owner[t]
	if old == pid:
		return
	tile_hash ^= _tile_key(t, old) ^ _tile_key(t, pid)
	var x := t % width
	var y := t / width
	if old != NONE:
		var q: SimPlayer = players[old]
		q.tiles -= 1
		q.sum_x -= x
		q.sum_y -= y
		if q.capital == t:
			q.capital = -1
	owner[t] = pid
	if pid != NONE:
		var p: SimPlayer = players[pid]
		p.tiles += 1
		p.sum_x += x
		p.sum_y += y
		p.grow_bbox(x, y)
	dirty.append(t)


func take_dirty() -> PackedInt32Array:
	var d := dirty
	dirty = PackedInt32Array()
	return d


# ------------------------------------------------------------------ spawning

func spawn_ok(t: int, pid: int) -> bool:
	if t < 0 or t >= owner.size() or not passable(t) or owner[t] != NONE:
		return false
	var md: int = rules.section("spawn")["min_distance"]
	var x := t % width
	var y := t / width
	for p in players:
		if p.pid == 0 or p.pid == pid or p.capital < 0:
			continue
		var dx: int = p.capital % width - x
		var dy: int = p.capital / width - y
		if dx * dx + dy * dy < md * md:
			return false
	return true


func spawn_at(p: SimPlayer, t: int) -> void:
	var r: int = rules.section("spawn")["radius"]
	var cx := t % width
	var cy := t / width
	for dy in range(-r, r + 1):
		for dx in range(-r, r + 1):
			var x := cx + dx
			var y := cy + dy
			if dx * dx + dy * dy > r * r or x < 0 or y < 0 or x >= width or y >= height:
				continue
			var i := idx(x, y)
			if passable(i) and owner[i] == NONE:
				set_owner(i, p.pid)
	p.capital = t
	p.spawned = true
	emit({"type": "spawn", "pid": p.pid, "tile": t})


func spawn_random(p: SimPlayer) -> void:
	for attempt in 400:
		var t := rng.below(owner.size())
		if spawn_ok(t, p.pid):
			spawn_at(p, t)
			return
	# Crowded map: accept any free land tile.
	for attempt in 2000:
		var t := rng.below(owner.size())
		if passable(t) and owner[t] == NONE:
			spawn_at(p, t)
			return


func in_spawn_phase() -> bool:
	return tick < int(rules.section("spawn")["phase_ticks"])


# ------------------------------------------------------------------ events

func emit(e: Dictionary) -> void:
	e["tick"] = tick
	events.append(e)
	if events.size() > 2048:
		events = events.slice(1024)
		event_base += 1024


# ------------------------------------------------------------------ tick

## Advances one tick. intents are applied first, in the given (server) order.
func step(intents: Array) -> void:
	if over:
		tick += 1
		return
	var queued := ai_queue
	ai_queue = []
	for intent in queued:
		apply_intent(intent)
	for intent in intents:
		apply_intent(intent)
	if tick == int(rules.section("spawn")["phase_ticks"]) - 1:
		for p in players:
			if p.pid != 0 and not p.spawned:
				spawn_random(p)
	Economy.tick(self)
	Combat.tick(self)
	Strikes.tick(self)
	Economy.eras(self)
	Diplomacy.tick(self)
	bot_brain.think(self)
	nation_brain.think(self)
	Diplomacy.eliminations(self)
	if tick % 10 == 0:
		Diplomacy.check_victory(self)
	tick += 1


func apply_intent(intent: Dictionary) -> void:
	var pid: int = int(intent.get("p", 0))
	if pid <= 0 or pid >= players.size():
		return
	var p: SimPlayer = players[pid]
	var kind: String = str(intent.get("type", ""))
	if kind == "spawn":
		if in_spawn_phase() and not p.spawned and spawn_ok(int(intent.get("tile", -1)), pid):
			spawn_at(p, int(intent["tile"]))
		return
	if not p.alive or not p.spawned:
		return
	match kind:
		"attack": Combat.start_attack(self, pid, int(intent.get("target", 0)), int(intent.get("ratio", 200)), int(intent.get("tile", -1)))
		"retreat": Combat.retreat(self, pid, int(intent.get("target", 0)))
		"boat": Combat.launch_boat(self, pid, int(intent.get("from", -1)), int(intent.get("tile", -1)), int(intent.get("ratio", 200)))
		"build": Economy.build(self, pid, int(intent.get("kind", -1)), int(intent.get("tile", -1)))
		"perk": Economy.choose_perk(self, pid, int(intent.get("choice", 0)))
		"strike": Strikes.launch(self, pid, int(intent.get("kind", -1)), int(intent.get("tile", -1)))
		"propose": Diplomacy.propose(self, pid, int(intent.get("to", 0)), int(intent.get("treaty", -1)))
		"respond": Diplomacy.respond(self, pid, int(intent.get("id", -1)), bool(intent.get("accept", false)))
		"break": Diplomacy.break_treaty(self, pid, int(intent.get("id", -1)))
		"donate": Diplomacy.donate(self, pid, int(intent.get("to", 0)), int(intent.get("troops", 0)), int(intent.get("gold", 0)))
		"surrender": Diplomacy.surrender(self, pid)


# ------------------------------------------------------------------ queries

func player(pid: int) -> SimPlayer:
	return players[pid] if pid > 0 and pid < players.size() else null


func alive_players() -> Array:
	var out := []
	for p in players:
		if p.pid != 0 and p.alive:
			out.append(p)
	return out


func world_era() -> int:
	var e := 0
	for p in players:
		if p.pid != 0 and p.alive:
			e = maxi(e, p.era)
	return e


func buildings_of(pid: int, type: int) -> int:
	var n := 0
	for b in buildings:
		if b["alive"] and b["type"] == type and owner[b["tile"]] == pid:
			n += 1
	return n


## Owners that share a land border with pid (sorted ascending), plus whether wilderness is adjacent.
## Scans only pid's bounding box.
func border_info(pid: int) -> Dictionary:
	var p: SimPlayer = players[pid]
	var seen := {}
	var wild := false
	if p.tiles == 0:
		return {"neighbors": PackedInt32Array(), "wild": false}
	var x0: int = maxi(0, p.min_x - 1)
	var x1: int = mini(width - 1, p.max_x + 1)
	var y0: int = maxi(0, p.min_y - 1)
	var y1: int = mini(height - 1, p.max_y + 1)
	for y in range(y0, y1 + 1):
		var row := y * width
		for x in range(x0, x1 + 1):
			var t := row + x
			if owner[t] != pid:
				continue
			if x > 0: wild = _note(t - 1, pid, seen) or wild
			if x < width - 1: wild = _note(t + 1, pid, seen) or wild
			if y > 0: wild = _note(t - width, pid, seen) or wild
			if y < height - 1: wild = _note(t + width, pid, seen) or wild
	var ids := PackedInt32Array(seen.keys())
	ids.sort()
	return {"neighbors": ids, "wild": wild}


func _note(t: int, pid: int, seen: Dictionary) -> bool:
	var o: int = owner[t]
	if o == pid or terrain[t] == MapGen.OCEAN:
		return false
	if o == NONE:
		return true
	seen[o] = true
	return false


# ------------------------------------------------------------------ hash / snapshot

func state_hash() -> int:
	var h := SimHash.new()
	h.add(tick); h.add(tile_hash); h.add(rng.state); h.add(next_uid); h.add(1 if over else 0)
	for p in players:
		p.hash_into(h)
	for a in attacks:
		h.add(a.uid); h.add(a.attacker); h.add(a.target); h.add(a.troops); h.add(a.frontier.size() - a.pos + a.next.size())
	for b in boats:
		h.add(b["uid"]); h.add(b["troops"]); h.add(b["pos"])
	for b in buildings:
		h.add(b["type"]); h.add(b["tile"]); h.add(1 if b["alive"] else 0); h.add(b["disabled_until"])
	for s in strikes:
		h.add(s["kind"]); h.add(s["tile"]); h.add(s["impact"])
	for t in treaties:
		h.add(t["id"]); h.add(t["type"]); h.add(t["a"]); h.add(t["b"]); h.add(t["end"]); h.add(t["notice_end"])
	for pr in proposals:
		h.add(pr["id"]); h.add(pr["type"]); h.add(pr["from"]); h.add(pr["to"]); h.add(pr["expires"])
	return h.value


## Full state for reconnect and desync recovery. Terrain is regenerated from the seed.
func snapshot() -> PackedByteArray:
	var ps := []
	for p in players:
		ps.append(p.to_dict())
	var atk := []
	for a in attacks:
		atk.append(a.to_dict())
	var d := {
		"v": 1, "seed": seed_value, "w": width, "h": height, "tick": tick, "rng": rng.state, "owner": owner,
		"building_at": building_at, "visit": visit, "players": ps, "buildings": buildings, "attacks": atk,
		"boats": boats, "strikes": strikes, "treaties": treaties, "proposals": proposals, "tile_hash": tile_hash,
		"next_uid": next_uid, "duration": duration_ticks, "over": over, "winners": winners, "ai_queue": ai_queue,
		"ai": nation_brain.to_dict(),
	}
	var raw := var_to_bytes(d)
	var out := PackedByteArray()
	out.resize(4)
	out.encode_u32(0, raw.size())
	out.append_array(raw.compress(FileAccess.COMPRESSION_ZSTD))
	return out


static func restore(bytes: PackedByteArray, rule_set: Ruleset = null) -> Sim:
	if bytes.size() < 4:
		return null
	var size := bytes.decode_u32(0)
	if size <= 0 or size > 64 * 1024 * 1024:
		return null
	var decoded: Variant = bytes_to_var(bytes.slice(4).decompress(size, FileAccess.COMPRESSION_ZSTD))
	if typeof(decoded) != TYPE_DICTIONARY:
		return null
	var d: Dictionary = decoded
	var s := Sim.new()
	s.rules = rule_set if rule_set != null else Ruleset.load_default()
	s.seed_value = d["seed"]; s.width = d["w"]; s.height = d["h"]; s.tick = d["tick"]
	s.rng = SimRng.new(1); s.rng.state = d["rng"]
	s.terrain = MapGen.generate(s.seed_value, s.width, s.height, s.rules.section("map")["land_permille"])
	for t in s.terrain:
		if t != MapGen.OCEAN:
			s.land_total += 1
	s.owner = d["owner"]; s.building_at = d["building_at"]; s.visit = d["visit"]
	for pd in d["players"]:
		s.players.append(SimPlayer.from_dict(pd))
	s.buildings = d["buildings"]
	for ad in d["attacks"]:
		s.attacks.append(SimAttack.from_dict(ad))
	s.boats = d["boats"]; s.strikes = d["strikes"]; s.treaties = d["treaties"]; s.proposals = d["proposals"]
	s.tile_hash = d["tile_hash"]; s.next_uid = d["next_uid"]; s.duration_ticks = d["duration"]
	s.over = d["over"]; s.winners = d["winners"]; s.ai_queue = d["ai_queue"]
	s.bot_brain = BotAI.new()
	s.nation_brain = NationAI.new()
	s.nation_brain.from_dict(d["ai"])
	# Every tile is dirty for a freshly restored view; centroid sums are rebuilt from ownership.
	for i in s.owner.size():
		var o: int = s.owner[i]
		if o != NONE:
			s.dirty.append(i)
			s.players[o].sum_x += i % s.width
			s.players[o].sum_y += i / s.width
	return s


func uid() -> int:
	next_uid += 1
	return next_uid - 1
