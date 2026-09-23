extends GdUnitTestSuite

func test_enet_loopback_lobby_chat_and_lockstep() -> void:
    var checks: Node = auto_free(load("res://tools/NetChecks.cs").new())
    assert_str(checks.call("RunChecks")).is_empty()

func test_lobby_and_replay_scenes_build() -> void:
    var lobby: Node = auto_free(load("res://game/scenes/lobby.tscn").instantiate())
    add_child(lobby)
    for path in ["UI/Root/Menu/Host", "UI/Root/Menu/Join", "UI/Root/Menu/Faction", "UI/Root/Menu/Ready", "UI/Root/Menu/Start", "UI/Root/Room/Players"]:
        assert_that(lobby.get_node_or_null(path)).is_not_null()
    assert_int(lobby.get_node("UI/Root/Menu/Faction").item_count).is_equal(2)
    var match_scene: Node = auto_free(load("res://game/scenes/match.tscn").instantiate())
    add_child(match_scene)
    assert_that(match_scene.get_node_or_null("Net")).is_not_null()
    assert_that(load("res://game/scenes/replay.tscn")).is_not_null()
