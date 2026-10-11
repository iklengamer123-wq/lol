using System;
using Godot;

namespace CustomRhythia.UI;

// Static heart marker for the favorite maps. Simple filled heart, no animation.
public partial class HeartIcon : Control
{
    public HeartIcon()
    {
        MouseFilter = MouseFilterEnum.Ignore;
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
