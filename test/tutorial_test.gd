extends GdUnitTestSuite

func test_tutorial_steps_advance_on_real_actions() -> void:
    var checks: Node = auto_free(load("res://tools/TutorialChecks.cs").new())
    var scene: Node = auto_free(checks.call("CreateMatch"))
    add_child(scene)
    assert_str(checks.call("RunChecks", scene)).is_empty()

func test_lobby_offers_tutorial_first() -> void:
    var lobby: Node = auto_free(load("res://game/scenes/lobby.tscn").instantiate())
    add_child(lobby)
    assert_that(lobby.get_node_or_null("UI/Root/Menu/Tutorial")).is_not_null()
