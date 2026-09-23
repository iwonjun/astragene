using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace RtsGame.UI;

/// <summary>Player preferences persisted in user://settings.json and applied at boot and when a match loads.</summary>
public static class GameSettings
{
    public const string Path = "user://settings.json";
    public static readonly Vector2I[] Resolutions = { new(1280, 720), new(1600, 900), new(1920, 1080), new(2560, 1440) };
    public static readonly string[] QualityNames = { "낮음 (그림자·블룸 끔)", "보통", "높음 (MSAA 4x)" };
    public static readonly string[] Buses = { "Master", "Music", "SFX", "Voice", "UI" };
    public static int Resolution { get; set; } = 1;
    public static int Quality { get; set; } = 1;
    public static bool Fullscreen { get; set; }
    public static float ScrollSpeed { get; set; } = 1f;
    public static bool EdgeScroll { get; set; } = true;
    public static readonly float[] Volumes = { 1f, 0.8f, 1f, 1f, 0.9f };
    private static bool _loaded;

    public static void Load()
    {
        _loaded = true;
        if (!FileAccess.FileExists(Path)) return;
        try
        {
            var d = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(FileAccess.GetFileAsString(Path));
            if (d == null) return;
            if (d.TryGetValue("resolution", out var r)) Resolution = Math.Clamp(r.GetInt32(), 0, Resolutions.Length - 1);
            if (d.TryGetValue("quality", out var q)) Quality = Math.Clamp(q.GetInt32(), 0, 2);
            if (d.TryGetValue("fullscreen", out var f)) Fullscreen = f.GetBoolean();
            if (d.TryGetValue("scroll", out var s)) ScrollSpeed = Math.Clamp(s.GetSingle(), 0.4f, 2.5f);
            if (d.TryGetValue("edgeScroll", out var e)) EdgeScroll = e.GetBoolean();
            for (int i = 0; i < Buses.Length; i++) if (d.TryGetValue("volume" + Buses[i], out var v)) Volumes[i] = Math.Clamp(v.GetSingle(), 0f, 1f);
        }
        catch (Exception e) { GD.PushWarning("Settings: " + e.Message); }
    }
    public static void EnsureLoaded() { if (!_loaded) Load(); }
    public static void Save()
    {
        var d = new Dictionary<string, object> { ["resolution"] = Resolution, ["quality"] = Quality, ["fullscreen"] = Fullscreen, ["scroll"] = ScrollSpeed, ["edgeScroll"] = EdgeScroll };
        for (int i = 0; i < Buses.Length; i++) d["volume" + Buses[i]] = Volumes[i];
        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        file?.StoreString(JsonSerializer.Serialize(d, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Window, audio and viewport settings. Safe in headless runs (display calls are skipped).</summary>
    public static void Apply(Viewport viewport)
    {
        EnsureLoaded();
        for (int i = 0; i < Buses.Length; i++)
        {
            int bus = AudioServer.GetBusIndex(Buses[i]);
            if (bus >= 0) { AudioServer.SetBusVolumeDb(bus, Volumes[i] <= 0.001f ? -80f : Mathf.LinearToDb(Volumes[i])); AudioServer.SetBusMute(bus, Volumes[i] <= 0.001f); }
        }
        viewport.Msaa3D = Quality == 2 ? Viewport.Msaa.Msaa4X : Quality == 1 ? Viewport.Msaa.Msaa2X : Viewport.Msaa.Disabled;
        viewport.Scaling3DScale = Quality == 0 ? 0.8f : 1f;
        if (DisplayServer.GetName() == "headless") return;
        if (Fullscreen) DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        else
        {
            if (DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen) DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            var size = Resolutions[Resolution]; var screen = DisplayServer.ScreenGetUsableRect();
            size = new Vector2I(Math.Min(size.X, screen.Size.X), Math.Min(size.Y, screen.Size.Y));
            DisplayServer.WindowSetSize(size); DisplayServer.WindowSetPosition(screen.Position + (screen.Size - size) / 2);
        }
    }

    /// <summary>Per-match quality: sun shadows and bloom/tonemapping extras follow the quality level.</summary>
    public static void ApplyMatch(Node match)
    {
        EnsureLoaded();
        if (match.GetNodeOrNull<DirectionalLight3D>("Sun") is DirectionalLight3D sun) sun.ShadowEnabled = Quality > 0;
        if (match.GetNodeOrNull<WorldEnvironment>("Environment")?.Environment is Godot.Environment env)
        {
            env = (Godot.Environment)env.Duplicate(); env.GlowEnabled = Quality > 0 && env.GlowEnabled; env.SsaoEnabled = Quality == 2 && env.SsaoEnabled;
            match.GetNode<WorldEnvironment>("Environment").Environment = env;
        }
    }
}
