class_name Fmt
extends RefCounted
## Number formatting and human-readable (Korean) text for simulation events.


static func num(n: int) -> String:
	var a := absi(n)
	if a >= 1000000:
		return "%.1fM" % (n / 1000000.0)
	if a >= 10000:
		return "%.1fk" % (n / 1000.0)
	return str(n)


static func clock(ticks: int) -> String:
	var s := maxi(0, ticks) / 10
	return "%d:%02d" % [s / 60, s % 60]


static func treaty_name(rules: Ruleset, id: int) -> String:
	return str(rules.treaties[id]["name_ko"]) if id >= 0 and id < rules.treaties.size() else "?"


static func pname(sim: Sim, pid: int) -> String:
	var p := sim.player(pid)
	return p.name if p != null else "?"


## Returns [text, importance] for events worth showing, [] otherwise. importance: 0 info, 1 me, 2 alert.
static func event_text(sim: Sim, e: Dictionary, me: int) -> Array:
	var r := sim.rules
	match str(e["type"]):
		"betrayal":
			var victim := "당신" if e["b"] == me else pname(sim, e["b"])
			return ["[배신] %s이(가) %s과의 조약을 깨고 공격했습니다! (배신자 60초)" % [pname(sim, e["a"]), victim], 2 if e["b"] == me or e["a"] == me else 1]
		"treaty":
			if e["a"] == me or e["b"] == me:
				var other: int = e["b"] if e["a"] == me else e["a"]
				return ["[조약] %s과(와) %s 조약을 맺었습니다." % [pname(sim, other), treaty_name(r, e["treaty"])], 1]
			if e["treaty"] == Diplomacy.ALLIANCE:
				return ["%s과(와) %s이(가) 동맹을 맺었습니다." % [pname(sim, e["a"]), pname(sim, e["b"])], 0]
		"treaty_ended":
			if e["a"] == me or e["b"] == me:
				var other2: int = e["b"] if e["a"] == me else e["a"]
				return ["%s과(와)의 %s 조약이 끝났습니다." % [pname(sim, other2), treaty_name(r, e["treaty"])], 1]
		"nap_notice":
			if e["b"] == me:
				return ["[예고] %s이(가) 불가침 조약 파기를 예고했습니다. 30초 뒤 공격할 수 있습니다." % pname(sim, e["a"]), 2]
		"rejected":
			if e["a"] == me:
				return ["%s이(가) %s 제안을 거절했습니다." % [pname(sim, e["b"]), treaty_name(r, e["treaty"])], 1]
		"expired":
			if e["a"] == me:
				return ["%s에게 보낸 %s 제안이 만료되었습니다." % [pname(sim, e["b"]), treaty_name(r, e["treaty"])], 0]
		"attack":
			if e["b"] == me:
				return ["[공격] %s이(가) 병력 %s으로 공격해 옵니다!" % [pname(sim, e["a"]), num(e["troops"])], 2]
		"era":
			if e["pid"] == me:
				return ["[시대] %s 시대에 들어섰습니다! 특성을 하나 고르세요." % r.eras[e["era"]]["name_ko"], 1]
			return ["%s이(가) %s 시대에 들어섰습니다." % [pname(sim, e["pid"]), r.eras[e["era"]]["name_ko"]], 0]
		"eliminated":
			return ["[멸망] %s이(가) 멸망했습니다." % pname(sim, e["pid"]), 2 if e["pid"] == me else 0]
		"landing":
			if e["pid"] != me and e["tile"] >= 0 and sim.owner[e["tile"]] != me:
				return []
			return ["[상륙] %s의 상륙 부대가 해안에 내렸습니다." % pname(sim, e["pid"]), 1]
		"strike_launch":
			var name := str(r.strikes[e["kind"]]["name_ko"])
			var victim_pid: int = sim.owner[e["tile"]]
			if e["kind"] == Strikes.NUKE:
				return ["[핵] %s이(가) 핵을 발사했습니다!" % pname(sim, e["pid"]), 2]
			if victim_pid == me:
				return ["[타격] %s이(가) 우리 영토에 %s을(를) 발사했습니다!" % [pname(sim, e["pid"]), name], 2]
		"donate":
			if e["b"] == me:
				return ["[지원] %s이(가) 병력 %s, 금 %s을(를) 보내왔습니다." % [pname(sim, e["a"]), num(e["troops"]), num(e["gold"])], 1]
		"game_over":
			var names := []
			for w in e["winners"]:
				names.append(pname(sim, w))
			return ["[종료] 경기 종료 — 승리: %s" % ", ".join(names), 2]
	return []
