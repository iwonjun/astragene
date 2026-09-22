extends GdUnitTestSuite

func test_match_scene_connects_bridge_and_local_selection() -> void:
    var scene: Node = auto_free(load("res://game/scenes/match.tscn").instantiate())
    add_child(scene)
    assert_that(scene.get_node_or_null("Bridge")).is_not_null()
    assert_that(scene.get_node_or_null("Selection")).is_not_null()
    assert_int(scene.get_node("Selection").get("SelectedCount")).is_equal(0)
    assert_array(scene.get_node("Bridge").call("FriendlyIds")).is_empty()

func test_selection_box_groups_toggle_cap_and_sim_isolation() -> void:
    var scene: Node = auto_free(load("res://game/scenes/match.tscn").instantiate())
    add_child(scene)
    var checks: Node = auto_free(load("res://tools/SelectionChecks.cs").new())
    assert_str(checks.call("RunChecks", scene)).is_empty()
