extends GdUnitTestSuite

func test_outcome_results_audio_and_settings() -> void:
    var scene: Node = auto_free(load("res://game/scenes/match.tscn").instantiate())
    add_child(scene)
    var checks: Node = auto_free(load("res://tools/PolishChecks.cs").new())
    add_child(checks)
    assert_str(checks.call("RunChecks", scene)).is_empty()

func test_settings_scene_builds() -> void:
    var settings: Node = auto_free(load("res://game/scenes/settings.tscn").instantiate())
    add_child(settings)
    assert_int(settings.get_node("Root/Display/Resolution").item_count).is_equal(4)
    assert_that(settings.get_node_or_null("Root/Keys/Key11")).is_not_null()
    for bus in ["Master", "Music", "SFX", "Voice", "UI"]:
        assert_that(settings.get_node_or_null("Root/Sound/" + bus)).is_not_null()
