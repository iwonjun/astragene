using Godot;
namespace RtsGame.UI;
public partial class BootMenu : Node
{
    public override void _Ready()=>GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile,"res://game/scenes/match.tscn");
}
