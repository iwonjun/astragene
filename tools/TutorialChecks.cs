using Godot;
using RtsGame.Bridge;
using RtsGame.Net;
using RtsGame.UI;
using RtsGame.View;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Entities;

/// <summary>Plays the tutorial the way a newcomer would and checks that each step advances on the real action.</summary>
public partial class TutorialChecks : Node
{
    public Node CreateMatch()
    {
        MatchLaunch.ResetLocal(); MatchLaunch.Tutorial = true;
        return GD.Load<PackedScene>("res://game/scenes/match.tscn").Instantiate();
    }
    public string RunChecks(Node match)
    {
        try
        {
            var bridge = match.GetNode<MatchBridge>("Bridge"); var hud = match.GetNode<MatchHud>("HUD");
            var tutorial = match.GetNode<Tutorial>("HUD/Root/Tutorial/Director"); var camera = match.GetNode<RtsCamera>("Camera");
            var selection = match.GetNode<SelectionController>("Selection");
            if (bridge.World.TickNumber != 0) return "unexpected start tick";
            tutorial.Call("Begin");
            if (tutorial.Index != 0 || !match.GetNode<Control>("HUD/Root/Tutorial").Visible) return "welcome step not shown";
            // Idle start: the tutorial teaches mining itself.
            Tick(bridge, 2);
            for (int i = 0; i < bridge.View.Capacity; i++) { var id = bridge.View.IdAt(i); if (id != EntityId.None && bridge.View.OwnState(id) == UnitState.Gathering) return "workers must start idle in the tutorial"; }
            match.GetNode<Button>("HUD/Root/Tutorial/Next").EmitSignal(Button.SignalName.Pressed);
            if (tutorial.Index != 1) return "manual advance failed";
            camera.Jump(camera.Focus + new Vector3(12, 0, 0)); Pump(tutorial);
            if (tutorial.Index != 2) return "camera step did not complete";
            var worker = bridge.View.IdAt(1); selection.SelectOnly(worker); Pump(tutorial);
            if (tutorial.Index != 3) return "selection step did not complete";
            int rx = -1, rz = -1;
            for (int z = 0; z < 45; z++) for (int x = 0; x < 45; x++) { int n = bridge.World.Map.Grid[x, z].ResourceNodeId; if (n == 0) { rx = x; rz = z; } }
            bridge.IssueContext(worker, new Vector3(rx + 0.5f, 3, rz + 0.5f), false); Tick(bridge, 1); Pump(tutorial);
            if (tutorial.Index != 4) return "gather step did not complete";
            foreach (var c in bridge.StartingOrders()) bridge.Issue(c);
            Tick(bridge, 1); Pump(tutorial);
            if (tutorial.Index != 5) return "all-workers step did not complete";
            var core = bridge.View.IdAt(0); selection.SelectOnly(core); hud.Refresh(); tutorial._Process(0.01);
            if (hud.MenuLevel != 0) return "unexpected menu";
            hud.ActivateSlot(0); Tick(bridge, 1); Pump(tutorial);
            if (tutorial.Index != 6) return "train step did not complete";
            selection.SelectOnly(worker); hud.Refresh(); tutorial._Process(0.01); hud.ActivateSlot(8);
            if (hud.MenuLevel != 1) return "build submenu not opened";
            return "";
        }
        finally { MatchLaunch.ResetLocal(); }
    }
    private static void Tick(MatchBridge bridge, int n) { for (int i = 0; i < n; i++) bridge._PhysicsProcess(0.05); }
    private static void Pump(Tutorial t) { t._Process(0.01); t._Process(1.3); }
}
