extends GdUnitTestSuite

func test_match_scene_connects_bridge_and_local_selection() -> void:
    var scene: Node = auto_free(load("res://game/scenes/match.tscn").instantiate())
    add_child(scene)
    assert_that(scene.get_node_or_null("Bridge")).is_not_null()
    assert_that(scene.get_node_or_null("Selection")).is_not_null()
    assert_int(scene.get_node("Selection").get("SelectedCount")).is_equal(0)
    assert_int(scene.get_node("Bridge").call("FriendlyIds").size()).is_equal(7)

func test_selection_box_groups_toggle_cap_and_sim_isolation() -> void:
    var scene: Node = auto_free(load("res://game/scenes/match.tscn").instantiate())
    add_child(scene)
    var checks: Node = auto_free(load("res://tools/SelectionChecks.cs").new())
    assert_str(checks.call("RunChecks", scene)).is_empty()

func test_generated_assets_and_instancing_contract() -> void:
    for index in 10:
        var mesh: Mesh = load("res://game/assets/generated/unit_%d.mesh" % index)
        assert_that(mesh).is_not_null()
        assert_int(mesh.get_surface_count()).is_equal(1)
        assert_that(mesh.surface_get_material(0)).is_not_null()
    var scene: Node = auto_free(load("res://game/scenes/match.tscn").instantiate())
    add_child(scene)
    var renderer: Node = scene.get_node("Renderer")
    var batches := 0
    for child in renderer.get_children():
        if child is MultiMeshInstance3D:
            batches += 1
            assert_bool(child.multimesh.use_custom_data).is_true()
            assert_int(child.multimesh.instance_count).is_greater_equal(128)
    assert_int(batches).is_equal(16)
    assert_that(scene.get_node("Camera")).is_not_null()
    assert_that(scene.get_node("Environment").environment).is_not_null()
