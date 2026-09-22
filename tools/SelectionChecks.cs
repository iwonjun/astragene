using Godot;
using System;
using RtsGame.Bridge;
using RtsGame.View;
using RtsGame.Sim.Core;
using RtsGame.Sim.World;

public partial class SelectionChecks : Node
{
    public string RunChecks(Node match)
    {
        var bridge=match.GetNode<MatchBridge>("Bridge");
        var selection=match.GetNode<SelectionController>("Selection");
        bridge.SetPhysicsProcess(false);
        var tiles=new Tile[128*128];Array.Fill(tiles,new Tile(true,true,0));
        var spawns=new SpawnSpec[210];
        for(int i=0;i<spawns.Length;i++)spawns[i]=new SpawnSpec(i<205?0:1,1,false,new Fix2(Fix64.FromInt(45+i%15),Fix64.FromInt(45+i/15)));
        bridge.Initialize(new SimWorld(new MapData(new Grid(tiles)),spawns));
        var camera=new Camera3D {Projection=Camera3D.ProjectionType.Orthogonal,Size=60,Position=new Vector3(52,70,52),RotationDegrees=new Vector3(-90,0,0)};
        match.AddChild(camera);
        ulong before=bridge.World.Hash();
        selection.SelectScreenRect(new Rect2(-10000,-10000,20000,20000),camera,false,false);
        if(selection.SelectedCount!=200)return "Selection cap not enforced";
        selection._UnhandledInput(new InputEventKey {Pressed=true,PhysicalKeycode=Key.Key1,CtrlPressed=true});
        selection.SelectScreenRect(new Rect2(-500,-500,2,2),camera,false,false);
        if(selection.SelectedCount!=0)return "Empty click must clear";
        selection._UnhandledInput(new InputEventKey {Pressed=true,PhysicalKeycode=Key.Key1});
        if(selection.SelectedCount!=200)return "Control group restore failed";
        bool jumped=false;
        selection.CameraJumpRequested += _=>jumped=true;
        selection._UnhandledInput(new InputEventKey {Pressed=true,PhysicalKeycode=Key.Key1});
        if(!jumped)return "Repeated group did not request camera jump";
        Vector2 click=camera.UnprojectPosition(MatchBridge.ToView(bridge.World.Entities.Get(bridge.World.Entities.IdAt(0)).Transform.Position));
        selection.SelectScreenRect(new Rect2(click,Vector2.Zero),camera,false,false);
        if(selection.SelectedCount!=1)return "Single click selection failed";
        selection.SelectScreenRect(new Rect2(click,Vector2.Zero),camera,true,false);
        if(selection.SelectedCount!=0)return "Ctrl click toggle failed";
        selection.SelectScreenRect(new Rect2(click,Vector2.Zero),camera,false,true);
        if(selection.SelectedCount<2 || selection.SelectedCount>200)return "Same type selection failed";
        if(before!=bridge.World.Hash())return "Local selection changed simulation";
        return "";
    }
}
