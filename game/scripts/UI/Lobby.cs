using System;
using Godot;
using RtsGame.Net;
using RtsGame.Sim.World;

namespace RtsGame.UI;

/// <summary>Main menu and direct-IP lobby. Lobby state lives on the host and arrives as Session commands.</summary>
public partial class Lobby : Control
{
    private static readonly string[] FactionNames = { "LUMINA 연합", "VERGE 군체" };
    private NetworkSession? _net;
    private Label _players = null!, _status = null!, _seed = null!;
    private OptionButton _faction = null!, _map = null!, _difficulty = null!;
    private CheckButton _ready = null!;
    private Button _host = null!, _join = null!, _start = null!, _leave = null!, _local = null!;
    private LineEdit _address = null!, _port = null!;
    private bool _autoReady, _autoStart;
    private ulong _mapHash;

    public override void _Ready()
    {
        MatchLaunch.ResetLocal();
        string menu = "Root/Menu/";
        _players = GetNode<Label>("Root/Room/Players"); _status = GetNode<Label>("Root/Room/Status"); _seed = GetNode<Label>("Root/Room/Seed");
        _players.AutowrapMode = TextServer.AutowrapMode.WordSmart; _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _faction = GetNode<OptionButton>(menu + "Faction"); _map = GetNode<OptionButton>(menu + "Map"); _difficulty = GetNode<OptionButton>(menu + "Difficulty");
        _ready = GetNode<CheckButton>(menu + "Ready"); _start = GetNode<Button>(menu + "Start"); _leave = GetNode<Button>(menu + "Leave");
        _host = GetNode<Button>(menu + "Host"); _join = GetNode<Button>(menu + "Join"); _local = GetNode<Button>(menu + "Local");
        _address = GetNode<LineEdit>(menu + "Address"); _port = GetNode<LineEdit>(menu + "Port");
        foreach (string name in FactionNames) _faction.AddItem(name);
        _map.AddItem("Duel · 2인 대칭 맵");
        foreach (string name in new[] { "쉬움", "보통", "어려움" }) _difficulty.AddItem(name);
        _difficulty.Select(1);
        _mapHash = MapLoader.Load(FileAccess.GetFileAsBytes(MatchLaunch.DefaultMap)).Hash();

        _local.Pressed += StartLocal;
        var tutorial = GetNode<Button>(menu + "Tutorial"); tutorial.Pressed += StartTutorial;
        _firstRun = !FileAccess.FileExists(Tutorial.DoneFlag);
        _tutorialButton = tutorial;
        _host.Pressed += () => Host(ParsePort(_port.Text));
        _join.Pressed += () => Join(_address.Text.Trim(), ParsePort(_port.Text));
        _faction.ItemSelected += index => _net?.SetFaction((int)index);
        _ready.Toggled += on => _net?.SetReady(on);
        _start.Pressed += () => { if (_net?.StartMatch() != true) _status.Text = "모든 플레이어가 준비해야 시작할 수 있습니다."; };
        _leave.Pressed += Disconnect;
        GetNode<Button>(menu + "Replay").Pressed += () => { MatchLaunch.Mode = MatchMode.Replay; GetTree().ChangeSceneToFile("res://game/scenes/replay.tscn"); };
        GetNode<Button>(menu + "Quit").Pressed += () => GetTree().Quit();
        var settings = GetNode<Button>(menu + "Settings"); settings.Pressed += OpenSettings; settings.Disabled = !ResourceLoader.Exists("res://game/scenes/settings.tscn");
        GetNode<Button>(menu + "Replay").Disabled = !FileAccess.FileExists("user://replays/last.agr");
        Resized += Layout; Layout();
        Refresh();

        // Unattended soak runs: --host[=port] / --join=addr[:port] / --faction=N / --auto-ready / --auto-start.
        _autoReady = MatchLaunch.Flag("auto-ready"); _autoStart = MatchLaunch.Flag("auto-start");
        MatchLaunch.ReplayOut = MatchLaunch.Arg("replay-out");
        if (int.TryParse(MatchLaunch.Arg("faction"), out int faction) && (uint)faction <= 1) _faction.Select(faction);
        string hostArg = MatchLaunch.Arg("host", MatchLaunch.Flag("host") ? NetworkSession.DefaultPort.ToString() : "");
        string joinArg = MatchLaunch.Arg("join");
        if (hostArg != "") Host(ParsePort(hostArg));
        else if (joinArg != "")
        {
            string[] parts = joinArg.Split(':');
            Join(parts[0], parts.Length > 1 ? ParsePort(parts[1]) : NetworkSession.DefaultPort);
        }
    }
    private void Layout()
    {
        var root = GetNode<Control>("Root");
        var size = GetViewportRect().Size; float scale = Mathf.Min(size.X / 1440f, size.Y / 900f);
        root.Scale = new Vector2(scale, scale); root.Position = (size - new Vector2(1440, 900) * scale) / 2;
    }
    private static int ParsePort(string text) => int.TryParse(text, out int port) && port is > 1024 and < 65536 ? port : NetworkSession.DefaultPort;

    private void OpenSettings() { if (ResourceLoader.Exists("res://game/scenes/settings.tscn")) GetTree().ChangeSceneToFile("res://game/scenes/settings.tscn"); }

    private bool _firstRun;
    private Button _tutorialButton = null!;
    private void StartTutorial()
    {
        Disconnect();
        MatchLaunch.ResetLocal(); MatchLaunch.Tutorial = true;
        MatchLaunch.Factions = new[] { 0, 1 }; MatchLaunch.Seed = 1;
        GetTree().ChangeSceneToFile("res://game/scenes/match.tscn");
    }
    private void StartLocal()
    {
        Disconnect();
        MatchLaunch.Mode = MatchMode.Local; MatchLaunch.LocalPlayer = 0;
        int faction = _faction.Selected < 0 ? 0 : _faction.Selected;
        MatchLaunch.Factions = new[] { faction, 1 - faction }; MatchLaunch.Players = new[] { 0, 1 };
        MatchLaunch.Seed = GD.Randi() | (ulong)GD.Randi() << 32;
        MatchLaunch.AiDifficulty = _difficulty.Selected;
        GetTree().ChangeSceneToFile("res://game/scenes/match.tscn");
    }
    private void Host(int port)
    {
        Disconnect();
        Attach(NetworkSession.Host(port, Math.Max(0, _faction.Selected), _mapHash));
        if (_net?.Phase == SessionPhase.Lobby) _status.Text = $"포트 {port}에서 대기 중. 상대가 이 PC의 IP로 접속합니다.";
        if (_autoReady && _net != null) { _ready.ButtonPressed = true; _net.SetReady(true); }
    }
    private void Join(string address, int port)
    {
        Disconnect();
        if (address == "") { _status.Text = "호스트 IP를 입력하세요."; return; }
        Attach(NetworkSession.Join(address, port, _mapHash));
        if (_net != null && _net.Phase != SessionPhase.Closed) _status.Text = $"{address}:{port} 접속 중…";
    }
    private void Attach(NetworkSession session)
    {
        _net = session; MatchLaunch.Session = session;
        session.LobbyChanged += OnLobbyChanged; session.Failed += OnFailed; session.Started += OnStarted; session.ChatReceived += OnChat;
        if (session.Phase == SessionPhase.Closed) { _status.Text = session.LastError; _net = null; MatchLaunch.Session = null; }
        Refresh();
    }
    private void Detach(NetworkSession session) { session.LobbyChanged -= OnLobbyChanged; session.Failed -= OnFailed; session.Started -= OnStarted; session.ChatReceived -= OnChat; }
    private void OnFailed(string message) { _status.Text = message; if (_net != null) Detach(_net); _net = null; MatchLaunch.Session = null; Refresh(); }
    private void OnChat(int player, string text, bool team) => _status.Text = $"P{player + 1}: {text}";
    private void OnLobbyChanged()
    {
        if (_net == null) return;
        if (_autoReady && !_net.IsHost && _net.LocalPlayer > 0 && !_net.Slots.Ready(_net.LocalPlayer))
        {
            if (int.TryParse(MatchLaunch.Arg("faction"), out int f) && (uint)f <= 1 && _net.Slots.Faction(_net.LocalPlayer) != f) _net.SetFaction(f);
            else { _ready.SetPressedNoSignal(true); _net.SetReady(true); }
        }
        if (_autoStart && _net.CanStart) _net.StartMatch();
        Refresh();
    }
    private void OnStarted()
    {
        if (_net == null) return;
        var players = _net.Slots.Players();
        var factions = new int[players.Length];
        for (int i = 0; i < players.Length; i++) { if (players[i] != i) { _status.Text = "좌석 배치 오류"; return; } factions[i] = _net.Slots.Faction(i); }
        MatchLaunch.Mode = MatchMode.Network; MatchLaunch.Players = players; MatchLaunch.Factions = factions;
        MatchLaunch.Seed = _net.Seed; MatchLaunch.InputDelay = _net.InputDelay; MatchLaunch.LocalPlayer = _net.LocalPlayer;
        Detach(_net); _net = null; // ownership moves to the match scene
        GetTree().ChangeSceneToFile("res://game/scenes/match.tscn");
    }
    private void Disconnect()
    {
        if (_net != null) { Detach(_net); _net.Close(); } _net = null; MatchLaunch.Session = null; _ready.SetPressedNoSignal(false); Refresh();
    }

    public override void _Process(double delta)
    {
        _net?.Poll();
        // First launch: make the tutorial the obvious first click.
        if (_firstRun) _tutorialButton.Modulate = Colors.White.Lerp(new Color(1.5f, 1.3f, 0.5f), 0.5f + 0.5f * Mathf.Sin(Time.GetTicksMsec() * 0.006f));
    }
    public override void _ExitTree() { if (_net != null) Detach(_net); }

    private void Refresh()
    {
        bool connected = _net != null && _net.Phase != SessionPhase.Closed;
        bool inLobby = connected && _net!.Phase == SessionPhase.Lobby;
        _host.Disabled = connected; _join.Disabled = connected; _address.Editable = !connected; _port.Editable = !connected;
        _ready.Disabled = !inLobby; _leave.Disabled = !connected; _start.Visible = !connected || _net!.IsHost; _start.Disabled = !(inLobby && _net!.CanStart);
        _local.Disabled = connected;
        if (!connected && _firstRun && _status.Text == "") _status.Text = "처음이신가요? 왼쪽 위 '튜토리얼'에서 5분이면 기본 조작을 배울 수 있습니다.";
        if (!connected) { _players.Text = "방을 만들거나 호스트에 접속하세요.\n\n· 최대 2인 (Duel 맵)\n· 모든 PC가 전체 시뮬레이션을 계산하는 락스텝 방식\n· 전장의 안개는 화면 표시용이며 치트 방지 수단이 아닙니다."; _seed.Text = ""; return; }
        if (_net!.Phase == SessionPhase.Connecting) { _players.Text = "호스트 응답 대기 중…"; return; }
        string text = "";
        for (int s = 0; s < NetworkSession.MapCapacity; s++)
        {
            var slots = _net.Slots;
            string who = s == _net.LocalPlayer ? " (나)" : s == 0 ? " (호스트)" : "";
            text += slots.Occupied(s) ? $"P{s + 1}{who}   {FactionNames[slots.Faction(s)]}   {(slots.Ready(s) ? "● 준비" : "○ 대기")}\n" : $"P{s + 1}   빈 자리\n";
        }
        _players.Text = text;
        _seed.Text = $"시드 {_net.Seed:X16}   입력 지연 {_net.InputDelay}턴";
        if (_net.LocalPlayer >= 0 && _faction.Selected != _net.Slots.Faction(_net.LocalPlayer)) _faction.Select(_net.Slots.Faction(_net.LocalPlayer));
    }
}
