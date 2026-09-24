class_name SimAttack
extends RefCounted
## A wave of troops spreading from the attacker's border into the target's land (0 = wilderness).
## The frontier is processed breadth-first, so conquest grows as an even flood along the border.

var uid: int = 0
var attacker: int = 0
var target: int = 0
var troops: int = 0
var frontier: PackedInt32Array = PackedInt32Array()
var next: PackedInt32Array = PackedInt32Array()
var pos: int = 0
var done: bool = false


func remaining() -> int:
	return frontier.size() - pos + next.size()


func to_dict() -> Dictionary:
	return {"uid": uid, "attacker": attacker, "target": target, "troops": troops, "frontier": frontier, "next": next, "pos": pos, "done": done}


static func from_dict(d: Dictionary) -> SimAttack:
	var a := SimAttack.new()
	a.uid = d["uid"]; a.attacker = d["attacker"]; a.target = d["target"]; a.troops = d["troops"]
	a.frontier = d["frontier"]; a.next = d["next"]; a.pos = d["pos"]; a.done = d["done"]
	return a
