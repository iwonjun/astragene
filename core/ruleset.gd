class_name Ruleset
extends RefCounted
## Integer rules loaded from data/rules.json (AGENTS.md R6). JSON numbers arrive as floats and are
## converted to int once here, so the simulation never sees a float.

const DEFAULT_PATH := "res://data/rules.json"
var data: Dictionary = {}
var terrain: Array = []
var eras: Array = []
var buildings: Array = []
var strikes: Array = []
var perks: Array = []
var treaties: Array = []
var difficulty: Array = []
var ai_profiles: Array = []


static func load_default() -> Ruleset:
	return from_text(FileAccess.get_file_as_string(DEFAULT_PATH))


static func from_text(text: String) -> Ruleset:
	var parsed: Variant = JSON.parse_string(text)
	if typeof(parsed) != TYPE_DICTIONARY:
		push_error("rules.json is not a JSON object")
		return null
	var r := Ruleset.new()
	r.data = _ints(parsed)
	r.terrain = r.data["terrain"]
	r.eras = r.data["eras"]
	r.buildings = r.data["buildings"]
	r.strikes = r.data["strikes"]
	r.perks = r.data["perks"]
	r.treaties = r.data["treaties"]
	r.difficulty = r.data["difficulty"]
	r.ai_profiles = r.data["ai_profiles"]
	return r


static func _ints(v: Variant) -> Variant:
	match typeof(v):
		TYPE_FLOAT:
			return int(v)
		TYPE_DICTIONARY:
			var out := {}
			for key in v:
				out[key] = _ints(v[key])
			return out
		TYPE_ARRAY:
			var arr := []
			for item in v:
				arr.append(_ints(item))
			return arr
	return v


func section(key: String) -> Dictionary:
	return data[key]


func value(section_key: String, key: String) -> int:
	return int(data[section_key][key])


## Text digest so every peer can confirm it runs the same rules before a match.
func digest() -> int:
	var h := SimHash.new()
	h.add_bytes(JSON.stringify(data, "", true).to_utf8_buffer())
	return h.value
