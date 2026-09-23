extends SceneTree

# Documentation capture of the results screen:
# godot --script res://tools/capture_results.gd --write-movie docs/phase13-results.png --fixed-fps 20 --quit-after 40
var scene: Node
var checks: Node
var frame := 0

func _initialize() -> void:
	scene = load("res://game/scenes/match.tscn").instantiate()
	root.add_child(scene)
	checks = load("res://tools/PolishChecks.cs").new()
	root.add_child(checks)

func _process(_delta: float) -> bool:
	frame += 1
	if frame == 5:
		var error: String = checks.call("RunChecks", scene)
		if error != "":
			push_error(error)
	return false
