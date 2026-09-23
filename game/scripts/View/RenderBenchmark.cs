using Godot;
using System;
using System.Collections.Generic;
using RtsGame.Bridge;
using RtsGame.Sim.Core;
using RtsGame.Sim.World;

namespace RtsGame.View;
// Opt-in real-renderer fixture. Simulation and combat run normally; no hidden state is read.
public partial class RenderBenchmark : Node
{
    private bool _enabled,_saving,_artCapture;
    private int _warmFrames,_fullFrames;
    private double _fullMs;
    private int _frames,_drawMax,_visibleMax,_shot;
    private double _seconds,_warmSeconds;
    private readonly List<double> _frameTimes=new();
    private MatchBridge _bridge=null!;
    private RtsCamera _camera=null!;
    public override void _Ready()
    {
        _artCapture=Array.IndexOf(OS.GetCmdlineUserArgs(),"--art-capture")>=0;
        _enabled=_artCapture || Array.IndexOf(OS.GetCmdlineUserArgs(),"--render-benchmark")>=0;
        if(!_enabled){SetProcess(false);return;}
        System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath("res://docs"));
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);Engine.MaxFps=0;
        _bridge=GetNode<MatchBridge>("../Bridge");_camera=GetNode<RtsCamera>("../Camera");_camera.ControlsEnabled=false;
        if(_artCapture)
        {
            var gallery=new List<SpawnSpec>();
            for(int side=0;side<2;side++)for(int role=0;role<5;role++)
                gallery.Add(new SpawnSpec(side,side*5+role,false,new Fix2(Fix64.FromInt(46+role*3),Fix64.FromInt(44+side*5))));
            gallery.Add(new SpawnSpec(0,0,true,new Fix2(Fix64.FromInt(43),Fix64.FromInt(46))));
            gallery.Add(new SpawnSpec(1,6,true,new Fix2(Fix64.FromInt(62),Fix64.FromInt(46))));
            _bridge.Initialize(new SimWorld(_bridge.World.Map,gallery.ToArray()));_bridge.SetPhysicsProcess(false);
            _camera.Altitude=20;_camera.Jump(new Vector3(52,0,46));GetWindow().Size=new Vector2I(1600,900);return;
        }
        var spawns=new List<SpawnSpec>();
        // Two 200-unit opposing armies: the same normal unit stats and combat code as a game.
        for(int side=0;side<2;side++)for(int i=0;i<200;i++)
            spawns.Add(new SpawnSpec(side,side*5+i%5,false,new Fix2(Fix64.FromRatio(440+i%40*10,10),Fix64.FromRatio(580+side*50+i/40*10,10))));
        _bridge.Initialize(new SimWorld(_bridge.World.Map,spawns.ToArray()));_bridge.SetPhysicsProcess(false);
        _camera.Altitude=48;_camera.Jump(new Vector3(64,0,64));
        GetWindow().Size=new Vector2I(1600,900);
    }
    public override void _Process(double delta)
    {
        if(!_enabled || _saving)return;
        _warmFrames++;_warmSeconds+=delta;if(_warmFrames<120 || _warmSeconds<1)return;
        if(_artCapture)
        {
            if(_shot==0){_shot=2;SaveShot();_warmFrames=0;_warmSeconds=0;return;}
            if(_shot==2){_camera.Altitude=30;_camera.Jump(new Vector3(20,0,20));_bridge.ResetMatch();_bridge.SetPhysicsProcess(false);_shot=3;_warmFrames=0;_warmSeconds=0;return;}
            if(_shot==3){_shot=4;SaveShot();return;}
            GetTree().Quit();return;
        }
        _bridge.SetPhysicsProcess(true);
        _frames++;_seconds+=delta;
        if(GetNode<WorldRenderer>("../Renderer").VisibleEntities==400){_fullFrames++;_fullMs+=delta*1000;}
        if(_frames>1){_frameTimes.Add(delta*1000);_drawMax=Math.Max(_drawMax,(int)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame));_visibleMax=Math.Max(_visibleMax,GetNode<WorldRenderer>("../Renderer").VisibleEntities);}
        if(_shot<3 && _seconds>1+_shot*1.5){_shot++;SaveShot();}
        if(_seconds>6 && !_saving)
        {
            _frameTimes.Sort();double sum=0;foreach(double t in _frameTimes)sum+=t;
            var report=new {renderer=RenderingServer.GetCurrentRenderingMethod(),adapter=RenderingServer.GetVideoAdapterName(),resolution="1600x900",frames=_frameTimes.Count,averageMs=sum/_frameTimes.Count,p95Ms=_frameTimes[(int)(_frameTimes.Count*0.95)],maximumDrawCalls=_drawMax,maximumVisibleEntities=_visibleMax,full400Frames=_fullFrames,full400AverageMs=_fullFrames>0?_fullMs/_fullFrames:0,remainingTick=_bridge.World.TickNumber};
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("res://docs/render-benchmark.json"),System.Text.Json.JsonSerializer.Serialize(report,new System.Text.Json.JsonSerializerOptions {WriteIndented=true}));
            GD.Print("RENDER BENCHMARK "+System.Text.Json.JsonSerializer.Serialize(report));GetTree().Quit();
        }
    }
    private async void SaveShot()
    {
        _saving=true;await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://docs/phase9-{(_shot==4?3:_shot)}.png"));_saving=false;
    }
}
