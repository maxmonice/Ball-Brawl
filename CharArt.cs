namespace BallBrawl;

// Render-only art for the newer characters, status effects, gravity/poison zones and meteors. Reads Sim state, never changes it.
static class CharArt
{
    static readonly SolidBrush br = new(Color.White); static readonly Pen pen = new(Color.White) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round };
    static readonly PointF[] tri = new PointF[3];
    static int A(float v) => Math.Clamp((int)v, 0, 255);
    static void Dot(Graphics g, float x, float y, float r, Color c) { br.Color = c; g.FillEllipse(br, x - r, y - r, 2 * r, 2 * r); }
    static void Spike(Graphics g, PointF p, float ang, float r0, float len, float w, Color c)
    {
        float cs = MathF.Cos(ang), sn = MathF.Sin(ang);
        tri[0] = new(p.X + cs * r0 - sn * w, p.Y + sn * r0 + cs * w); tri[1] = new(p.X + cs * r0 + sn * w, p.Y + sn * r0 - cs * w); tri[2] = new(p.X + cs * (r0 + len), p.Y + sn * (r0 + len));
        br.Color = c; g.FillPolygon(br, tri);
    }

    public static string Tags(Ball b)
    {
        string s = "";
        if (b.BurnT > 0) s += "BURN "; if (b.Poison > 0) s += $"PSN{b.Poison} "; if (b.SlowStacks > 0) s += $"SLOW{Math.Min(30, b.SlowStacks * 5)}% ";
        if (b.FreezeT > 0) s += "FROZEN "; if (b.ShieldT > 0) s += "SHIELD "; if (b.BoostT > 0) s += "BOOST "; if (b.TailStacks > 0) s += $"WIND+{Math.Min(30, b.TailStacks * 5)}% ";
        if (b.D.K == Kind.Solar) s += $"E{b.Energy:0}";
        return s;
    }

    public static void Body(Graphics g, Ball b, float t)
    {
        float r = b.R, x = b.P.X, y = b.P.Y, pi = MathF.PI;
        switch (b.D.K)
        {
            case Kind.Inferno:
                Dot(g, x, y, r + 10, Color.FromArgb(40, 255, 120, 20));
                for (int i = 0; i < 6; i++) { float f = MathF.Sin(t * 11 + i * 2.1f), a = -pi / 2 + (i - 2.5f) * .42f + f * .12f; Spike(g, b.P, a, r * .8f, 18 + 9 * f * f, 6, Color.FromArgb(190, 255, 110, 20)); Spike(g, b.P, a, r * .8f, 11 + 6 * f * f, 3, Color.FromArgb(220, 255, 220, 80)); }
                break;
            case Kind.Frost:
                Dot(g, x, y, r + 9, Color.FromArgb(28, 190, 230, 255));
                for (int i = 0; i < 8; i++) Spike(g, b.P, i * pi / 4 + t * .15f, r * .85f, 13, 5, Color.FromArgb(235, 235, 248, 255));
                break;
            case Kind.Void:
                for (int k = 0; k < 3; k++) { float rr = r + 7 + k * 5; pen.Color = Color.FromArgb(150, 160, 70, 220); pen.Width = 2; g.DrawArc(pen, x - rr, y - rr, 2 * rr, 2 * rr, t * 140 + k * 120, 85); }
                for (int i = 0; i < 4; i++) { float a = t * 3 + i * pi / 2; Dot(g, x + MathF.Cos(a) * (r + 9), y + MathF.Sin(a) * (r + 9), 2.5f, Color.FromArgb(220, 190, 110, 255)); }
                break;
            case Kind.Venom:
                for (int i = 0; i < 4; i++) { float f = (t * .7f + i * .25f) % 1; Dot(g, x + MathF.Sin(i * 2.4f + t) * r * .5f, y + r * .6f - f * r * 1.2f, 2 + 2 * (1 - f), Color.FromArgb(A(200 * (1 - f)), 190, 255, 120)); }
                { float f = (t * .6f) % 1; Dot(g, x + r * .35f, y + r + f * 12, 3.5f * (1 - f * .5f), Color.FromArgb(A(220 * (1 - f)), 110, 220, 40)); }   // dripping slime
                break;
            case Kind.Solar:
                {
                    float en = b.Energy / 100f; Dot(g, x, y, r + 8 + 6 * en, Color.FromArgb(A(30 + 90 * en), 255, 210, 60));
                    pen.Color = Color.FromArgb(230, 255, 230, 120); pen.Width = 2.5f; float r1 = r + 6, r2 = r + 12;
                    g.DrawArc(pen, x - r1, y - r1, 2 * r1, 2 * r1, t * 90, 110); g.DrawArc(pen, x - r1, y - r1, 2 * r1, 2 * r1, t * 90 + 180, 110); g.DrawArc(pen, x - r2, y - r2, 2 * r2, 2 * r2, -t * 60, 80);
                    pen.Width = 1.5f; for (int i = 0; i < 8; i++) { float a = i * pi / 4 + t; g.DrawLine(pen, x + MathF.Cos(a) * (r + 15), y + MathF.Sin(a) * (r + 15), x + MathF.Cos(a) * (r + 19 + 8 * en), y + MathF.Sin(a) * (r + 19 + 8 * en)); }
                }
                break;
            case Kind.Mecha:
                {
                    bool od = b.UltT > 0; float s1 = r * .5f, sh = od ? MathF.Sin(t * 25) * 2 : 0; pen.Color = Color.FromArgb(200, 70, 80, 95); pen.Width = 2;
                    for (int k = -1; k <= 1; k += 2) { float d = k * s1, hw = MathF.Sqrt(r * r - d * d); g.DrawLine(pen, x - hw + sh, y + d, x + hw + sh, y + d); }   // armour seams
                    g.DrawLine(pen, x, y - r, x, y + r); pen.Color = Color.FromArgb(120, 255, 255, 255); pen.Width = 1; g.DrawArc(pen, x - r + 3, y - r + 3, 2 * r - 6, 2 * r - 6, 200, 60);
                    float sx = x + b.V.X * r * .35f, sy = y + b.V.Y * r * .35f; var core = od ? Color.FromArgb(255, 255, 90, 40) : Color.FromArgb(255, 60, 230, 255);
                    Dot(g, sx, sy, 7, Color.FromArgb(70, core)); Dot(g, sx, sy, 4, core);   // glowing sensor follows heading
                }
                break;
            case Kind.Cosmic:
                for (int i = 0; i < 5; i++) { float a = t * 2.2f + i * 1.256f, rr = r + 8 + 3 * MathF.Sin(t * 3 + i); Dot(g, x + MathF.Cos(a) * rr, y + MathF.Sin(a) * rr, i % 2 == 0 ? 3 : 2, i % 2 == 0 ? Color.FromArgb(230, 255, 240, 255) : Color.FromArgb(230, 255, 150, 220)); }
                pen.Color = Color.FromArgb(90, 255, 140, 230); pen.Width = 1.5f; g.DrawEllipse(pen, x - r - 6, y - r - 6, 2 * r + 12, 2 * r + 12);
                break;
            case Kind.Tempest:
                {
                    bool cy = b.UltT > 0;
                    for (int k = 0; k < 3; k++) { float rr = r + 6 + k * 5 + (cy ? 6 : 0); pen.Color = Color.FromArgb(cy ? 230 : 180, k == 1 ? 120 : 255, 235, 255); pen.Width = 2.5f; g.DrawArc(pen, x - rr, y - rr, 2 * rr, 2 * rr, t * (cy ? 420 : 200) + k * 120, 110); }
                }
                break;
        }
    }

    public static void Status(Graphics g, Ball b, float t)
    {
        float r = b.R, x = b.P.X, y = b.P.Y;
        if (b.BurnT > 0) for (int i = 0; i < 3; i++) { float f = MathF.Sin(t * 13 + i * 2); Spike(g, b.P, -MathF.PI / 2 + (i - 1) * .5f, r * .8f, 12 + 5 * f * f, 4, Color.FromArgb(170, 255, 120, 20)); }
        if (b.Poison > 0) { Dot(g, x, y, r, Color.FromArgb(25 + b.Poison * 12, 90, 220, 40)); Dot(g, x + MathF.Sin(t * 5) * r * .4f, y - (t * 30) % r, 2.5f, Color.FromArgb(200, 170, 255, 110)); }
        if (b.SlowStacks > 0) { pen.Color = Color.FromArgb(A(50 + b.SlowStacks * 30), 150, 210, 255); pen.Width = 2 + b.SlowStacks * .5f; g.DrawEllipse(pen, x - r - 2, y - r - 2, 2 * r + 4, 2 * r + 4); }
        if (b.ShieldT > 0) { pen.Color = Color.FromArgb(200, 90, 230, 255); pen.Width = 3; for (int i = 0; i < 6; i++) g.DrawArc(pen, x - r - 7, y - r - 7, 2 * r + 14, 2 * r + 14, i * 60 + t * 40, 44); }
        if (b.FreezeT > 0)
        {
            var rc = new RectangleF(x - r - 6, y - r - 6, 2 * r + 12, 2 * r + 12); br.Color = Color.FromArgb(120, 190, 235, 255); g.FillRectangle(br, rc);
            pen.Color = Color.FromArgb(220, 255, 255, 255); pen.Width = 2; g.DrawRectangle(pen, rc.X, rc.Y, rc.Width, rc.Height);
            g.DrawLine(pen, x - r * .6f, y - r * .6f, x + r * .2f, y + r * .5f); g.DrawLine(pen, x + r * .5f, y - r * .7f, x - r * .1f, y);
        }
    }

    public static void World(Graphics g, Sim s, float t)
    {
        foreach (var z in s.Zones)
        {
            float life = Math.Min(1, (z.Life - z.Age) / .5f), R = z.R * Math.Min(1, z.Age / .3f), cx = z.P.X, cy = z.P.Y;
            if (R < 4f) continue;   // zone grows from radius 0; GDI+ rejects zero-size arcs/ellipses
            if (z.K == Kind.Void)
            {
                Dot(g, cx, cy, R, Color.FromArgb(A(70 * life), 25, 5, 45)); Dot(g, cx, cy, R * .35f, Color.FromArgb(A(160 * life), 10, 0, 25));
                for (int q = 0; q < 3; q++) { float rr = R * (.55f + q * .22f); pen.Color = Color.FromArgb(A(170 * life), 170, 90, 240); pen.Width = 2; g.DrawArc(pen, cx - rr, cy - rr, 2 * rr, 2 * rr, t * (120 + q * 40) + q * 100, 100); }   // distorted rings
                for (int i = 0; i < 14; i++) { float f = 1 - (t * .9f + i / 14f) % 1, a = i * .9f + t * 2; Dot(g, cx + MathF.Cos(a) * R * f, cy + MathF.Sin(a) * R * f, 2.5f, Color.FromArgb(A(220 * life), 200, 140, 255)); }   // inward particles
            }
            else
            {
                Dot(g, cx, cy, R, Color.FromArgb(A((55 + 15 * MathF.Sin(t * 3)) * life), 110, 220, 40));
                pen.Color = Color.FromArgb(A(160 * life), 90, 200, 30); pen.Width = 2.5f; g.DrawEllipse(pen, cx - R, cy - R, 2 * R, 2 * R);
                for (int i = 0; i < 10; i++) { float f = (t * .5f + i * .1f) % 1, a = i * 2.4f, d = R * .8f * MathF.Sqrt(f); Dot(g, cx + MathF.Cos(a) * d, cy + MathF.Sin(a) * d - f * 20, 3 + 3 * f, Color.FromArgb(A(200 * (1 - f) * life), 170, 255, 110)); }
            }
        }
        foreach (var m in s.Meteors)
        {
            if (!m.Started) continue;
            float k = Math.Clamp((m.Age - m.Start) / Balance.MeteorFall, 0, 1), pulse = .6f + .4f * MathF.Sin(t * 20), rr = Balance.MeteorRadius;
            pen.Color = Color.FromArgb(A(220 * pulse), 255, 70, 90); pen.Width = 2.5f; g.DrawEllipse(pen, m.Pos.X - rr, m.Pos.Y - rr, 2 * rr, 2 * rr);   // warning indicator
            Dot(g, m.Pos.X, m.Pos.Y, rr * k, Color.FromArgb(50, 255, 70, 90));
            float hx = m.Pos.X - 150 * (1 - k), hy = m.Pos.Y - 300 * (1 - k); pen.Color = Color.FromArgb(200, 255, 150, 60); pen.Width = 6; g.DrawLine(pen, hx - 45, hy - 90, hx, hy); Dot(g, hx, hy, 8, Color.FromArgb(255, 255, 240, 200));
        }
    }
}
