class_name SimPlayer
extends RefCounted
## One nation in the simulation. Plain integers only; the presentation reads these fields.

const HUMAN := 0
const BOT := 1
const NATION := 2

var pid: int = 0
var name: String = ""
var kind: int = HUMAN
var alive: bool = true
var spawned: bool = false
var pop: int = 0
var gold_milli: int = 0
var tiles: int = 0
var era: int = 0
var gauge: int = 0
var perks: PackedInt32Array = PackedInt32Array()
var perk_options: PackedInt32Array = PackedInt32Array()
var perk_deadline: int = 0
var capital: int = -1
var traitor_until: int = 0
var last_proposal: int = -100000
var difficulty: int = 1
var profile: int = 0
var eliminated_tick: int = -1
var min_x: int = 1 << 30
var min_y: int = 1 << 30
var max_x: int = -1
var max_y: int = -1
# Derived (not hashed; they follow from ownership): territory centroid sums for map labels.
var sum_x: int = 0
var sum_y: int = 0
# Derived each tick by the economy (not hashed separately; they follow from state).
var cap: int = 0
var income_milli: int = 0
# Nation AI memory: trust toward every pid, 0..2000 with 1000 neutral.
var trust: PackedInt32Array = PackedInt32Array()


func _init(id: int = 0, perk_count: int = 0) -> void:
	pid = id
	perks.resize(perk_count)


var gold: int:
	get:
		return gold_milli / 1000


func perk(id: int) -> int:
	return perks[id] if id < perks.size() else 0


func is_traitor(tick: int) -> bool:
	return traitor_until > tick


func grow_bbox(x: int, y: int) -> void:
	min_x = mini(min_x, x)
	min_y = mini(min_y, y)
	max_x = maxi(max_x, x)
	max_y = maxi(max_y, y)


func to_dict() -> Dictionary:
	return {
		"pid": pid, "name": name, "kind": kind, "alive": alive, "spawned": spawned, "pop": pop,
		"gold": gold_milli, "tiles": tiles, "era": era, "gauge": gauge, "perks": perks,
		"perk_options": perk_options, "perk_deadline": perk_deadline, "capital": capital,
		"traitor": traitor_until, "last_proposal": last_proposal, "difficulty": difficulty,
		"profile": profile, "eliminated": eliminated_tick,
		"bbox": PackedInt32Array([min_x, min_y, max_x, max_y]), "trust": trust,
	}


static func from_dict(d: Dictionary) -> SimPlayer:
	var p := SimPlayer.new(d["pid"])
	p.name = d["name"]; p.kind = d["kind"]; p.alive = d["alive"]; p.spawned = d["spawned"]
	p.pop = d["pop"]; p.gold_milli = d["gold"]; p.tiles = d["tiles"]; p.era = d["era"]; p.gauge = d["gauge"]
	p.perks = d["perks"]; p.perk_options = d["perk_options"]; p.perk_deadline = d["perk_deadline"]
	p.capital = d["capital"]; p.traitor_until = d["traitor"]; p.last_proposal = d["last_proposal"]
	p.difficulty = d["difficulty"]; p.profile = d["profile"]; p.eliminated_tick = d["eliminated"]
	var b: PackedInt32Array = d["bbox"]
	p.min_x = b[0]; p.min_y = b[1]; p.max_x = b[2]; p.max_y = b[3]
	p.trust = d["trust"]
	return p


func hash_into(h: SimHash) -> void:
	h.add(pid); h.add(1 if alive else 0); h.add(1 if spawned else 0); h.add(pop); h.add64(gold_milli)
	h.add(tiles); h.add(era); h.add(gauge); h.add(capital); h.add(traitor_until); h.add(perk_deadline)
	for v in perks: h.add(v)
	for v in perk_options: h.add(v)
	for v in trust: h.add(v)
