using System.Drawing.Drawing2D;
namespace BallBrawl;

// Events the gameplay sim raises; the renderer turns them into effects. Gameplay never reads VFX state.
readonly record struct GameEvent(int T, Kind K, PointF P, PointF N, bool Ult, float Scale); // T: 0 = hit, 1 = lightning ultimate start

struct Particle { public float X, Y, VX, VY, Age, Life, Size, Drag, Tail; public Color C; public bool Glow; }
struct Ring { public PointF P; public float Delay, Age, Life, Size, Thick; public Color C; public bool Inward; }
class Bolt { public PointF[][] Lines; public float Age, Life, W; public Color C; }

// Pooled particles, shockwaves, branching bolts, flash and shake. All timing is dt-based.
class Vfx
{
    const int Cap = 700; readonly Particle[] ps = new Particle[Cap]; int head; float clock, ember, trauma, flash;
    readonly List<Ring> rings = new(); readonly List<Bolt> bolts = new(); readonly Random rng = new(); Color flashC = Color.White;
    readonly Pen pen = new(Color.White) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
    readonly SolidBrush br = new(Color.White); readonly PointF[] arc = new PointF[5];
    public float ShakeX, ShakeY;
    public Vfx() { Clear(); }

    static int A(float v) => Math.Clamp((int)v, 0, 255);
    float R(float a, float b) => a + (b - a) * rng.NextSingle();
    public void Shake(float a) => trauma = Math.Min(1, trauma + a);
    public void Flash(float a, Color c) { flash = Math.Max(flash, a); flashC = c; }
    public void Clear() { for (int i = 0; i < Cap; i++) ps[i].Age = ps[i].Life = 1; rings.Clear(); bolts.Clear(); trauma = flash = ShakeX = ShakeY = 0; }

    void Emit(PointF p, float ang, float spd, float life, float size, Color c, float drag = 3, float tail = 0.04f, bool glow = false)
    { ps[head] = new Particle { X = p.X, Y = p.Y, VX = MathF.Cos(ang) * spd, VY = MathF.Sin(ang) * spd, Life = life, Size = size, C = c, Drag = drag, Tail = tail, Glow = glow }; head = (head + 1) % Cap; }
    void Sparks(PointF p, float ang, float spread, int n, float s0, float s1, Color c1, Color c2, float size, bool glow = false)
    { for (int i = 0; i < n; i++) Emit(p, ang + R(-spread, spread), R(s0, s1), R(.2f, .5f), size * R(.6f, 1.2f), rng.Next(2) == 0 ? c1 : c2, 3.5f, .045f, glow); }
    void AddRing(PointF p, float delay, float life, float size, float thick, Color c, bool inward = false) => rings.Add(new Ring { P = p, Delay = delay, Life = life, Size = size, Thick = thick, C = c, Inward = inward });
    PointF[] Jag(PointF a, float ang, float len, int n)
    {
        var pts = new PointF[n + 1]; pts[0] = a; float cx = MathF.Cos(ang), cy = MathF.Sin(ang);
        for (int i = 1; i <= n; i++) { float t = len * i / n, j = R(-1, 1) * len * .18f; pts[i] = new(a.X + cx * t - cy * j, a.Y + cy * t + cx * j); }
        return pts;
    }
    void AddBolt(PointF a, float ang, float len, Color c, float w)   // main bolt plus two branches
    {
        var main = Jag(a, ang, len, 5); var lines = new List<PointF[]> { main };
        for (int b = 0; b < 2; b++) lines.Add(Jag(main[rng.Next(1, 4)], ang + R(-1, 1), len * R(.3f, .55f), 4));
        bolts.Add(new Bolt { Lines = lines.ToArray(), Life = .25f, C = c, W = w });
    }

    public void Handle(GameEvent e)
    {
        float a = MathF.Atan2(e.N.Y, e.N.X), s = e.Scale, pi = MathF.PI;
        if (e.T == 1)   // Lightning ultimate: contracting rings (anticipation), then energy burst
        {
            AddRing(e.P, 0, .18f, 130, 6, Color.Cyan, true); AddRing(e.P, 0, .18f, 80, 4, Color.White, true);
            AddRing(e.P, .18f, .5f, 280, 12, Color.DeepSkyBlue); AddRing(e.P, .24f, .6f, 190, 6, Color.White);
            Sparks(e.P, 0, pi, 50, 250, 800, Color.Cyan, Color.White, 3);
            for (int i = 0; i < 24; i++) Emit(e.P, R(0, 6.28f), 900, .2f, 2.5f, Color.White, 1, .07f);
            for (int i = 0; i < 4; i++) AddBolt(e.P, R(0, 6.28f), R(110, 170), Color.Cyan, 3);
            Flash(.7f, Color.Cyan); Shake(.7f); return;
        }
        switch (e.K)
        {
            case Kind.Lightning:
                AddRing(e.P, 0, .3f, e.Ult ? 110 : 70, 5, Color.DeepSkyBlue); Sparks(e.P, a, pi, e.Ult ? 26 : 14, 200, 520, Color.Cyan, Color.White, 2.5f);
                for (int i = 0; i < (e.Ult ? 3 : 2); i++) AddBolt(e.P, R(0, 6.28f), R(60, 110), Color.Cyan, 2.5f);
                Flash(e.Ult ? .3f : .15f, Color.Cyan); Shake(e.Ult ? .35f : .15f); break;
            case Kind.Sword:
                Sparks(e.P, a, pi, e.Ult ? 46 : 14, 250, 700, Color.Orange, Color.White, 2.5f); AddRing(e.P, 0, .2f, e.Ult ? 140 : 60, 3, Color.White);
                for (int i = 0; i < (e.Ult ? 28 : 10); i++) Emit(e.P, R(0, 6.28f), 900, .16f, 2.5f, Color.White, 1, .07f);   // radial speed lines
                Flash(e.Ult ? .9f : .25f, Color.White); Shake(e.Ult ? .8f : .25f); break;
            default:   // Juggernaut: heavy purple shockwaves and debris
                AddRing(e.P, 0, .55f, 150 * s / 2, 14, Color.MediumPurple); AddRing(e.P, .08f, .7f, 220 * s / 2, 8, Color.Violet); AddRing(e.P, 0, .3f, 80 * s / 2, 6, Color.White);
                Sparks(e.P, a, pi, 20, 80, 320, Color.MediumPurple, Color.Indigo, 6, true); Flash(.25f, Color.MediumPurple); Shake(.55f); break;
        }
    }

    // continuous effects: lightning embers during the ultimate, sword-tip streaks while slashing
    public void Ambient(Sim sim, float dt)
    {
        if (dt <= 0) return;
        foreach (var b in sim.Balls)
        {
            if (!b.Alive) continue;
            if (b.D.K == Kind.Lightning && b.UltT > 0) { ember += dt * 90; while (ember >= 1) { ember--; Emit(b.P, R(0, 6.28f), R(30, 160), R(.25f, .5f), R(2, 4), rng.Next(2) == 0 ? Color.Cyan : Color.White, 2.5f, .03f, true); } }
            if (b.D.K == Kind.Sword && b.SlashT >= 0) { float ang = 0.6f + b.SlashT / 0.25f * MathF.PI * 2; Emit(new(b.P.X + MathF.Cos(ang) * (b.R + 30), b.P.Y + MathF.Sin(ang) * (b.R + 30)), ang + MathF.PI / 2, 150, .2f, 2, Color.White, 4, .05f); }
        }
    }

    public void Update(float dt)
    {
        clock += dt;
        for (int i = 0; i < Cap; i++) { ref var q = ref ps[i]; if (q.Age >= q.Life) continue; q.Age += dt; float d = Math.Max(0, 1 - q.Drag * dt); q.VX *= d; q.VY *= d; q.X += q.VX * dt; q.Y += q.VY * dt; }
        for (int i = rings.Count - 1; i >= 0; i--) { var r = rings[i]; r.Age += dt; if (r.Age >= r.Delay + r.Life) rings.RemoveAt(i); else rings[i] = r; }
        for (int i = bolts.Count - 1; i >= 0; i--) { bolts[i].Age += dt; if (bolts[i].Age >= bolts[i].Life) bolts.RemoveAt(i); }
        trauma = Math.Max(0, trauma - dt * 2.2f); flash = Math.Max(0, flash - dt * 4);
        float m = trauma * trauma * 16; ShakeX = R(-1, 1) * m; ShakeY = R(-1, 1) * m;
    }

    public void DrawBack(Graphics g)
    {
        foreach (var r in rings)
        {
            float t = r.Age - r.Delay; if (t < 0) continue; float k = t / r.Life, e = 1 - (1 - k) * (1 - k), rad = r.Size * (r.Inward ? 1 - e : e);
            int al = A(255 * (r.Inward ? MathF.Sin(k * MathF.PI) : 1 - k)); float w = r.Thick * (1 - .7f * k) + 1;
            pen.Color = Color.FromArgb(al / 3, r.C); pen.Width = w * 2.5f; g.DrawEllipse(pen, r.P.X - rad, r.P.Y - rad, 2 * rad, 2 * rad);
            pen.Color = Color.FromArgb(al, r.C); pen.Width = w; g.DrawEllipse(pen, r.P.X - rad, r.P.Y - rad, 2 * rad, 2 * rad);
        }
    }

    public void DrawFront(Graphics g)
    {
        foreach (var b in bolts)
        {
            float k = b.Age / b.Life; int al = A(255 * (1 - k)); if (rng.Next(4) == 0) continue;   // flicker
            foreach (var l in b.Lines)
            {
                pen.Color = Color.FromArgb(al / 4, b.C); pen.Width = b.W * 3; g.DrawLines(pen, l);
                pen.Color = Color.FromArgb(al, b.C); pen.Width = b.W; g.DrawLines(pen, l);
                pen.Color = Color.FromArgb(al, Color.White); pen.Width = 1.2f; g.DrawLines(pen, l);
            }
        }
        for (int i = 0; i < Cap; i++)
        {
            ref var q = ref ps[i]; if (q.Age >= q.Life) continue; float k = q.Age / q.Life; int al = A(255 * (1 - k) * (1 - k)); if (al <= 0) continue;
            if (q.Glow) { br.Color = Color.FromArgb(al, q.C); float s = q.Size * (1 - .5f * k); g.FillEllipse(br, q.X - s, q.Y - s, 2 * s, 2 * s); }
            else { pen.Color = Color.FromArgb(al, q.C); pen.Width = Math.Max(1, q.Size * (1 - k)); g.DrawLine(pen, q.X, q.Y, q.X - q.VX * q.Tail, q.Y - q.VY * q.Tail); }
        }
    }

    public void DrawFlash(Graphics g, Rectangle ar) { if (flash <= 0) return; br.Color = Color.FromArgb(A(Math.Min(1, flash) * 200), flashC); g.FillRectangle(br, ar); }

    // Lightning identity: pulsing neon aura layers plus crackling arcs hugging the body
    public void DrawAura(Graphics g, Ball b)
    {
        if (b.D.K != Kind.Lightning) return;
        bool on = b.UltT > 0 || b.Glow > 0; float pulse = .5f + .5f * MathF.Sin(clock * 9), r = b.R;
        for (int i = 4; i >= 1; i--) { float rr = r + i * (on ? 9 : 5) * (1 + .25f * pulse); br.Color = Color.FromArgb(Math.Max(4, (on ? 63 : 25) - i * (on ? 8 : 3)), 0, 190, 255); g.FillEllipse(br, b.P.X - rr, b.P.Y - rr, 2 * rr, 2 * rr); }
        if (!on && rng.Next(3) != 0) return;
        for (int n = 0; n < 3; n++)
        {
            float a0 = R(0, 6.28f); for (int i = 0; i < 5; i++) { float aa = a0 + i * .22f, rr = r + (i == 0 || i == 4 ? 2 : R(4, 14)); arc[i] = new(b.P.X + MathF.Cos(aa) * rr, b.P.Y + MathF.Sin(aa) * rr); }
            pen.Color = Color.FromArgb(220, Color.Cyan); pen.Width = 2; g.DrawLines(pen, arc);
        }
    }

    // Sword identity: layered crescent slash arc (dark underlay keeps it visible on the cream theme)
    public void DrawSlash(Graphics g, Ball b, float ang, float r)
    {
        float deg = ang * 57.2958f; var rc = new RectangleF(b.P.X - r, b.P.Y - r, 2 * r, 2 * r);   // r = blade-tip radius
        pen.Color = Color.FromArgb(70, 40, 40, 60); pen.Width = 10; g.DrawArc(pen, rc, deg - 150, 150);
        pen.Color = Color.FromArgb(150, 170, 200, 255); pen.Width = 6; g.DrawArc(pen, rc, deg - 150, 150);
        pen.Color = Color.White; pen.Width = 2.5f; g.DrawArc(pen, rc, deg - 120, 120);
    }
}
