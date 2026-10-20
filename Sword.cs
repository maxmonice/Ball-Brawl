using System.Drawing.Drawing2D;
namespace BallBrawl;

// The Anime Sword Ball's weapon: a separate component. Shapes are built once in local units
// (+x = blade direction, 1 unit = 1px at ball radius 28) and drawn with a rotated/scaled transform.
static class SwordArt
{
    public static float Reach(float r) => 76 * r / 28f;   // distance from ball centre to blade tip
    static GraphicsPath Poly(params float[] v) { var p = new GraphicsPath(); var pts = new PointF[v.Length / 2]; for (int i = 0; i < pts.Length; i++) pts[i] = new(v[2 * i], v[2 * i + 1]); p.AddPolygon(pts); return p; }
    static readonly GraphicsPath Blade = Poly(34, -3.8f, 68, -3.2f, 76, 0, 68, 3.2f, 34, 3.8f);
    static readonly GraphicsPath Shade = Poly(34, 0, 76, 0, 68, 3.2f, 34, 3.8f);
    static readonly GraphicsPath Guard = Poly(29.5f, -9, 34.5f, -10.5f, 34.5f, 10.5f, 29.5f, 9);
    static readonly GraphicsPath Grip = Rect(18, -2.4f, 12, 4.8f), Pommel = Ell(11.4f, -3.6f, 7.2f), Jewel = Ell(30.4f, -1.8f, 3.6f);
    static GraphicsPath Rect(float x, float y, float w, float h) { var p = new GraphicsPath(); p.AddRectangle(new RectangleF(x, y, w, h)); return p; }
    static GraphicsPath Ell(float x, float y, float d) { var p = new GraphicsPath(); p.AddEllipse(x, y, d, d); return p; }
    static readonly GraphicsPath[] Parts = { Grip, Pommel, Guard, Blade };
    static readonly SolidBrush BladeB = new(Color.FromArgb(226, 234, 248));
    static readonly SolidBrush GoldB = new(Color.FromArgb(244, 202, 74));
    static readonly SolidBrush GripB = new(Color.FromArgb(55, 40, 90)), ShadeB = new(Color.FromArgb(110, 90, 110, 150)), JewelB = new(Color.Crimson), GhostB = new(Color.White);
    static readonly Pen Edge = new(Color.FromArgb(40, 35, 60), 1.4f) { LineJoin = LineJoin.Round }, Shine = new(Color.White, 1.1f), Wrap = new(Color.FromArgb(150, 130, 200), 1f),
        Fuller = new(Color.FromArgb(120, 150, 190), 1f), OutW = new(Color.White, 3.5f) { LineJoin = LineJoin.Round }, OutB = new(Color.Black, 3.5f) { LineJoin = LineJoin.Round };

    // mode 0 = normal colours; 1 / 2 = black or white silhouette frames (outlined in the opposite colour so the hilt stays readable over the ball)
    public static void Draw(Graphics g, PointF c, float s, float ang, int mode)
    {
        var st = g.Save(); g.TranslateTransform(c.X, c.Y); g.RotateTransform(ang * 57.2958f); g.ScaleTransform(s, s);
        if (mode == 0)
        {
            g.FillPath(GripB, Grip); for (int x = 20; x < 30; x += 3) g.DrawLine(Wrap, x, -2.4f, x + 1.5f, 2.4f);
            g.FillPath(GoldB, Pommel); g.DrawPath(Edge, Pommel);
            g.FillPath(BladeB, Blade); g.FillPath(ShadeB, Shade); g.DrawLine(Fuller, 38, 0, 66, 0); g.DrawLine(Shine, 35, -3.4f, 69, -3f); g.DrawPath(Edge, Blade);
            g.FillPath(GoldB, Guard); g.DrawPath(Edge, Guard); g.FillPath(JewelB, Jewel);
            g.DrawPath(Edge, Grip);
        }
        else { var fill = mode == 1 ? Brushes.Black : Brushes.White; var outl = mode == 1 ? OutW : OutB; foreach (var p in Parts) { g.DrawPath(outl, p); g.FillPath(fill, p); } }
        g.Restore(st);
    }
    public static void DrawGhost(Graphics g, PointF c, float s, float ang, int alpha)   // afterimage of the blade during a swing
    {
        var st = g.Save(); g.TranslateTransform(c.X, c.Y); g.RotateTransform(ang * 57.2958f); g.ScaleTransform(s, s);
        GhostB.Color = Color.FromArgb(alpha, 225, 235, 255); g.FillPath(GhostB, Blade); g.Restore(st);
    }

    // ---- pose helpers (single source for the sword's idle sway and swing; used by DrawBall and ImpactFx) ----
    public static float Rest(Ball b, float t) => -0.9f + 0.07f * MathF.Sin(t * 3 + b.D.HP);
    public static float SlashAngle(float rest, float slashT)   // wind-up, then a fast 360 swing
    { float p = slashT / 0.25f; return rest + (p < .12f ? -0.6f * p / .12f : -0.6f + (MathF.PI * 2 + 0.6f) * (1 - MathF.Pow(1 - (p - .12f) / .88f, 2))); }
    public static (float, float) Pose(Ball b, float t, bool cine)   // (angle, scale); during the ultimate freeze the sword is cocked and enlarged
    {
        float rest = Rest(b, t), s = b.R / 28f;
        if (b.SlashT >= 0 && cine) return (rest - 0.9f + 0.03f * MathF.Sin(t * 90), s * 1.3f);
        return (b.SlashT >= 0 ? SlashAngle(rest, b.SlashT) : rest, s);
    }
    static readonly Pen EdgeP = new(Color.Crimson) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
    static readonly PointF[] EdgeLine = { new(34, -3.8f), new(68, -3.2f), new(76, 0) };
    static readonly PointF[][] Segs = { new PointF[] { new(34, -3.8f), new(48, -3.5f), new(48, 3.5f), new(34, 3.8f) }, new PointF[] { new(49, -3.5f), new(63, -3.3f), new(63, 3.3f), new(49, 3.5f) }, new PointF[] { new(64, -3.3f), new(68, -3.2f), new(76, 0), new(68, 3.2f), new(64, 3.3f) } };
    static readonly SolidBrush SegB = new(Color.White);
    public static void DrawEdge(Graphics g, PointF c, float s, float ang, Color col)   // crimson highlight along the cutting edge
    {
        var st = g.Save(); g.TranslateTransform(c.X, c.Y); g.RotateTransform(ang * 57.2958f); g.ScaleTransform(s, s);
        EdgeP.Color = Color.FromArgb(70, col); EdgeP.Width = 6; g.DrawLines(EdgeP, EdgeLine); EdgeP.Color = col; EdgeP.Width = 1.8f; g.DrawLines(EdgeP, EdgeLine); g.Restore(st);
    }
    public static void DrawShards(Graphics g, PointF c, float s, float ang, Color col, float frag)   // blade afterimage that breaks into three pieces as it fades
    {
        var st = g.Save(); g.TranslateTransform(c.X, c.Y); g.RotateTransform(ang * 57.2958f); g.ScaleTransform(s, s); SegB.Color = col;
        for (int i = 0; i < 3; i++) { var q = g.Save(); g.TranslateTransform(frag * 10 * i, frag * (i - 1) * 6); g.FillPolygon(SegB, Segs[i]); g.Restore(q); }
        g.Restore(st);
    }
}