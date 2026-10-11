using System;
using Godot;

namespace CustomRhythia.UI;

// Favorite marker. Draws a heart with the parametric curve
//   x(t) = 16 sin^3(t)
//   y(t) = 13 cos t - 5 cos 2t - 2 cos 3t - cos 4t
// so the shape is symmetric and independent of any font.
// The rainbow shimmer uses HSV with saturation 1 and value 1.
public partial class HeartIcon : Control
{
    private float _phase;

    public HeartIcon()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Ready()
    {
        _phase = GD.Randf() * Mathf.Tau;
    }

    public override void _Process(double delta)
    {
        float t = Time.GetTicksMsec() / 1000f;
        float hue = Mathf.PosMod(t * 0.55f + _phase / Mathf.Tau, 1.0f);

        Color c = Color.FromHsv(hue, 1.0f, 1.0f);
        c.A = 1.0f;
        Modulate = c;
    }

    public override void _Draw()
    {
        Vector2 sz = Size;
        if (sz.X <= 1f || sz.Y <= 1f) return;

        float cx = sz.X * 0.5f;
        float cy = sz.Y * 0.5f;
        float hw = sz.X * 0.5f * 0.92f;
        float hh = sz.Y * 0.5f * 0.92f;

        const int N = 128;
        var points = new Vector2[N];

        for (int i = 0; i < N; i++)
        {
            float t = (float)(i * Math.PI * 2.0 / N);

            float sinT = Mathf.Sin(t);
            float xMath = 16f * sinT * sinT * sinT;
            float yMath = 13f * Mathf.Cos(t)
                        - 5f * Mathf.Cos(2f * t)
                        - 2f * Mathf.Cos(3f * t)
                        - Mathf.Cos(4f * t);

            float xn = xMath / 16f;
            float yn = -(yMath + 6f) / 11f;

            points[i] = new Vector2(cx + xn * hw, cy + yn * hh);
        }

        DrawColoredPolygon(points, Colors.White);
    }
}
