using System;
using System.Collections.Generic;
using Godot;

namespace RtsGame.UI;

/// <summary>Per-minute command counts drawn as one line per player on a grid with minute and APM labels.</summary>
public partial class ApmGraph : Control
{
    private readonly List<(int[] Values, Color Color, string Name)> _series = new();
    public int SeriesCount => _series.Count;
    public void SetSeries(IEnumerable<(int[] Values, Color Color, string Name)> series) { _series.Clear(); _series.AddRange(series); QueueRedraw(); }
    public override void _Draw()
    {
        var font = GetThemeDefaultFont(); int fontSize = 13;
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.02f, 0.05f, 0.09f, 0.7f));
        int minutes = 1, top = 10;
        foreach (var s in _series) { minutes = Math.Max(minutes, s.Values.Length); foreach (int v in s.Values) top = Math.Max(top, v); }
        top = (top + 9) / 10 * 10;
        const float left = 44, bottom = 24;
        var plot = new Rect2(left, 8, Size.X - left - 10, Size.Y - bottom - 8);
        for (int g = 0; g <= 4; g++)
        {
            float y = plot.End.Y - plot.Size.Y * g / 4f;
            DrawLine(new Vector2(plot.Position.X, y), new Vector2(plot.End.X, y), new Color(0.3f, 0.6f, 0.8f, 0.18f));
            DrawString(font, new Vector2(4, y + 4), (top * g / 4).ToString(), HorizontalAlignment.Left, -1, fontSize, new Color(0.6f, 0.8f, 0.9f));
        }
        int labelEvery = Math.Max(1, minutes / 10);
        for (int m = 0; m < minutes; m += labelEvery)
        {
            float x = minutes == 1 ? plot.Position.X : plot.Position.X + plot.Size.X * m / (minutes - 1);
            DrawString(font, new Vector2(x - 8, Size.Y - 6), $"{m + 1}분", HorizontalAlignment.Left, -1, fontSize, new Color(0.6f, 0.8f, 0.9f));
        }
        float legend = plot.Position.X + 6;
        foreach (var s in _series)
        {
            if (s.Values.Length > 0)
            {
                var points = new Vector2[Math.Max(2, s.Values.Length)];
                for (int i = 0; i < points.Length; i++)
                {
                    int v = s.Values[Math.Min(i, s.Values.Length - 1)];
                    float x = plot.Position.X + (points.Length == 1 ? 0 : plot.Size.X * i / (points.Length - 1));
                    points[i] = new Vector2(x, plot.End.Y - plot.Size.Y * v / top);
                }
                DrawPolyline(points, s.Color, 2.5f, true);
                foreach (var p in points) DrawCircle(p, 3, s.Color);
            }
            DrawString(font, new Vector2(legend, plot.Position.Y + 14), "■ " + s.Name, HorizontalAlignment.Left, -1, fontSize + 1, s.Color);
            legend += 150;
        }
    }
}
