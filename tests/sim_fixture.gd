class_name SimFixture
extends RefCounted
## Helpers for core tests: small flat maps with hand-placed humans.


static func humans(count: int, extra: Dictionary = {}) -> Sim:
	var list := []
	for i in count:
		list.append({"name": "H%d" % (i + 1), "kind": SimPlayer.HUMAN})
	var config := {"seed": 5, "width": 80, "height": 50, "flat": true, "players": list}
	config.merge(extra, true)
	return Sim.create(config)


## Spawns player pid at (x, y) during the spawn phase and runs through the phase.
static func place(sim: Sim, spots: Array) -> void:
	var intents := []
	for i in spots.size():
		intents.append({"type": "spawn", "p": i + 1, "tile": sim.idx(spots[i][0], spots[i][1])})
	sim.step(intents)
	while sim.in_spawn_phase():
		sim.step([])


static func run(sim: Sim, ticks: int) -> void:
	for i in ticks:
		sim.step([])
