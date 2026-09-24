class_name AiVoice
extends RefCounted
## Templated speech for computer nations: replies to DMs and reactions to simulation events.
## (design.md 8.4 "정형 외교"; optional LLM diplomacy is out of Blitz alpha scope.)

const REPLIES := {
	"strong": ["우리의 창끝은 아직 날카롭다. 말보다 행동으로 증명해라.", "약자와는 손잡지 않는다.", "네 영토가 탐나는군. 조심하는 게 좋을 거다."],
	"friendly": ["좋은 이웃은 귀하지. 불가침 조약을 제안해 보는 건 어떤가?", "교역로를 열자. 서로에게 이득이다.", "그대의 제안이라면 귀 기울이겠다."],
	"afraid": ["우리는 싸움을 원하지 않는다.", "평화를 원한다면 조약 카드를 보내라.", "제발 우리 국경은 건드리지 말아 다오."],
	"distrust": ["배신자의 말은 믿지 않는다.", "네가 한 짓을 모두가 기억하고 있다.", "신뢰는 행동으로 되찾는 것이다."],
}
const ACCEPT := ["좋다. 약속은 지켜라.", "협정을 받아들이지.", "이것이 서로를 위한 길이다."]
const REJECT := ["거절한다.", "지금은 때가 아니다.", "그 제안은 우리에게 득이 없다."]
const BETRAYED := ["{a}의 배신을 잊지 않겠다!", "{a}, 약속을 짓밟은 대가를 치르게 될 것이다.", "모두 보았는가? {a}는 믿을 수 없는 자다."]
const NUKED := ["{a}가 핵을 썼다. 온 세계가 적이 될 것이다."]


func _pick(list: Array, seed_value: int) -> String:
	return list[absi(seed_value) % list.size()]


func reply(sim: Sim, nation: SimPlayer, from: int, _text: String) -> String:
	var other: SimPlayer = sim.players[from]
	var trust: int = nation.trust[from]
	var seed_value := sim.tick + from * 7
	if other.is_traitor(sim.tick) or trust < 600:
		return _pick(REPLIES["distrust"], seed_value)
	var mine := NationAI.strength(nation)
	var theirs := NationAI.strength(other)
	if mine * 10 > theirs * 13 and nation.profile != NationAI.MERCHANT:
		return _pick(REPLIES["strong"], seed_value)
	if theirs * 10 > mine * 13 or nation.profile == NationAI.COWARD:
		return _pick(REPLIES["afraid"], seed_value)
	return _pick(REPLIES["friendly"], seed_value)


## Returns [from_pid, channel, to_pid, text] or [] for silence.
func on_event(sim: Sim, e: Dictionary) -> Array:
	match str(e["type"]):
		"ai_say":
			var yes := str(e["key"]).begins_with("accept")
			return [e["pid"], "dm", e["to"], _pick(ACCEPT if yes else REJECT, e["tick"] + e["pid"])]
		"betrayal":
			var victim: SimPlayer = sim.players[e["b"]]
			if victim.kind == SimPlayer.NATION and victim.alive:
				return [victim.pid, "global", 0, _pick(BETRAYED, e["tick"]).replace("{a}", sim.players[e["a"]].name)]
		"strike_launch":
			if int(e["kind"]) == Strikes.NUKE:
				for p in sim.players:
					if p.pid != 0 and p.kind == SimPlayer.NATION and p.alive and p.pid != e["pid"]:
						return [p.pid, "global", 0, _pick(NUKED, e["tick"]).replace("{a}", sim.players[e["pid"]].name)]
	return []
