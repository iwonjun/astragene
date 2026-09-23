using Godot;

namespace RtsGame.UI;

/// <summary>Settings screen: resolution, quality, fullscreen, volumes, camera scroll and command-card hotkeys.</summary>
public partial class SettingsMenu : Control
{
    private Hotkeys _keys = new();
    private readonly Button[] _keyButtons = new Button[12];
    private readonly HSlider[] _volumes = new HSlider[5];
    private int _editing = -1;
    private Label _status = null!;

    public override void _Ready()
    {
        GameSettings.EnsureLoaded();
        var resolution = GetNode<OptionButton>("Root/Display/Resolution");
        foreach (var r in GameSettings.Resolutions) resolution.AddItem($"{r.X} × {r.Y}");
        resolution.Select(GameSettings.Resolution);
        var quality = GetNode<OptionButton>("Root/Display/Quality");
        foreach (var q in GameSettings.QualityNames) quality.AddItem(q);
        quality.Select(GameSettings.Quality);
        var fullscreen = GetNode<CheckButton>("Root/Display/Fullscreen"); fullscreen.ButtonPressed = GameSettings.Fullscreen;
        var scroll = GetNode<HSlider>("Root/Display/Scroll"); scroll.Value = GameSettings.ScrollSpeed;
        var edge = GetNode<CheckButton>("Root/Display/EdgeScroll"); edge.ButtonPressed = GameSettings.EdgeScroll;
        for (int i = 0; i < 5; i++)
        {
            int bus = i; _volumes[i] = GetNode<HSlider>("Root/Sound/" + GameSettings.Buses[i]); _volumes[i].Value = GameSettings.Volumes[i];
            _volumes[i].ValueChanged += v => { GameSettings.Volumes[bus] = (float)v; GameSettings.Apply(GetViewport()); }; // live preview
        }
        for (int i = 0; i < 12; i++) { int slot = i; _keyButtons[i] = GetNode<Button>($"Root/Keys/Key{i}"); _keyButtons[i].Pressed += () => { _editing = slot; _keyButtons[slot].Text = "키를 누르세요"; }; }
        GetNode<Button>("Root/Keys/Reset").Pressed += () => { if (FileAccess.FileExists("user://hotkeys.json")) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://hotkeys.json")); _keys = new Hotkeys(); _status.Text = "단축키를 기본값으로 되돌렸습니다."; RefreshKeys(); };
        _status = GetNode<Label>("Root/Status");
        GetNode<Button>("Root/Back").Pressed += () => { GameSettings.Load(); GameSettings.Apply(GetViewport()); GetTree().ChangeSceneToFile("res://game/scenes/lobby.tscn"); };
        GetNode<Button>("Root/Save").Pressed += () =>
        {
            GameSettings.Resolution = resolution.Selected; GameSettings.Quality = quality.Selected; GameSettings.Fullscreen = fullscreen.ButtonPressed;
            GameSettings.ScrollSpeed = (float)scroll.Value; GameSettings.EdgeScroll = edge.ButtonPressed;
            GameSettings.Save(); _keys.Save(); GameSettings.Apply(GetViewport());
            _status.Text = "저장했습니다. 그래픽 품질은 다음 경기부터 적용됩니다.";
        };
        RefreshKeys();
        Resized += Layout; Layout();
    }
    private void RefreshKeys() { for (int i = 0; i < 12; i++) _keyButtons[i].Text = $"칸 {i + 1}   [{_keys[i]}]"; }
    private void Layout()
    {
        var root = GetNode<Control>("Root"); var size = GetViewportRect().Size; float scale = Mathf.Min(size.X / 1440f, size.Y / 900f);
        root.Scale = new Vector2(scale, scale); root.Position = (size - new Vector2(1440, 900) * scale) / 2;
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if (_editing < 0 || input is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (_keys.Assign(_editing, key.PhysicalKeycode)) { _editing = -1; RefreshKeys(); } else _status.Text = "WASD를 제외한 영문 키를 지정하세요.";
        GetViewport().SetInputAsHandled();
    }
}
