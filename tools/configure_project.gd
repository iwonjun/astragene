@tool
extends SceneTree

func _initialize() -> void:
    ProjectSettings.set_setting("application/run/main_scene", "res://game/scenes/boot.tscn")
    ProjectSettings.set_setting("application/config/features", PackedStringArray(["4.6", "C#", "Forward Plus"]))
    ProjectSettings.set_setting("dotnet/project/assembly_name", "RtsGame")
    ProjectSettings.set_setting("physics/common/physics_ticks_per_second", 20)
    ProjectSettings.set_setting("rendering/renderer/rendering_method", "forward_plus")
    var keys := {
        "camera_up": KEY_W, "camera_down": KEY_S,
        "camera_left": KEY_A, "camera_right": KEY_D,
        "recent_event": KEY_SPACE, "pause_menu": KEY_ESCAPE,
        "queue_modifier": KEY_SHIFT, "select_modifier": KEY_CTRL,
    }
    for action in keys:
        _key_action(action, keys[action])
    for index in range(10):
        _key_action("control_group_%d" % index, KEY_0 + index)
    _mouse_action("select", MOUSE_BUTTON_LEFT)
    _mouse_action("command", MOUSE_BUTTON_RIGHT)
    _mouse_action("zoom_in", MOUSE_BUTTON_WHEEL_UP)
    _mouse_action("zoom_out", MOUSE_BUTTON_WHEEL_DOWN)
    var error := ProjectSettings.save()
    if error != OK:
        push_error("Project settings save failed: %s" % error_string(error))
        quit(1)
        return
    print("Configured .NET, Forward+, 20 Hz and input actions.")
    quit(0)

func _key_action(action: String, key: int) -> void:
    var event := InputEventKey.new()
    event.physical_keycode = key
    ProjectSettings.set_setting("input/" + action, {"deadzone": 0.2, "events": [event]})

func _mouse_action(action: String, button: int) -> void:
    var event := InputEventMouseButton.new()
    event.button_index = button
    ProjectSettings.set_setting("input/" + action, {"deadzone": 0.2, "events": [event]})
