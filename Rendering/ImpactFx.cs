using System.Drawing.Drawing2D;
namespace BallBrawl;

// Accessibility / comfort options (keys: F = reduce flashing, G = cycle screen-shake strength).
static class Settings { public static bool ReduceFlash; public static float Shake = 1f; }

class Impact { public BallCharacter A, B; public PointF P; public float Dir, Age, Life; public bool Ult, Burst; public int Seed; }
struct Snap { public PointF P; public float R, Ang, Age; public int Tint; public bool On; }
struct Shard { public float X, Y, VX, VY, Rot, VR, Size, Age, Life; public int Tint; public bool On; }

// Anime impact frames for Anime Sword Ball hits: anticipation -> slash compositions -> afterimages -> burst -> finishing frame.
// Render-side only: it is started by sword-hit GameEvents, follows the real sword pose, and never touches combat state.
// Palette: black, charcoal/greys, silver, crimson. Effects are localized panels (no full-screen strobing).
class ImpactFx : IDisposable
{
    static readonly Color Crimson = Color.FromArgb(190, 14, 34), Blood = Color.FromArgb(105, 12, 22), Silver = Color.FromArgb(222, 226, 234);
    static readonly Color[] Greys = { Color.FromArgb(38, 38, 44), Color.FromArgb(72, 72, 80), Color.FromArgb(120, 122, 130), Color.FromArgb(165, 168, 176) };
    static readonly float[] EN = { .14f, .34f, .50f, .50f, .78f, 1f }, EU = { .14f, .28f, .42f, .56f, .78f, 1f };   // phase end times (fraction of life)
    readonly List<Impact> imps = new(); readonly Snap[] snaps = new Snap[28]; readonly Shard[] shards = new Shard[90]; readonly Random rng = new();
    readonly SolidBrush br = new(Color.Black); readonly Pen pen = new(Color.Black) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
    readonly GraphicsPath path = new(); readonly PointF[] quad = new PointF[4], tri = new PointF[3], star = new PointF[24]; int sn, sh; float snapT;

    public void Dispose() { br.Dispose(); pen.Dispose(); path.Dispose(); }
    public void Clear() { imps.Clear(); Array.Clear(snaps); Array.Clear(shards); }
    static int A(float v) => Math.Clamp((int)v, 0, 255);
    static float H(int i, int seed) { float v = MathF.Sin(i * 12.9898f + seed * 78.233f) * 43758.5453f; return v - MathF.Floor(v); }
    static Color Tint(int i) => i switch { 0 => Greys[3], 1 => Greys[2], 2 => Greys[1], 3 => Crimson, 4 => Silver, _ => Greys[0] };

    public void Start(GameEvent e, float t)   // called once per real sword hit
    {
        if (e.Src == null || e.Dst == null) return;
        foreach (var m in imps) if (m.Age < .1f && m.A == e.Dst && m.B == e.Src) return;   // mirror event of the same collision
        imps.Add(new Impact { A = e.Src, B = e.Dst, P = e.P, Dir = SwordArt.Rest(e.Src, t) + MathF.PI / 2, Life = e.Ult ? .65f : .32f, Ult = e.Ult, Seed = rng.Next(1000) });   // Dir = tangent of the swing
    }

    public void Finish() { foreach (var m in imps) m.Age = m.Life; }   // ultimate complete

    public void Track(GameEngine s, float dt, float t)   // sample the real sword pose while it swings -> afterimages
    {
        snapT -= dt; if (s.Hit > 0 || s.CineT >= 0 || snapT > 0) return;
        foreach (var b in s.Balls)
        {
            if (!b.Alive || b.D.K != Kind.Sword || b.SlashT < 0 || !b.UltSlash) continue;   // ultimate slashes only
            snaps[sn] = new Snap { P = b.P, R = b.R, Ang = SwordArt.SlashAngle(SwordArt.Rest(b, t), b.SlashT), Tint = sn % 5, On = true }; sn = (sn + 1) % snaps.Length; snapT = .02f;
        }
    }

    public void Update(float dt)
    {
        for (int i = imps.Count - 1; i >= 0; i--)
        {
            var m = imps[i]; m.Age += dt; if (m.Age >= m.Life) { imps.RemoveAt(i); continue; }
            if (!m.Burst && m.Age / m.Life >= (m.Ult ? EU[3] : EN[2])) { m.Burst = true; Burst(m); }
        }
        for (int i = 0; i < snaps.Length; i++) { if (!snaps[i].On) continue; snaps[i].Age += dt; if (snaps[i].Age > .38f) snaps[i].On = false; }
        for (int i = 0; i < shards.Length; i++)
        {
            ref var d = ref shards[i]; if (!d.On) continue; d.Age += dt; if (d.Age >= d.Life) { d.On = false; continue; }
            float k = Math.Max(0, 1 - 3 * dt); d.VX *= k; d.VY *= k; d.X += d.VX * dt; d.Y += d.VY * dt; d.Rot += d.VR * dt;
        }
    }

    void Burst(Impact m)
    {
        int n = m.Ult ? 40 : 16;
        for (int i = 0; i < n; i++)
        {
            float a = (i < n / 2 ? m.Dir : m.Dir + MathF.PI) + (rng.NextSingle() - .5f) * 2.2f, sp = 150 + rng.NextSingle() * (m.Ult ? 650 : 420);
            shards[sh] = new Shard { X = m.P.X, Y = m.P.Y, VX = MathF.Cos(a) * sp, VY = MathF.Sin(a) * sp, Rot = a, VR = (rng.NextSingle() - .5f) * 20, Size = 4 + rng.NextSingle() * (m.Ult ? 11 : 8), Life = .35f + rng.NextSingle() * .3f, Tint = rng.Next(6), On = true };
            sh = (sh + 1) % shards.Length;
        }
    }

    (int, float) Phase(Impact m)
    {
        float tt = m.Age / m.Life; var e = m.Ult ? EU : EN;
        for (int i = 0; i < 6; i++) if (tt < e[i]) { float s0 = i == 0 ? 0 : e[i - 1]; return (i, (tt - s0) / (e[i] - s0)); }
        return (5, 1);
    }

    public void Draw(Graphics g, RectangleF ar, float t, bool cine)
    {
        try { DrawAll(g, ar, t, cine); } catch (Exception ex) when (ex is ArgumentException || ex is System.Runtime.InteropServices.ExternalException) { }   // a bad frame must never crash painting
    }

    void DrawAll(Graphics g, RectangleF ar, float t, bool cine)
    {
        float soft = Settings.ReduceFlash ? .6f : 1f;
        foreach (var m in imps)   // layer 1: dim the surroundings (hole tightens around the fight), then the angular panel + speed lines
        {
            var (ph, pp) = Phase(m); var f = new PointF((m.A.P.X + m.B.P.X) / 2, (m.A.P.Y + m.B.P.Y) / 2);
            float hole = ph == 0 ? 260 - 110 * pp : ph == 5 ? 140 : 150 - 30 * pp, al = (ph == 0 ? 55 + 45 * pp : ph == 5 ? 70 * (1 - pp) : ph == 4 ? 80 : 105) * soft;
            path.Reset(); path.AddRectangle(ar); path.AddEllipse(f.X - hole, f.Y - hole, 2 * hole, 2 * hole); br.Color = Color.FromArgb(A(al), 8, 6, 10); g.FillPath(br, path);
            int comp = Settings.ReduceFlash && ph >= 2 && ph <= 3 ? 1 : ph; if (comp < 1 || comp > 3) continue;
            float cs = MathF.Cos(m.Dir), sn_ = MathF.Sin(m.Dir), nx = -sn_, ny = cs, L = 300, W = 90 * Math.Min(1, .35f + pp * 1.3f) * (m.Ult ? 1.25f : 1f);
            quad[0] = new(m.P.X - cs * L + nx * W * .6f, m.P.Y - sn_ * L + ny * W * .6f); quad[1] = new(m.P.X + cs * L * .8f + nx * W, m.P.Y + sn_ * L * .8f + ny * W);
            quad[2] = new(m.P.X + cs * L - nx * W * .6f, m.P.Y + sn_ * L - ny * W * .6f); quad[3] = new(m.P.X - cs * L * .9f - nx * W, m.P.Y - sn_ * L * .9f - ny * W);
            br.Color = Color.FromArgb(225, comp == 1 ? Greys[0] : comp == 2 ? Blood : Greys[3]); g.FillPolygon(br, quad);
            pen.Color = Color.FromArgb(200, comp == 2 ? Crimson : Silver); pen.Width = 1.5f; g.DrawLine(pen, quad[0], quad[1]);
            for (int i = 0; i < 14; i++)   // directional lines follow the swing tangent
            {
                float off = (H(i, m.Seed) * 2 - 1) * W, len = 70 + H(i + 40, m.Seed) * 150, start = -L + ((H(i + 20, m.Seed) * 2 * L + (m.Age * 700)) % (2 * L));
                pen.Color = Color.FromArgb(190, Greys[(i + comp) % 4]); pen.Width = 1 + H(i + 60, m.Seed) * 1.5f;
                g.DrawLine(pen, m.P.X + cs * start + nx * off, m.P.Y + sn_ * start + ny * off, m.P.X + cs * (start + len) + nx * off, m.P.Y + sn_ * (start + len) + ny * off);
            }
        }
        foreach (var s in snaps)   // layer 2: afterimages (grey tints, occasional crimson) that fade and fragment
        {
            if (!s.On) continue; float k = s.Age / .38f, a = MathF.Pow(1 - k, 1.5f) * 200 * soft; var c = Tint(s.Tint);
            br.Color = Color.FromArgb(A(a * .4f), c); g.FillEllipse(br, s.P.X - s.R, s.P.Y - s.R, 2 * s.R, 2 * s.R);
            SwordArt.DrawShards(g, s.P, s.R / 28f, s.Ang, Color.FromArgb(A(a), c), k);
        }
        foreach (var m in imps)   // layer 3: silhouettes, crimson slash, burst, finishing frame
        {
            var (ph, pp) = Phase(m); int comp = Settings.ReduceFlash && ph >= 2 && ph <= 3 ? 1 : ph; var (ang, sc) = SwordArt.Pose(m.A, t, cine);
            float cs = MathF.Cos(m.Dir), sn_ = MathF.Sin(m.Dir), nx = -sn_, ny = cs;
            if (ph == 0) { SwordArt.DrawEdge(g, m.A.P, sc, ang, Crimson); continue; }   // anticipation: red edge on the blade
            if (ph <= 3 || ph == 5)
            {
                if (ph <= 3)
                {
                    float rec = 10 * (1 - pp) + 2, jx = (H((int)(m.Age * 90), m.Seed) - .5f) * 4;   // target recoils along the swing, distinct from the attacker
                    Color tf = comp == 1 ? Greys[2] : comp == 2 ? Greys[0] : Color.Black, to = comp == 2 ? Crimson : Silver;
                    br.Color = tf; pen.Color = to; pen.Width = 2; float bx = m.B.P.X + cs * rec + jx, by = m.B.P.Y + sn_ * rec, br2 = m.B.R * 1.03f; g.FillEllipse(br, bx - br2, by - br2, 2 * br2, 2 * br2); g.DrawEllipse(pen, bx - br2, by - br2, 2 * br2, 2 * br2);
                }
                float ra = m.A.R * 1.05f; br.Color = Color.Black; pen.Color = Silver; pen.Width = 2; g.FillEllipse(br, m.A.P.X - ra, m.A.P.Y - ra, 2 * ra, 2 * ra); g.DrawEllipse(pen, m.A.P.X - ra, m.A.P.Y - ra, 2 * ra, 2 * ra);
                SwordArt.Draw(g, m.A.P, sc, ang, 1); SwordArt.DrawEdge(g, m.A.P, sc, ang, Crimson);
                float ease = ph == 5 ? 1 : 1 - (1 - pp) * (1 - pp), Ls = ph == 5 ? 175 : 70 + 110 * ease, w = ph == 5 ? 2.5f * (1 - pp * .5f) : (m.Ult ? 10 : 7) * (1 - .3f * pp);
                float al = ph == 5 ? 255 * (1 - pp * pp) : 255;
                quad[0] = new(m.P.X - cs * Ls, m.P.Y - sn_ * Ls); quad[1] = new(m.P.X + nx * w, m.P.Y + ny * w); quad[2] = new(m.P.X + cs * Ls, m.P.Y + sn_ * Ls); quad[3] = new(m.P.X - nx * w, m.P.Y - ny * w);
                br.Color = Color.FromArgb(A(al), Crimson); g.FillPolygon(br, quad); pen.Color = Color.FromArgb(A(al * .8f), Silver); pen.Width = 1.2f; g.DrawLine(pen, quad[0], quad[2]);
                if (ph == 5) for (int i = 0; i < 3; i++)   // silver glints
                {
                    float gx = m.P.X + cs * (i - 1) * 70 + nx * (i - 1) * 10, gy = m.P.Y + sn_ * (i - 1) * 70 + ny * (i - 1) * 10, gl = 8 + 5 * i; pen.Color = Color.FromArgb(A(220 * (1 - pp)), Silver); pen.Width = 1.5f;
                    g.DrawLine(pen, gx - gl, gy, gx + gl, gy); g.DrawLine(pen, gx, gy - gl, gx, gy + gl);
                }
            }
            else   // burst: angular black/grey star, sharp radial lines, expanding crimson shockwave
            {
                float R2 = (30 + 90 * pp) * (m.Ult ? 1.5f : 1f), fade = 1 - pp * .6f;
                for (int layer = 0; layer < 2; layer++)
                {
                    float rr = layer == 0 ? R2 : R2 * .55f;
                    for (int i = 0; i < 24; i++) { float a = i * MathF.PI / 12 + m.Seed, r = i % 2 == 0 ? rr * (.8f + .4f * H(i, m.Seed)) : rr * .3f; star[i] = new(m.P.X + MathF.Cos(a) * r, m.P.Y + MathF.Sin(a) * r); }
                    br.Color = Color.FromArgb(A(235 * fade), layer == 0 ? Greys[1] : Color.Black); g.FillPolygon(br, star);
                }
                for (int i = 0; i < 14; i++)
                {
                    float a = i * MathF.PI * 2 / 14 + H(i, m.Seed) * .3f, r0 = R2 * .5f, r1 = R2 * (1.1f + .5f * H(i + 9, m.Seed)); pen.Color = Color.FromArgb(A(230 * fade), i % 3 == 0 ? Crimson : i % 3 == 1 ? Silver : Color.Black); pen.Width = i % 3 == 0 ? 2.5f : 1.5f;
                    g.DrawLine(pen, m.P.X + MathF.Cos(a) * r0, m.P.Y + MathF.Sin(a) * r0, m.P.X + MathF.Cos(a) * r1, m.P.Y + MathF.Sin(a) * r1);
                }
                float ring = 14 + 150 * pp; if (ring >= 3) { pen.Color = Color.FromArgb(A(230 * (1 - pp)), Crimson); pen.Width = Math.Min(1 + 5 * (1 - pp), ring); g.DrawEllipse(pen, m.P.X - ring, m.P.Y - ring, 2 * ring, 2 * ring); }
                SwordArt.Draw(g, m.A.P, sc, ang, 1);
            }
        }
        foreach (var d in shards)   // scattering angular shards that dissolve
        {
            if (!d.On) continue; float k = d.Age / d.Life, s = d.Size * (1 - .4f * k); br.Color = Color.FromArgb(A(255 * (1 - k)), Tint(d.Tint));
            for (int i = 0; i < 3; i++) { float a = d.Rot + (i == 0 ? 0 : i == 1 ? 2.4f : -2.4f), r = i == 0 ? s : s * .5f; tri[i] = new(d.X + MathF.Cos(a) * r, d.Y + MathF.Sin(a) * r); }
            g.FillPolygon(br, tri);
        }
    }
}
