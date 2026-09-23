extends GdUnitTestSuite

func test_hud_layout_and_command_only_interaction() -> void:
    var scene: Node = auto_free(load("res://game/scenes/match.tscn").instantiate())
    add_child(scene)
    var hud: Node = scene.get_node("HUD")
    for i in 12:
        assert_that(hud.get_node_or_null("Root/CommandPanel/Slot%d" % i)).is_not_null()
    var checks: Node = auto_free(load("res://tools/HudChecks.cs").new())
    assert_str(checks.call("RunChecks", scene)).is_empty()
