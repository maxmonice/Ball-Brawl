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
    static readonly LinearGradientBrush BladeB = new(new RectangleF(34, -4, 42, 8), Color.FromArgb(245, 250, 255), Color.FromArgb(140, 160, 190), 90f);
    static readonly LinearGradientBrush GoldB = new(new RectangleF(11, -10, 24, 20), Color.FromArgb(255, 225, 120), Color.FromArgb(170, 120, 30), 90f);
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
}
