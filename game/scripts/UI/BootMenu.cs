using Godot;
using RtsGame.Net;
namespace RtsGame.UI;
public partial class BootMenu : Node
{
    // Benchmarks and captures keep launching straight into a match; players land in the lobby.
    public override void _Ready()
    {
        if(int.TryParse(MatchLaunch.Arg("ai"),out int ai))MatchLaunch.AiDifficulty=ai; // --local --ai=0..2
        bool direct=MatchLaunch.Flag("render-benchmark")||MatchLaunch.Flag("art-capture")||MatchLaunch.Flag("local");
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile,direct?"res://game/scenes/match.tscn":"res://game/scenes/lobby.tscn");
    }
}
