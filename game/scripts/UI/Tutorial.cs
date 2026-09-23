using System;
using Godot;
using RtsGame.Bridge;
using RtsGame.Net;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Data;
using RtsGame.Sim.Entities;
using RtsGame.View;

namespace RtsGame.UI;

/// <summary>
/// Guided first match. Each step explains one idea, points at the thing to click (world marker and
/// command-card glow) and advances by itself when the player has actually done it. Reads only the
/// local VisibilityFilter and never issues commands for the player.
/// </summary>
public partial class Tutorial : Node
{
    private sealed record Step(string Title, Func<string> Body, Func<bool> Done, Func<Vector3?>? Marker = null, Func<int>? Highlight = null, Func<string>? Progress = null, bool Manual = false);
    public const string DoneFlag = "user://tutorial_done";
    private MatchHud _hud = null!;
    private MatchBridge _bridge = null!;
    private RtsCamera _camera = null!;
    private Control _panel = null!;
    private Label _title = null!, _body = null!, _progress = null!, _stepLabel = null!;
    private Button _next = null!;
    private Step[] _steps = Array.Empty<Step>();
    private int _index = -1;
    private double _hold, _time;
    private Vector3 _cameraStart;
    private float _altitudeStart;
    private Node3D? _marker;
    public int Index => _index;
    public bool Active => _index >= 0 && _index < _steps.Length;

    public override void _Ready()
    {
        _panel = GetParent<Control>();
        _hud = GetNode<MatchHud>("../../..");
        _title = _panel.GetNode<Label>("Title"); _body = _panel.GetNode<Label>("Body"); _progress = _panel.GetNode<Label>("Progress"); _stepLabel = _panel.GetNode<Label>("Step");
        _body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _next = _panel.GetNode<Button>("Next"); _next.Pressed += Advance;
        _panel.GetNode<Button>("Close").Pressed += Finish;
        _panel.Hide();
        if (!MatchLaunch.Tutorial) { SetProcess(false); return; }
        CallDeferred(MethodName.Begin);
    }

    private void Begin()
    {
        _bridge = _hud.Bridge; _camera = GetNode<RtsCamera>("../../../../Camera");
        _steps = BuildSteps();
        _panel.Show(); _index = -1; Advance();
    }

    private string Key(int slot) => _hud.KeyName(slot);
    private int Own(Func<EntitySnapshot, bool> filter)
    {
        int n = 0; var view = _bridge.View;
        for (int i = 0; i < view.Capacity; i++) { var id = view.IdAt(i); if (id != EntityId.None) { var e = view.Get(id); if (e.Owner.Player == _bridge.LocalPlayer && filter(e)) n++; } }
        return n;
    }
    private int Enemy(Func<EntitySnapshot, bool> filter)
    {
        // The tutorial knows the enemy base layout, but still checks it only where the player can see or remembers it.
        int n = 0; var view = _bridge.View;
        for (int i = 0; i < view.Capacity; i++)
        {
            var id = view.IdAt(i); EntitySnapshot e;
            if (id != EntityId.None) e = view.Get(id); else if (!view.TryGhost(i, out e)) continue;
            if (e.Owner.Player != _bridge.LocalPlayer && e.Owner.Player >= 0 && filter(e)) n++;
        }
        return n;
    }
    private static bool IsWorker(EntitySnapshot e) => !e.Type.IsBuilding && DefDatabase.Units[e.Type.Definition].Worker;
    private int Gathering() => Own(e => IsWorker(e) && _bridge.View.OwnState(e.Id) == UnitState.Gathering);
    private EntitySnapshot? First(Func<EntitySnapshot, bool> filter)
    {
        var view = _bridge.View;
        for (int i = 0; i < view.Capacity; i++) { var id = view.IdAt(i); if (id != EntityId.None) { var e = view.Get(id); if (e.Owner.Player == _bridge.LocalPlayer && filter(e)) return e; } }
        return null;
    }
    private bool SelectedIs(Func<EntitySnapshot, bool> filter)
    {
        foreach (var id in _hud.Selection.Selected) if (_bridge.View.TryGet(id, out var e) && filter(e)) return true;
        return false;
    }
    private Vector3? NearestOre()
    {
        var hq = First(e => e.Type.IsBuilding && e.Type.Definition is 0 or 6); if (hq is not EntitySnapshot h) return null;
        var home = _bridge.EntityPosition(h); Vector3? best = null; float bestDistance = float.MaxValue;
        for (int z = 0; z < 128; z++) for (int x = 0; x < 128; x++)
        {
            int node = _bridge.World.Map.Grid[x, z].ResourceNodeId; if (node < 0 || node % 10 is 5 or 9) continue;
            var p = _bridge.SurfacePosition(new Sim.Core.Fix2(Sim.Core.Fix64.FromRatio(x * 2 + 1, 2), Sim.Core.Fix64.FromRatio(z * 2 + 1, 2)));
            float d = p.DistanceSquaredTo(home); if (d < bestDistance) { bestDistance = d; best = p; }
        }
        return best;
    }
    private Vector3? EnemyBase() => new Vector3(109, 0, 109);
    private int Queued(int unit)
    {
        int n = 0;
        var view = _bridge.View;
        for (int i = 0; i < view.Capacity; i++)
        {
            var id = view.IdAt(i); if (id == EntityId.None) continue; var e = view.Get(id);
            if (e.Owner.Player != _bridge.LocalPlayer || !e.Type.IsBuilding) continue;
            for (int q = 0; q < 5; q++) if (view.ProductionAt(id, q).Definition == unit) n++;
        }
        return n;
    }

    private Step[] BuildSteps() => new Step[]
    {
        new("ASTRAGENE에 오신 것을 환영합니다",
            () => "당신은 LUMINA 연합의 지휘관입니다. 목표는 단순합니다.\n① 일꾼으로 자원을 모으고  ② 건물을 지어 병력을 만들고  ③ 적 본진(지도 오른쪽 아래 끝)을 파괴하세요.\n이 창의 안내를 따라 하면 한 단계씩 자동으로 넘어갑니다.",
            () => false, Manual: true),
        new("1. 카메라 움직이기",
            () => "W A S D 키 또는 마우스를 화면 가장자리로 가져가면 카메라가 움직입니다.\n마우스 휠로 확대·축소할 수 있습니다. 주변을 조금 둘러보세요.",
            () => _camera.Focus.DistanceTo(_cameraStart) > 8 || Mathf.Abs(_camera.Altitude - _altitudeStart) > 5),
        new("2. 일꾼 선택하기",
            () => "화면 빈 곳에서 마우스 왼쪽 버튼을 누른 채 드래그해 작은 일꾼(Engineer)들을 상자로 감싸세요.\n선택된 유닛 발밑에 링이 생기고 아래 패널에 표시됩니다. 유닛을 한 번 클릭해도 선택됩니다.",
            () => SelectedIs(IsWorker),
            Marker: () => First(IsWorker) is EntitySnapshot w ? _bridge.EntityPosition(w) : null),
        new("3. 광물 채집하기",
            () => "일꾼을 선택한 채로, 표시된 파란 광물 결정(Ore) 위에서 마우스 오른쪽 버튼을 클릭하세요.\n일꾼이 광물을 캐서 본진으로 나르고, 화면 위 ORE 숫자가 올라갑니다.",
            () => Gathering() >= 1,
            Marker: NearestOre),
        new("4. 모든 일꾼 일 시키기",
            () => "놀고 있는 일꾼이 없도록 나머지 일꾼도 모두 광물에 보내세요.\n광물 하나에는 동시에 2명까지 캘 수 있으니, 여러 결정에 나눠 우클릭하면 더 빠릅니다.\n팁: 일꾼을 더블클릭하면 화면 안의 같은 종류가 모두 선택됩니다.",
            () => Gathering() >= Math.Min(5, Own(IsWorker)),
            Marker: NearestOre,
            Progress: () => $"채집 중인 일꾼 {Gathering()} / {Own(IsWorker)}"),
        new("5. 일꾼 더 뽑기",
            () => $"본진(가운데 큰 건물 Core)을 클릭하고, 오른쪽 아래 명령 카드에서 [{Key(0)}] Engineer를 누르세요(55 Ore).\n생산 중인 유닛은 선택 패널 아래 칸에 보이며, 칸을 클릭하면 취소하고 전액 돌려받습니다.",
            () => Queued(0) > 0 || Own(IsWorker) >= 7,
            Marker: () => First(e => e.Type.IsBuilding && e.Type.Definition == 0) is EntitySnapshot h ? _bridge.EntityPosition(h) : null,
            Highlight: () => SelectedIs(e => e.Type.IsBuilding && e.Type.Definition == 0) ? 0 : -1),
        new("6. 보급 늘리기 (Beacon)",
            () => $"화면 위 SUPPLY 는 '사용 중 / 최대' 인구입니다. 최대에 닿으면 더 생산할 수 없습니다.\n일꾼 선택 → [{Key(8)}] 건설 → [{Key(1)}] Beacon(90 Ore) → 본진 근처 빈 땅을 왼쪽 클릭하세요.\n청록색 윤곽이면 지을 수 있는 자리, 빨간색이면 안 되는 자리입니다. 우클릭이나 Esc로 취소합니다.",
            () => Own(e => e.Type.IsBuilding && e.Type.Definition == 1) > 0,
            Highlight: () => _hud.MenuLevel == 1 ? 1 : SelectedIs(IsWorker) ? 8 : -1,
            Progress: () => $"보유 Ore {_bridge.View.Resources.Ore} / 필요 90"),
        new("7. 병영 짓기 (Drill Hall)",
            () => $"병력을 뽑으려면 병영이 필요합니다. 일꾼 선택 → [{Key(8)}] 건설 → [{Key(2)}] Drill Hall(145 Ore).\n광물이 부족하면 잠시 기다리세요. LUMINA 일꾼은 건설하는 동안 그 자리에 붙어 있습니다.",
            () => Own(e => e.Type.IsBuilding && e.Type.Definition == 2) > 0,
            Highlight: () => _hud.MenuLevel == 1 ? 2 : SelectedIs(IsWorker) ? 8 : -1,
            Progress: () => $"보유 Ore {_bridge.View.Resources.Ore} / 필요 145"),
        new("8. 병력 생산 (Trooper 4기)",
            () => $"Drill Hall이 다 지어지면(선택 패널의 '건설 중' 표시가 사라지면) 클릭하고 [{Key(0)}] Trooper를 4기 생산하세요.\n팁: 건물을 선택하고 땅을 우클릭하면 새 유닛이 모일 '집결 지점'이 정해집니다.",
            () => Own(e => !e.Type.IsBuilding && e.Type.Definition == 1) >= 4,
            Marker: () => First(e => e.Type.IsBuilding && e.Type.Definition == 2) is EntitySnapshot b ? _bridge.EntityPosition(b) : null,
            Highlight: () => SelectedIs(e => e.Type.IsBuilding && e.Type.Definition == 2) ? 0 : -1,
            Progress: () => $"Trooper {Own(e => !e.Type.IsBuilding && e.Type.Definition == 1)} / 4  (생산 대기 {Queued(1)})"),
        new("9. 적 본진 공격",
            () => $"Trooper들을 드래그로 선택하고 [{Key(1)}] 공격 이동을 누른 뒤, 표시된 적 본진 쪽 땅(또는 왼쪽 아래 전술 지도)을 클릭하세요.\n공격 이동 중인 유닛은 가는 길에 만나는 적과 싸웁니다. 적 위에서 우클릭하면 그 대상을 직접 공격합니다.\n적 본진 Core를 파괴하면 튜토리얼 완료!",
            () => SeenEnemyBase && Enemy(e => e.Type.IsBuilding && e.Type.Definition is 0 or 6) == 0,
            Marker: EnemyBase,
            Highlight: () => SelectedIs(e => !e.Type.IsBuilding && e.Type.Definition == 1) ? 1 : -1),
        new("튜토리얼 완료!",
            () => "축하합니다. RTS의 기본을 모두 익혔습니다.\n더 알아두면 좋은 것: Ctrl+숫자 = 부대 지정, 숫자 = 부대 불러오기(두 번 누르면 카메라 이동), Shift = 명령 예약, Space = 최근 알림 위치로 이동, Esc = 메뉴.\n이제 로비에서 'AI와 대전'(쉬움)을 해 보세요!",
            () => false, Manual: true),
    };
    private bool _seenEnemyBase;
    private bool SeenEnemyBase => _seenEnemyBase |= _bridge.View.At(109, 109) != Sim.Systems.Visibility.Unexplored;

    private void Advance()
    {
        if (_index == _steps.Length - 1) { Finish(); return; }
        _index++; _hold = 0; _time = 0;
        _cameraStart = _camera.Focus; _altitudeStart = _camera.Altitude;
        var step = _steps[_index];
        _stepLabel.Text = _index == 0 ? "튜토리얼" : _index == _steps.Length - 1 ? "완료" : $"단계 {_index} / {_steps.Length - 2}";
        _title.Text = step.Title; _next.Text = step.Manual ? (_index == _steps.Length - 1 ? "로비로" : "시작하기") : "건너뛰기";
        if (_index == 2) _camera.Jump(new Vector3(21, 0, 24));
        if (_index == _steps.Length - 1) { using var f = FileAccess.Open(DoneFlag, FileAccess.ModeFlags.Write); f?.StoreString("1"); }
        Refresh();
    }
    private void Finish()
    {
        _index = _steps.Length; _panel.Hide(); _hud.Highlight(-1); _marker?.QueueFree(); _marker = null; SetProcess(false);
        using (var f = FileAccess.Open(DoneFlag, FileAccess.ModeFlags.Write)) f?.StoreString("1");
        GetTree().ChangeSceneToFile("res://game/scenes/lobby.tscn");
    }

    private void Refresh()
    {
        var step = _steps[_index];
        _body.Text = step.Body();
        _progress.Text = step.Progress?.Invoke() ?? (step.Manual ? "" : "진행 중…");
        _hud.Highlight(step.Highlight?.Invoke() ?? -1);
        var target = step.Marker?.Invoke();
        if (target is Vector3 p) { EnsureMarker(); _marker!.Visible = true; _marker.Position = p + new Vector3(0, 0.15f, 0); }
        else if (_marker != null) _marker.Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!Active || _bridge?.World == null) return;
        _time += delta;
        var step = _steps[_index];
        Refresh();
        if (_marker != null && _marker.Visible) { float t = (float)Time.GetTicksMsec() / 1000f; _marker.GetChild<Node3D>(1).Position = new Vector3(0, 2.2f + Mathf.Sin(t * 4) * 0.3f, 0); _marker.GetChild<Node3D>(0).Scale = Vector3.One * (1 + 0.12f * Mathf.Sin(t * 5)); }
        if (step.Manual || !step.Done()) { _hold = 0; return; }
        _hold += delta;
        if (_hold == delta) { _progress.Text = "✔ 완료!"; _hud.Notify("잘했어요! 다음 단계로 넘어갑니다.", false); }
        if (_hold > 1.2) Advance();
    }

    private void EnsureMarker()
    {
        if (_marker != null) return;
        var material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1f, 0.85f, 0.2f, 0.85f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, NoDepthTest = true };
        _marker = new Node3D { Name = "TutorialMarker" };
        _marker.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 1.1f, OuterRadius = 1.35f, Rings = 32, RingSegments = 8 }, MaterialOverride = material });
        _marker.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.45f, BottomRadius = 0f, Height = 0.9f, RadialSegments = 12 }, MaterialOverride = material });
        _hud.GetParent().AddChild(_marker);
    }
}
