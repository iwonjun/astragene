using System;
using Godot;
using RtsGame.Bridge;
using RtsGame.Sim.Commands;
using RtsGame.Sim.World;

namespace RtsGame.Net;

/// <summary>Replays a recorded command stream through the normal match view with speed, pause and viewpoint controls.</summary>
public partial class ReplayController : Node
{
    private static readonly int[] Speeds = { 1, 2, 4, 8 };
    private ReplayPlayer? _player;
    private MatchBridge? _bridge;
    private Label _info = null!;
    private int _speed = 1, _viewer;
    private bool _paused;
    public ReplayPlayer? Player => _player;

    public override void _Ready()
    {
        string path = MatchLaunch.Arg("replay", "user://replays/last.agr");
        ReplayLog? log = MatchLaunch.Replay;
        string error = "";
        if (log == null)
        {
            try { log = FileAccess.FileExists(path) ? ReplayLog.Decode(FileAccess.GetFileAsBytes(path)) : null; if (log == null) error = "리플레이 파일이 없습니다: " + path; }
            catch (Exception e) { error = "리플레이를 읽을 수 없습니다: " + e.Message; }
        }
        if (MatchLaunch.Flag("verify")) { Verify(log, path, error); return; }
        BuildPanel();
        if (log == null) { _info.Text = error; return; }
        _player = new ReplayPlayer(log);
        MatchLaunch.Mode = MatchMode.Replay;
        var match = GD.Load<PackedScene>("res://game/scenes/match.tscn").Instantiate();
        _bridge = match.GetNode<MatchBridge>("Bridge");
        _bridge.InitializeReplay(_player.World, 0);
        AddChild(match); MoveChild(match, 0);
        if (log.ContentHash != MatchSetup.ContentHash()) _info.Text = "경고: 밸런스 데이터가 녹화 당시와 다릅니다.";
    }

    private void Verify(ReplayLog? log, string path, string error)
    {
        if (log == null) { GD.PrintErr(error); GetTree().Quit(2); return; }
        int mismatch = ReplayPlayer.Verify(log);
        GD.Print(mismatch < 0 ? $"REPLAY OK {log.TickCount} ticks, every tick hash matches ({path})" : $"REPLAY MISMATCH at tick {mismatch} ({path})");
        GetTree().Quit(mismatch < 0 ? 0 : 1);
    }

    private void BuildPanel()
    {
        var layer = new CanvasLayer { Layer = 15 }; AddChild(layer);
        var bar = new HBoxContainer { Position = new Vector2(430, 80) }; bar.AddThemeConstantOverride("separation", 8); layer.AddChild(bar);
        bar.AddChild(MakeButton("⏯ 일시정지", () => { _paused = !_paused; }));
        foreach (int speed in Speeds) { int s = speed; bar.AddChild(MakeButton($"{s}x", () => _speed = s)); }
        bar.AddChild(MakeButton("시점 전환", () => { _viewer = (_viewer + 1) % 2; if (_bridge != null) _bridge.LocalPlayer = _viewer; }));
        bar.AddChild(MakeButton("처음부터", () => { if (_player == null || _bridge == null) return; _player.SeekTo(0); _bridge.InitializeReplay(_player.World, _viewer); }));
        bar.AddChild(MakeButton("로비", () => GetTree().ChangeSceneToFile("res://game/scenes/lobby.tscn")));
        _info = new Label { Position = new Vector2(430, 128) }; _info.AddThemeFontSizeOverride("font_size", 18); layer.AddChild(_info);
    }
    private static Button MakeButton(string text, Action pressed)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(78, 36) };
        button.Pressed += pressed; return button;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null || _bridge == null) return;
        if (!_paused && !_bridge.IsPaused) for (int i = 0; i < _speed && _player.Step(); i++) { }
        int seconds = _player.Tick / 20, total = _player.Length / 20;
        string status = _player.FirstMismatchTick >= 0 ? $"불일치: 틱 {_player.FirstMismatchTick}" : "모든 틱 해시 일치";
        _info.Text = $"REPLAY  {seconds / 60:00}:{seconds % 60:00} / {total / 60:00}:{total % 60:00}   {_speed}x{(_paused ? "  일시정지" : "")}   시점 P{_viewer + 1}   {status}";
    }
}
