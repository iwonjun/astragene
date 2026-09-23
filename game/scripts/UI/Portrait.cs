using Godot;
namespace RtsGame.UI;
public partial class Portrait : Control
{
    public int Role;
    public Color Accent=new(0.1f,0.85f,1);
    public bool Building;
    public override void _Draw()
    {
        DrawStyleBox(new StyleBoxFlat {BgColor=new Color(0.03f,0.1f,0.16f),BorderColor=Accent,BorderWidthLeft=2,BorderWidthTop=1,BorderWidthRight=1,BorderWidthBottom=3,CornerRadiusTopLeft=12,CornerRadiusBottomRight=12},new Rect2(Vector2.Zero,Size));
        var c=Size/2;DrawCircle(c,47,new Color(Accent.R,Accent.G,Accent.B,0.07f));
        if(Building){DrawRect(new Rect2(c-new Vector2(34,15),new Vector2(68,55)),Accent.Darkened(0.3f));DrawCircle(c-new Vector2(0,20),17,Accent);}
        else if(Role%5==4){DrawColoredPolygon(new[]{c+new Vector2(0,-38),c+new Vector2(45,0),c+new Vector2(0,38),c+new Vector2(-45,0)},Accent);}
        else{DrawCircle(c-new Vector2(0,24),Role%5==0?24:17,Accent.Lightened(0.25f));DrawRect(new Rect2(c-new Vector2(25,3),new Vector2(50,38)),Accent.Darkened(0.3f));DrawRect(new Rect2(c-new Vector2(17,28),new Vector2(34,8)),Colors.White);if(Role%5==2)DrawLine(c+new Vector2(22,22),c+new Vector2(44,-40),Accent,8);}
    }
}
