using Godot;
namespace RtsGame.View;
public partial class RtsCamera : Camera3D
{
    public Vector3 Focus=new(20,0,20);
    public float Altitude=30;
    public float ScrollSpeed=24;
    public bool ControlsEnabled=true;
    public bool EdgeScroll=true;
    public override void _Ready()
    {
        RotationDegrees=new Vector3(-50,0,0);Current=true;Fov=45;Far=250;
        RtsGame.UI.GameSettings.EnsureLoaded();ScrollSpeed=24*RtsGame.UI.GameSettings.ScrollSpeed;EdgeScroll=RtsGame.UI.GameSettings.EdgeScroll;
        GetNode<SelectionController>("../Selection").CameraJumpRequested+=Jump;
        UpdatePose();
    }
    public void Jump(Vector3 point){Focus=new Vector3(Mathf.Clamp(point.X,0,128),0,Mathf.Clamp(point.Z,0,128));UpdatePose();}
    public void UpdatePose(){Position=Focus+new Vector3(0,Altitude,Altitude/Mathf.Tan(Mathf.DegToRad(50)));}
    public override void _Process(double delta)
    {
        if(!ControlsEnabled)return;
        Vector2 direction=Input.GetVector("camera_left","camera_right","camera_up","camera_down");
        var mouse=GetViewport().GetMousePosition();var size=GetViewport().GetVisibleRect().Size;
        if(EdgeScroll && DisplayServer.WindowIsFocused() && mouse.X>=0 && mouse.Y>=0 && mouse.X<size.X && mouse.Y<size.Y){if(mouse.X<8)direction.X-=1;if(mouse.X>size.X-8)direction.X+=1;if(mouse.Y<8)direction.Y-=1;if(mouse.Y>size.Y-8)direction.Y+=1;}
        Jump(Focus+new Vector3(direction.X,0,direction.Y)*ScrollSpeed*(float)delta);
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if(!ControlsEnabled)return;
        if(input is InputEventMouseButton m && m.Pressed){if(m.ButtonIndex==MouseButton.WheelUp)Altitude=Mathf.Max(20,Altitude-3);if(m.ButtonIndex==MouseButton.WheelDown)Altitude=Mathf.Min(60,Altitude+3);UpdatePose();}
        if(input.IsActionPressed("recent_event"))Jump(GetNode<WorldRenderer>("../Renderer").LastEventPosition);
    }
}
