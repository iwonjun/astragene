using System;
using Godot;
using RtsGame.Bridge;
using RtsGame.Sim.Commands;
using RtsGame.Sim.World;

namespace RtsGame.Net;

/// <summary>Drives a network match: polls ENet, feeds turn packets, steps 20 Hz ticks and shows wait/desync overlays.</summary>
public partial class LockstepRunner : Node
{
    private MatchBridge _bridge = null!;
    private NetworkSession? _net;
    private Label? _overlay;
    private Label Overlay { get { EnsureOverlay(); return _overlay!; } }
    private double _waiting, _rttTimer, _saveTimer;
    private bool _dumped;
    public LockstepSession? Session { get; private set; }
    public ReplayLog? Replay { get; private set; }
    public int StallFrames { get; private set; }
    public event Action<int, string, bool>? ChatReceived;

    public override void _Ready() { _bridge = GetNode<MatchBridge>("../Bridge"); EnsureOverlay(); }
    private void EnsureOverlay()
    {
        if (_overlay != null) return;
        var layer = new CanvasLayer { Layer = 20 }; AddChild(layer);
        _overlay = new Label { Visible = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore };
        _overlay.AddThemeFontOverride("font", GD.Load<Font>("res://game/assets/fonts/Pretendard-Regular.otf"));
        _overlay.AddThemeFontSizeOverride("font_size", 30); _overlay.AddThemeColorOverride("font_color", new Color(0.55f, 0.95f, 1f));
        _overlay.AddThemeColorOverride("font_outline_color", new Color(0.02f, 0.05f, 0.1f)); _overlay.AddThemeConstantOverride("outline_size", 8);
        layer.AddChild(_overlay);
    }

    /// <summary>Called by the bridge once the world exists.</summary>
    public void Begin(SimWorld world, NetworkSession net)
    {
        _net = net; Replay = new ReplayLog(world.Map, MatchSetupSpawns(), MatchLaunch.Seed);
        Session = new LockstepSession(world, net.LocalPlayer, MatchLaunch.Players, net.InputDelay, Replay);
        net.ChatReceived += (player, text, team) => ChatReceived?.Invoke(player, text, team);
        _soakTicks = int.TryParse(MatchLaunch.Arg("soak-ticks"), out int soak) ? soak : 0;
        if (MatchLaunch.Flag("soak-bot")) _bot = new SoakBot(net.LocalPlayer, MatchLaunch.Seed ^ (ulong)(net.LocalPlayer + 1));
        net.Failed += message => { Overlay.Text = message + "\n경기가 중단되었습니다. Esc 메뉴에서 나갈 수 있습니다."; Overlay.Show(); SaveReplay(); };
        Flush();
    }
    private static SpawnSpec[] MatchSetupSpawns() => MatchSetup.Spawns(MatchLaunch.Factions);

    public void QueueLocal(Command command) { if (Session?.Status != LockstepStatus.Desynced) Session?.QueueLocal(command); }

    public override void _PhysicsProcess(double delta)
    {
        if (Session == null || _net == null) return;
        _net.Poll();
        while (_net.TryDequeueTurn(out var packet)) Session.Receive(packet);
        while (_net.TryDequeueLeave(out var leave)) Session.ScheduleLeave(leave.Player, leave.Turn);
        if (Session.Status == LockstepStatus.Desynced) { OnDesync(); if (_soakTicks > 0) FinishSoak(); return; }
        if (_soakTicks > 0 && Session.World.TickNumber >= _soakTicks) { Flush(); _soakLinger += delta; if (_soakLinger > 3) FinishSoak(); return; }
        if (_bot != null && Session.World.TickNumber % 10 == 0) _bot.Think(Session);
        if (Session.TryStep()) { _waiting = 0; if (Overlay.Visible && _net.Phase != SessionPhase.Closed) Overlay.Hide(); }
        else if (Session.Status == LockstepStatus.Waiting)
        {
            StallFrames++; _waiting += delta;
            if (_waiting > 0.25 && _net.Phase != SessionPhase.Closed)
            {
                string missing = ""; for (int p = 0; p < 4; p++) if ((Session.MissingMask >> p & 1) != 0) missing += $" P{p + 1}";
                Overlay.Text = $"플레이어 대기 중…{missing}\n{_waiting:0.0}초"; Overlay.Show();
            }
        }
        if (Session.Status == LockstepStatus.Desynced) { OnDesync(); return; }
        Flush();
        _rttTimer += delta;
        if (_rttTimer >= 2) { _rttTimer = 0; Session.SetInputDelay(TurnManager.DelayForRtt(_net.RoundTripMs)); }
        _saveTimer += delta;
        if (_saveTimer >= 60) { _saveTimer = 0; SaveReplay(); }
    }
    private int _soakTicks;
    private double _soakLinger;
    private SoakBot? _bot;
    private bool _soakDone;
    /// <summary>Unattended network soak: prints a machine-readable result line, saves the replay and exits.</summary>
    private void FinishSoak()
    {
        if (_soakDone || Session == null) return;
        _soakDone = true;
        string replay = SaveReplay();
        bool desync = Session.Status == LockstepStatus.Desynced;
        GD.Print($"SOAK RESULT player={Session.LocalPlayer} ticks={Session.World.TickNumber} verifiedCheckpoints={Session.VerifiedCheckpoints} stallFrames={StallFrames} desync={(desync ? 1 : 0)} finalHash={Session.World.Hash():x16} delay={Session.InputDelay} rtt={_net?.RoundTripMs} replay={ProjectSettings.GlobalizePath(replay)}");
        GetTree().Quit(desync ? 1 : 0);
    }
    private void Flush() { while (Session!.TryDequeueOutgoing(out var packet)) _net!.SendTurn(packet); }

    public void SendChat(string text, bool team) => _net?.SendChat(text, team);

    private void OnDesync()
    {
        if (_dumped || Session?.Desync is not DesyncInfo info) return;
        _dumped = true;
        string path = WriteDesyncDump(info);
        Overlay.Text = $"동기화 오류 감지 (틱 {info.Tick}, P{info.Player + 1})\n시뮬레이션을 정지했습니다.\n{path}"; Overlay.Show();
        GD.PushError($"Desync at tick {info.Tick}: local {info.Local:x16} remote {info.Remote:x16} ({path})");
        SaveReplay();
    }

    /// <summary>
    /// Rebuilds the exact state at the mismatching checkpoint from the recorded command stream and writes
    /// it to logs/desync_{tick}.json. Each process adds its own section, so two local builds share one file.
    /// </summary>
    private string WriteDesyncDump(DesyncInfo info)
    {
        var world = Replay!.CreateWorld();
        for (int t = 0; t < info.Tick && t < Replay.TickCount; t++) world.Tick(Replay.CommandsAtTick(t));
        // Source runs write next to the project; exported builds write next to the executable.
        string dir = OS.HasFeature("template") ? OS.GetExecutablePath().GetBaseDir().PathJoin("logs") : ProjectSettings.GlobalizePath("res://logs");
        DirAccess.MakeDirRecursiveAbsolute(dir);
        string path = dir.PathJoin($"desync_{info.Tick}.json");
        string section = $"\"player{_net!.LocalPlayer}\":{{\"localHash\":\"{info.Local:x16}\",\"remoteHash\":\"{info.Remote:x16}\",\"remotePlayer\":{info.Player},\"state\":{world.DumpState()}}}";
        string existing = FileAccess.FileExists(path) ? FileAccess.GetFileAsString(path).Trim() : "";
        string content = existing.StartsWith("{") && existing.EndsWith("}") && existing.Length > 2 ? existing.Substring(0, existing.Length - 1) + "," + section + "}" : "{" + section + "}";
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write); file?.StoreString(content);
        return path;
    }

    private string _replayPath = "";
    public string SaveReplay()
    {
        if (Replay == null || Replay.TickCount == 0) return "";
        if (_replayPath == "") _replayPath = MatchLaunch.ReplayOut != "" ? MatchLaunch.ReplayOut : $"user://replays/net_{DateTime.Now:yyyyMMdd_HHmmss}_p{_net?.LocalPlayer + 1}.agr";
        return MatchLaunch.SaveReplay(Replay, _replayPath);
    }
    public override void _ExitTree() { SaveReplay(); _net?.Close(); if (ReferenceEquals(MatchLaunch.Session, _net)) MatchLaunch.Session = null; }
}
