using System.Drawing.Drawing2D;
namespace BallBrawl;

// Events the gameplay sim raises; the renderer turns them into effects. Gameplay never reads VFX state.
readonly record struct GameEvent(int T, Kind K, PointF P, PointF N, bool Ult, float Scale, Ball Src = null, Ball Dst = null); // T: 0 = hit, 1 = lightning ultimate start

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
    public void Shake(float a) => trauma = Math.Min(1, trauma + a * Settings.Shake);
    public void Flash(float a, Color c) { if (Settings.ReduceFlash) a *= .25f; flash = Math.Max(flash, a); flashC = c; }
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
        if (e.T >= 2 || e.K >= Kind.Inferno) { HandleExt(e); return; }
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
                Sparks(e.P, a, pi, e.Ult ? 46 : 14, 250, 700, Color.FromArgb(200, 20, 40), Color.FromArgb(215, 220, 228), 2.5f); AddRing(e.P, 0, .2f, e.Ult ? 140 : 60, 3, Color.FromArgb(200, 20, 40));
                for (int i = 0; i < (e.Ult ? 28 : 10); i++) Emit(e.P, R(0, 6.28f), 900, .16f, 2.5f, Color.FromArgb(150, 152, 160), 1, .07f);   // radial speed lines
                Shake(e.Ult ? .8f : .25f); break;   // no full-arena flash for the sword: ImpactFx draws localized frames
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
            AmbientExt(b, dt);
            if (b.D.K == Kind.Lightning && b.UltT > 0) { ember += dt * 90; while (ember >= 1) { ember--; Emit(b.P, R(0, 6.28f), R(30, 160), R(.25f, .5f), R(2, 4), rng.Next(2) == 0 ? Color.Cyan : Color.White, 2.5f, .03f, true); } }
            if (b.D.K == Kind.Sword && b.SlashT >= 0) { float ang = 0.6f + b.SlashT / 0.25f * MathF.PI * 2; Emit(new(b.P.X + MathF.Cos(ang) * (b.R + 30), b.P.Y + MathF.Sin(ang) * (b.R + 30)), ang + MathF.PI / 2, 150, .2f, 2, Color.White, 4, .05f); }
        }
    }

    static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);
    int Rate(float perSec, float dt) { float c = perSec * dt; int n = (int)c; return n + (rng.NextSingle() < c - n ? 1 : 0); }
    static (Color, Color) Pal(Kind k) => k switch
    {
        Kind.Inferno => (Color.OrangeRed, Color.Gold), Kind.Frost => (C(170, 220, 255), Color.White), Kind.Void => (C(150, 60, 220), C(220, 160, 255)), Kind.Venom => (C(110, 220, 40), C(190, 255, 120)),
        Kind.Solar => (Color.Gold, Color.White), Kind.Mecha => (C(150, 170, 190), C(255, 220, 150)), Kind.Cosmic => (C(200, 90, 230), C(255, 170, 230)), Kind.Tempest => (C(190, 240, 255), Color.White), _ => (Color.Cyan, Color.White)
    };

    // events: 0 hit, 2 ultimate end, 10 ultimate start, 11 passive proc, 13 meteor impact, 14 ice shatter, 15 black-hole implosion, 16 cloud end, 17 status applied
    void HandleExt(GameEvent e)
    {
        var p = e.P; float rad = e.Scale, a = MathF.Atan2(e.N.Y, e.N.X), pi = MathF.PI; var K = e.K; var (c1, c2) = Pal(K);
        switch (e.T)
        {
            case 0:
                Sparks(p, a, pi, 14, 180, 520, c1, c2, 2.5f); AddRing(p, 0, .35f, K == Kind.Mecha ? 55 : 65, K == Kind.Mecha ? 6 : 4, c1);
                if (K == Kind.Inferno) Sparks(p, -pi / 2, .8f, 8, 40, 140, c1, c2, 6, true);
                else if (K == Kind.Venom || K == Kind.Cosmic) Sparks(p, a, pi, 10, 60, 240, c1, c2, 4, true);
                else if (K == Kind.Tempest || K == Kind.Solar) for (int i = 0; i < 10; i++) Emit(p, R(0, 6.28f), 800, .16f, 2.3f, c2, 1, .07f);
                else if (K == Kind.Void) AddRing(p, 0, .3f, 70, 3, c2, true);
                Flash(.12f, c1); Shake(K == Kind.Mecha ? .3f : .18f); break;
            case 2:
                AddRing(p, 0, .5f, K == Kind.Tempest ? 220 : 80, K == Kind.Tempest ? 8 : 4, c1);
                if (K == Kind.Tempest) { for (int i = 0; i < 24; i++) Emit(p, R(0, 6.28f), 900, .25f, 2.5f, c2, 1, .08f); Shake(.4f); } else Sparks(p, 0, pi, 10, 100, 300, c1, c2, 2.5f);
                break;
            case 10:
                AddRing(p, 0, .15f, rad * .6f, 6, c2, true); AddRing(p, .15f, .6f, rad, 14, c1); AddRing(p, .22f, .7f, rad * .7f, 7, c2); Sparks(p, 0, pi, 50, 250, 800, c1, c2, 3, K != Kind.Tempest);
                switch (K)
                {
                    case Kind.Inferno: Sparks(p, -pi / 2, 1.2f, 20, 40, 160, c1, c2, 9, true); Sparks(p, -pi / 2, 1.5f, 10, 20, 80, Color.FromArgb(120, 70, 70, 70), Color.FromArgb(120, 90, 90, 90), 12, true); Flash(1f, c1); Shake(.9f); break;   // flames + smoke
                    case Kind.Solar: for (int i = 0; i < 36; i++) Emit(p, i * .1745f, 1000, .3f, 3, c1, 1, .09f); Flash(1f, c2); Shake(.7f); break;
                    case Kind.Void: AddRing(p, .1f, .6f, rad * .9f, 8, c1, true); Flash(.3f, c1); Shake(.4f); break;
                    case Kind.Tempest: for (int i = 0; i < 30; i++) Emit(p, R(0, 6.28f), 900, .25f, 2.5f, c2, 1, .08f); Shake(.5f); break;
                    default: Flash(.4f, c1); Shake(.5f); break;
                }
                break;
            case 11: AddRing(p, 0, .4f, K == Kind.Solar ? rad : 60, 6, c1); Sparks(p, 0, pi, 24, 200, 600, c1, c2, 3); if (K == Kind.Solar) { Flash(.5f, c1); Shake(.4f); } break;
            case 13: AddRing(p, 0, .45f, rad * 1.5f, 10, c1); AddRing(p, .05f, .5f, rad, 6, c2); Sparks(p, 0, pi, 30, 200, 700, c1, c2, 3); Sparks(p, 0, pi, 10, 40, 200, c1, c2, 6, true); Flash(.5f, c2); Shake(.6f); break;
            case 14:
                AddRing(p, 0, .5f, 170, 8, c1);
                for (int i = 0; i < 24; i++) Emit(p, i * .2618f + R(-.1f, .1f), R(350, 800), R(.4f, .7f), 3.5f, i % 2 == 0 ? c1 : c2, 1.5f, .05f);   // outward ice shards
                Sparks(p, 0, pi, 30, 100, 500, c1, c2, 2.5f); Flash(.6f, c2); Shake(.6f); break;
            case 15: AddRing(p, 0, .25f, rad, 10, c1, true); AddRing(p, .25f, .6f, rad * 1.4f, 12, c2); Sparks(p, 0, pi, 40, 250, 800, c1, c2, 3); Flash(.7f, c1); Shake(.7f); break;   // implosion, then energy pulse
            case 16: AddRing(p, 0, .6f, rad, 4, c1); Sparks(p, 0, pi, 12, 30, 120, c1, c2, 5, true); break;
            case 17: Sparks(p, -pi / 2, pi, 6, 30, 120, c1, c2, K == Kind.Venom ? 4 : 2.5f, K != Kind.Frost); break;
        }
    }

    // continuous per-character effects (rates are per second, so they look the same at any frame rate)
    void AmbientExt(Ball b, float dt)
    {
        var back = new PointF(b.P.X - b.V.X * b.R, b.P.Y - b.V.Y * b.R); float vang = MathF.Atan2(-b.V.Y, -b.V.X), up = -MathF.PI / 2;
        if (b.BurnT > 0) for (int i = Rate(25, dt); i > 0; i--) Emit(b.P, up + R(-.5f, .5f), R(30, 90), R(.3f, .6f), R(2, 4), rng.Next(2) == 0 ? Color.OrangeRed : Color.Gold, 1, 0, true);
        switch (b.D.K)
        {
            case Kind.Inferno: for (int i = Rate(30, dt); i > 0; i--) Emit(back, vang + R(-.5f, .5f), R(20, 80), R(.3f, .6f), R(3, 6), rng.Next(3) == 0 ? Color.Gold : Color.OrangeRed, 2, 0, true); if (rng.Next(25) == 0) Emit(b.P, up, 25, 1f, 8, Color.FromArgb(90, 70, 70, 70), 1, 0, true); break;
            case Kind.Frost: for (int i = Rate(18, dt); i > 0; i--) Emit(back, R(0, 6.28f), R(10, 40), R(.5f, .9f), R(4, 7), Color.FromArgb(120, 200, 235, 255), 1.5f, 0, true); break;
            case Kind.Venom: for (int i = Rate(14, dt); i > 0; i--) Emit(b.P, R(0, 6.28f), R(10, 50), R(.4f, .8f), R(2, 4), Color.FromArgb(200, 120, 230, 60), 1, 0, true); break;
            case Kind.Solar: for (int i = Rate(b.Energy / 3, dt); i > 0; i--) { float a = R(0, 6.28f), d = b.R + 40; Emit(new(b.P.X + MathF.Cos(a) * d, b.P.Y + MathF.Sin(a) * d), a + MathF.PI, 100, .4f, 3, Color.Gold, .5f, 0, true); } break;
            case Kind.Mecha: if (b.UltT > 0) for (int i = Rate(40, dt); i > 0; i--) Emit(b.P, R(0, 6.28f), R(150, 400), R(.15f, .3f), 2, Color.FromArgb(255, 220, 120), 3, .04f); break;
            case Kind.Cosmic: for (int i = Rate(b.BoostT > 0 ? 50 : 20, dt); i > 0; i--) Emit(back, vang + R(-.3f, .3f), R(10, 60), R(.4f, .8f), R(2, 4), rng.Next(2) == 0 ? Color.FromArgb(255, 170, 230) : Color.White, 1, 0, true); break;
            case Kind.Tempest: for (int i = Rate(b.UltT > 0 ? 70 : 25, dt); i > 0; i--) Emit(back, vang + R(-.4f, .4f), R(150, 300), R(.2f, .4f), 2, rng.Next(2) == 0 ? Color.White : Color.FromArgb(150, 235, 255), 3, .06f); break;
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

    void Ring(Graphics g, PointF p, float rad)   // pen width is capped at the radius; a failed draw must never crash painting
    { try { g.DrawEllipse(pen, p.X - rad, p.Y - rad, 2 * rad, 2 * rad); } catch (System.Runtime.InteropServices.ExternalException) { } }

    public void DrawBack(Graphics g)
    {
        foreach (var r in rings)
        {
            float t = r.Age - r.Delay; if (t < 0) continue; float k = Math.Min(1f, t / r.Life), e = 1 - (1 - k) * (1 - k), rad = r.Size * (r.Inward ? 1 - e : e);
            if (!(rad >= 3f) || rad > 3000f) continue;   // GDI+ throws on tiny/invalid ellipses (inward rings shrink to ~0)
            int al = A(255 * (r.Inward ? MathF.Sin(k * MathF.PI) : 1 - k)); float w = r.Thick * (1 - .7f * k) + 1;
            pen.Color = Color.FromArgb(al / 3, r.C); pen.Width = Math.Min(w * 2.5f, rad); Ring(g, r.P, rad);
            pen.Color = Color.FromArgb(al, r.C); pen.Width = Math.Min(w, rad); Ring(g, r.P, rad);
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
            if (q.Glow) { br.Color = Color.FromArgb(al, q.C); float s = q.Size * (1 - .5f * k); g.FillRectangle(br, Pix.Snap(q.X) - s, Pix.Snap(q.Y) - s, 2 * s, 2 * s); }
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