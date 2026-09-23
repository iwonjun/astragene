using Godot;
using RtsGame.Bridge;
using RtsGame.Net;
using RtsGame.Sim.Commands;
using RtsGame.Sim.World;
using RtsGame.View;

namespace RtsGame.UI;

/// <summary>End-of-match screen: outcome, per-player gathering/production/combat table and the APM graph.</summary>
public partial class Results : Node
{
    private static readonly string[] Headers = { "", "채집 Ore / Plasma", "생산 유닛 / 건물", "처치 / 손실 · 명령" };
    private Control _panel = null!;
    private MatchBridge _bridge = null!;
    private MatchHud _hud = null!;
    private bool _shown;
    private double _delay = -1;
    public bool Shown => _shown;

    public override void _Ready()
    {
        _panel = GetParent<Control>(); _panel.Hide();
        _hud = GetNode<MatchHud>("../../..");
        _panel.GetNode<Button>("Watch").Pressed += () => _panel.Hide();
        _panel.GetNode<Button>("Lobby").Pressed += () => GetTree().ChangeSceneToFile("res://game/scenes/lobby.tscn");
        _panel.GetNode<Button>("Replay").Pressed += OpenReplay;
        CallDeferred(MethodName.Hook);
    }
    private void Hook()
    {
        _bridge = _hud.Bridge;
        _bridge.SimEventRaised += e => { if (!_shown && !MatchLaunch.Tutorial && _bridge.Mode != MatchMode.Replay && (e.Kind == "MatchOver" || (e.Kind == "Defeated" && e.Entity.Index == _bridge.LocalPlayer))) _delay = 2.0; };
    }
    public override void _Process(double delta)
    {
        if (_delay < 0) return;
        _delay -= delta;
        if (_delay <= 0) { _delay = -1; Show(); }
    }

    /// <summary>Builds the screen from public end-of-game statistics.</summary>
    public void Show()
    {
        _shown = true;
        var view = _bridge.View;
        var title = _panel.GetNode<Label>("Title");
        bool over = view.MatchOver, won = over && view.Winner == _bridge.LocalPlayer;
        title.Text = won ? "승리" : !over && !view.StatsOf(_bridge.LocalPlayer).Defeated ? "경기 결과" : over && view.Winner < 0 ? "무승부" : "패배";
        title.AddThemeColorOverride("font_color", won ? new Color(0.45f, 1f, 0.85f) : new Color(1f, 0.55f, 0.55f));
        long end = view.MatchEndTick > 0 ? view.MatchEndTick : _bridge.World.TickNumber;
        int seconds = (int)(end / 20);
        _panel.GetNode<Label>("Summary").Text = $"경기 시간 {seconds / 60}:{seconds % 60:00}" + (over && view.Winner >= 0 ? $"   ·   승자 P{view.Winner + 1}" : "");
        var table = _panel.GetNode<GridContainer>("Table");
        foreach (var child in table.GetChildren()) { table.RemoveChild(child); child.QueueFree(); }
        foreach (string h in Headers) table.AddChild(Cell(h, new Color(0.55f, 0.85f, 1f)));
        var series = new System.Collections.Generic.List<(int[], Color, string)>();
        for (int p = 0; p < 4; p++)
        {
            if (!view.Participant(p)) continue;
            var s = view.StatsOf(p); var color = WorldRenderer.Team(p);
            string name = $"P{p + 1}{(p == _bridge.LocalPlayer ? " (나)" : "")}{(s.Defeated ? " ✕" : "")}";
            table.AddChild(Cell(name, color));
            table.AddChild(Cell($"{s.OreGathered:N0} / {s.PlasmaGathered:N0}", Colors.White));
            table.AddChild(Cell($"{s.UnitsProduced} / {s.BuildingsBuilt}", Colors.White));
            var apm = view.ApmOf(p); int minutes = System.Math.Max(1, apm.Length);
            table.AddChild(Cell($"{s.Kills} / {s.UnitsLost}  ·  평균 APM {s.Commands / minutes}", Colors.White));
            series.Add((apm, color, $"P{p + 1}"));
        }
        _panel.GetNode<ApmGraph>("Apm").SetSeries(series);
        _panel.GetNode<Button>("Replay").Disabled = CurrentReplay() == null;
        _panel.Show();
    }
    private static Label Cell(string text, Color color)
    {
        var label = new Label { Text = text, CustomMinimumSize = new Vector2(180, 30) };
        label.AddThemeFontSizeOverride("font_size", 18); label.AddThemeColorOverride("font_color", color);
        return label;
    }
    private ReplayLog? CurrentReplay() => _bridge.Mode == MatchMode.Network ? _bridge.Runner?.Replay : _bridge.LocalReplay;
    private void OpenReplay()
    {
        var replay = CurrentReplay(); if (replay == null) return;
        if (_bridge.Mode == MatchMode.Network) _bridge.Runner?.SaveReplay(); else _bridge.SaveLocalReplay();
        MatchLaunch.Replay = replay; MatchLaunch.Mode = MatchMode.Replay;
        GetTree().ChangeSceneToFile("res://game/scenes/replay.tscn");
    }
}
