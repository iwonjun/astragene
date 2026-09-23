using Godot;
using RtsGame.Bridge;
using RtsGame.UI;
using RtsGame.View;
using RtsGame.Sim.Core;
using RtsGame.Sim.Entities;
using RtsGame.Sim.World;
public partial class HudChecks : Node
{
    public string RunChecks(Node match)
    {
        var bridge=match.GetNode<MatchBridge>("Bridge");bridge.SetPhysicsProcess(false);
        var selection=match.GetNode<SelectionController>("Selection");var hud=match.GetNode<MatchHud>("HUD");
        var core=bridge.View.IdAt(0);selection.SelectOnly(core);hud.Refresh();
        var before=bridge.World.Hash();hud.ActivateSlot(0);if(before!=bridge.World.Hash())return "HUD mutated simulation before command tick";
        bridge._PhysicsProcess(0.05);
        if(bridge.View.ProductionAt(core,0).Definition!=0 || bridge.View.Resources.Ore!=395)return "Train card did not queue worker and charge cost";
        hud.Refresh();hud.GetNode<Button>("Root/SelectionPanel/Queue0").EmitSignal(Button.SignalName.Pressed);bridge._PhysicsProcess(0.05);
        if(bridge.View.Resources.Ore!=450 || bridge.View.ProductionAt(core,0).Definition!=-1)return "Queue cancellation failed";
        var worker=bridge.View.IdAt(1);selection.SelectOnly(worker);hud.Refresh();
        int rx=-1,rz=-1;
        for(int z=0;z<45;z++)for(int x=0;x<45;x++)if(bridge.World.Map.Grid[x,z].ResourceNodeId==0){rx=x;rz=z;}
        bridge.IssueContext(worker,new Vector3(rx+0.5f,3,rz+0.5f),false);bridge._PhysicsProcess(0.05);
        if(bridge.View.Get(worker).Cargo.ResourceNode!=0)return "Context gathering failed";
        hud.ActivateSlot(8);hud.ActivateSlot(1);if(hud.BuildingPreview!=1)return "Construction submenu failed";
        hud._UnhandledInput(new InputEventKey{Pressed=true,PhysicalKeycode=Key.Escape});if(hud.BuildingPreview!=-1)return "Placement cancel failed";
        hud._UnhandledInput(new InputEventKey{Pressed=true,PhysicalKeycode=Key.Escape});if(!bridge.IsPaused)return "Pause failed";
        hud._UnhandledInput(new InputEventKey{Pressed=true,PhysicalKeycode=Key.Escape});if(bridge.IsPaused)return "Resume failed";
        hud.ReceiveChat(0,"local message",false);if(!hud.GetNode<Label>("Root/ChatLog").Text.Contains("local message"))return "Chat log failed";
        var keys=new Hotkeys();if(keys.Assign(0,Key.W))return "Camera conflict allowed";if(!keys.Assign(0,Key.P)||keys[0]!=Key.P)return "Hotkey reassignment failed";
        bridge.Initialize(new SimWorld(bridge.World.Map,new[]{new SpawnSpec(0,0,true,new Fix2(Fix64.FromInt(20),Fix64.FromInt(20)))},initialOre:0));
        selection.SelectOnly(bridge.View.IdAt(0));hud.Refresh();hud.ActivateSlot(0);if(!hud.NoticeText.Contains("자원"))return "Insufficient resource warning missing";
        return "";
    }
}
