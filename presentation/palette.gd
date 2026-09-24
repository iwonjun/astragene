class_name Palette
extends RefCounted
## Player colours: humans bright, nations medium, bots muted (so people stand out on a crowded map).


static func color(pid: int, kind: int) -> Color:
	if pid <= 0:
		return Color(0, 0, 0, 0)
	var hue := fmod(pid * 0.61803398875, 1.0)
	match kind:
		SimPlayer.HUMAN:
			return Color.from_hsv(hue, 0.85, 0.98)
		SimPlayer.NATION:
			return Color.from_hsv(hue, 0.62, 0.80)
	return Color.from_hsv(hue, 0.30, 0.62)


static func terrain_color(rules: Ruleset, id: int) -> Color:
	return Color(str(rules.terrain[id]["color"]))
