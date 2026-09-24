class_name BotAI
extends RefCounted
## Simple expansion bots (FSM): grow, flood into wilderness, then bully the weakest neighbour.
## They never sign treaties. Decisions are queued as ordinary intents for the next tick.

const THINK := 30


func think(sim: Sim) -> void:
	for pr in sim.proposals:
		var to: SimPlayer = sim.players[pr["to"]]
		if to.kind == SimPlayer.BOT and (sim.tick + to.pid) % 10 == 0:
			sim.ai_queue.append({"type": "respond", "p": to.pid, "id": pr["id"], "accept": false})
	for p in sim.players:
		if p.pid == 0 or p.kind != SimPlayer.BOT or not p.alive or not p.spawned:
			continue
		if (sim.tick + p.pid * 7) % THINK != 0 or sim.in_spawn_phase():
			continue
		_decide(sim, p)


func _decide(sim: Sim, p: SimPlayer) -> void:
	var fill: int = p.pop * 1000 / maxi(1, p.cap)
	if fill < 450:
		return
	var info := sim.border_info(p.pid)
	if info["wild"]:
		sim.ai_queue.append({"type": "attack", "p": p.pid, "target": 0, "ratio": 300})
		return
	var mine := Combat.density(p)
	var best := -1
	var best_density := 1 << 30
	for n in info["neighbors"]:
		var other: SimPlayer = sim.players[n]
		var d := Combat.density(other)
		if (d * 1200 / 1000 < mine or fill >= 950) and d < best_density and not Diplomacy.blocks_attack(sim, p.pid, n):
			best = n
			best_density = d
	if best > 0 and fill > 600:
		sim.ai_queue.append({"type": "attack", "p": p.pid, "target": best, "ratio": 400})
	elif p.capital >= 0 and Economy.can_build(sim, p.pid, Economy.CITY, p.capital):
		sim.ai_queue.append({"type": "build", "p": p.pid, "kind": Economy.CITY, "tile": p.capital})
