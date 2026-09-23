using System.Collections.Generic;
using Godot;
using RtsGame.Bridge;
using RtsGame.Sim.Commands;
using RtsGame.Sim.Data;
using RtsGame.Sim.Entities;
using RtsGame.Sim.World;

namespace RtsGame.View;

/// <summary>
/// Presentation-only sound: selection/command voices per faction, pooled positional combat SFX, production and
/// outcome stingers, and the BGM slot. Reads the bridge and view events; never touches simulation state.
/// </summary>
public partial class AudioDirector : Node
{
    private const string Dir = "res://game/assets/generated/audio/";
    private MatchBridge _bridge = null!;
    private SelectionController _selection = null!;
    private RtsCamera _camera = null!;
    private AudioStreamPlayer _music = null!, _voice = null!, _ui = null!;
    private readonly List<AudioStreamPlayer3D> _pool = new();
    private int _next;
    private readonly Dictionary<string, AudioStream> _streams = new();
    private ulong _selectionSignature, _lastVoice, _lastAck;
    private readonly ulong[] _lastKind = new ulong[4];
    public int SfxPlayed { get; private set; }
    public int VoicesPlayed { get; private set; }

    private AudioStream Stream(string name) { if (!_streams.TryGetValue(name, out var s)) _streams[name] = s = GD.Load<AudioStream>(Dir + name + ".tres"); return s; }

    public override void _Ready()
    {
        _bridge = GetNode<MatchBridge>("../Bridge"); _selection = GetNode<SelectionController>("../Selection"); _camera = GetNode<RtsCamera>("../Camera");
        _music = new AudioStreamPlayer { Stream = Stream("bgm_ambient"), Bus = "Music", VolumeDb = -6 }; AddChild(_music);
        _voice = new AudioStreamPlayer { Bus = "Voice" }; AddChild(_voice);
        _ui = new AudioStreamPlayer { Bus = "UI" }; AddChild(_ui);
        for (int i = 0; i < 12; i++) { var p = new AudioStreamPlayer3D { Bus = "SFX", UnitSize = 18, MaxDistance = 90, AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance }; AddChild(p); _pool.Add(p); }
        if (MatchNet.Benchmark) return;
        // Headless runs (CI, soak, replay verification) use the dummy driver; skip the endless BGM playback there.
        if (DisplayServer.GetName() != "headless") _music.Play();
        _bridge.CommandIssued += OnCommand;
        _bridge.SimEventRaised += OnEvent;
        GetNode<WorldRenderer>("../Renderer").Vfx.Emitted += OnVfx;
    }

    public override void _ExitTree()
    {
        // Stop playback before the scene goes, so no AudioServer playback keeps a stream alive at exit.
        _music.Stop(); _voice.Stop(); _ui.Stop(); foreach (var p in _pool) p.Stop();
        _music.Stream = null; _voice.Stream = null; _ui.Stream = null; foreach (var p in _pool) p.Stream = null;
        foreach (var stream in _streams.Values) stream.Dispose();
        _streams.Clear();
    }

    private Faction FactionOf(EntityId id)
    {
        if (!_bridge.View.TryGet(id, out var e)) return Faction.Lumina;
        return e.Type.IsBuilding ? DefDatabase.Buildings[e.Type.Definition].Faction : DefDatabase.Units[e.Type.Definition].Faction;
    }
    private void Voice(string kind, EntityId id)
    {
        string suffix = FactionOf(id) == Faction.Lumina ? "lumina" : "verge";
        _voice.Stream = Stream($"voice_{kind}_{suffix}"); _voice.PitchScale = 0.94f + (id.Index % 5) * 0.03f; _voice.Play(); VoicesPlayed++;
    }

    public override void _Process(double delta)
    {
        var selected = _selection.Selected;
        ulong signature = 1469598103934665603;
        foreach (var id in selected) signature = unchecked((signature ^ (uint)id.Index) * 1099511628211);
        if (signature != _selectionSignature)
        {
            _selectionSignature = signature;
            ulong now = Time.GetTicksMsec();
            if (selected.Count > 0 && now - _lastVoice > 250 && _bridge.View.TryGet(selected[0], out var e) && !e.Type.IsBuilding) { _lastVoice = now; Voice("select", selected[0]); }
        }
    }
    private void OnCommand(Command c)
    {
        ulong now = Time.GetTicksMsec();
        if (now - _lastAck < 400 || c.Type is CommandType.Train or CommandType.Research or CommandType.Cancel or CommandType.Surrender or CommandType.Rally) return;
        _lastAck = now; Voice("ack", c.Entity);
    }
    private void OnVfx(Vector3 at, int kind)
    {
        // At most one sound of a kind per 60 ms and only near the camera, so 400-unit fights stay readable.
        ulong now = Time.GetTicksMsec();
        if ((uint)kind >= 4 || now - _lastKind[kind] < 60 || at.DistanceTo(_camera.Focus) > 70) return;
        _lastKind[kind] = now;
        PlayAt(kind switch { 1 => "sfx_shot", 2 => "sfx_melee", 3 => "sfx_explosion", _ => "sfx_hit" }, at, kind == 3 ? 2 : -4);
    }
    private void PlayAt(string name, Vector3 at, float volume)
    {
        var p = _pool[_next]; _next = (_next + 1) % _pool.Count;
        p.Stream = Stream(name); p.Position = at; p.VolumeDb = volume; p.PitchScale = (float)GD.RandRange(0.92, 1.08); p.Play(); SfxPlayed++;
    }
    private void OnEvent(SimEvent e)
    {
        switch (e.Kind)
        {
            case "UnitComplete": _ui.Stream = Stream("sfx_produced"); _ui.Play(); break;
            case "BuildComplete": _ui.Stream = Stream("sfx_build"); _ui.Play(); break;
            case "Death": PlayAt("sfx_death", _camera.Focus, -8); break; // own losses only
            case "MatchOver": _music.Stop(); _ui.Stream = Stream(e.Entity.Index == _bridge.LocalPlayer ? "sfx_victory" : "sfx_defeat"); _ui.Play(); break;
        }
    }
}

/// <summary>Launch flags that disable presentation extras during automated captures.</summary>
public static class MatchNet
{
    public static bool Benchmark => Net.MatchLaunch.Flag("render-benchmark") || Net.MatchLaunch.Flag("art-capture");
}
