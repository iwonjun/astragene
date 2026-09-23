using Godot;
using RtsGame.Bridge;
using RtsGame.Net;
using RtsGame.UI;
using RtsGame.View;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;

/// <summary>Phase 13 checks: surrender → deterministic outcome → results table/APM graph, audio buses and settings round trip.</summary>
public partial class PolishChecks : Node
{
    public string RunChecks(Node match)
    {
        var bridge = match.GetNode<MatchBridge>("Bridge"); var hud = match.GetNode<MatchHud>("HUD");
        var results = match.GetNode<Results>("HUD/Root/Results/Director");
        results.Call("Hook");
        foreach (string bus in GameSettings.Buses) if (AudioServer.GetBusIndex(bus) < 0) return "missing audio bus " + bus;
        if (match.GetNodeOrNull<AudioDirector>("Audio") == null) return "audio director missing";
        bridge.Issue(new Command(CommandType.Move, 0, bridge.View.IdAt(1), EntityId.None, new Fix2(Fix64.FromInt(30), Fix64.FromInt(30))));
        bridge._PhysicsProcess(0.05); bridge._Process(0.016);
        bridge.Issue(new Command(CommandType.Surrender, 0, EntityId.None, EntityId.None, Fix2.Zero));
        bridge._PhysicsProcess(0.05); bridge._Process(0.016);
        if (!bridge.View.MatchOver || bridge.View.Winner != 1) return "surrender did not end the match";
        results._Process(2.5);
        if (!results.Shown) return "results screen not triggered by MatchOver";
        if (match.GetNode<Label>("HUD/Root/Results/Title").Text != "패배") return "wrong outcome title";
        if (match.GetNode<GridContainer>("HUD/Root/Results/Table").GetChildCount() != 12) return "results table should list header + 2 players";
        if (match.GetNode<ApmGraph>("HUD/Root/Results/Apm").SeriesCount != 2) return "APM graph needs one line per player";
        if (match.GetNode<Button>("HUD/Root/Results/Replay").Disabled) return "replay button should be available";

        float before = GameSettings.ScrollSpeed;
        GameSettings.ScrollSpeed = 1.7f; GameSettings.Volumes[1] = 0.25f; GameSettings.Save(); GameSettings.ScrollSpeed = 1f; GameSettings.Load();
        if (Mathf.Abs(GameSettings.ScrollSpeed - 1.7f) > 0.001f || Mathf.Abs(GameSettings.Volumes[1] - 0.25f) > 0.001f) return "settings did not round trip";
        GameSettings.Apply(GetViewport());
        if (Mathf.Abs(AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Music")) - Mathf.LinearToDb(0.25f)) > 0.01f) return "volume not applied";
        GameSettings.ScrollSpeed = before; GameSettings.Volumes[1] = 0.8f; GameSettings.Save();
        return "";
    }
}
