using System.Drawing.Drawing2D;
namespace BallBrawl;

// Procedural pixel art. One "art pixel" = U screen pixels. Static scenery is drawn at low resolution into a cached bitmap and scaled
// with nearest-neighbour; balls are cached pixel sprites; panels, buttons and bars are built from solid blocks. Nothing is blurred or filtered.
static class Pix
{
    public const int U = 3;
    public static readonly Color Cream = Color.FromArgb(0xED, 0xE8, 0xD0), Ink = Color.FromArgb(30, 24, 46), Gold = Color.FromArgb(250, 200, 70);
    static readonly SolidBrush br = new(Color.White);
    static Bitmap scene; static Size sceneSz; static Rectangle sceneAr; static readonly Dictionary<(int, int), Bitmap> balls = new();

    static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);
    static Color Mix(Color a, Color b, float t) => Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    static void Fill(Graphics g, Color c, int x, int y, int w, int h) { br.Color = c; g.FillRectangle(br, x, y, w, h); }
    public static float Snap(float v) => MathF.Round(v / U) * U;
    public static void Dispose() { scene?.Dispose(); scene = null; foreach (var b in balls.Values) b.Dispose(); balls.Clear(); }

    // ---------- scenery ----------
    static void Ridge(Graphics g, int lw, int lh, int baseY, int amp, Color c1, Color c2, int seed)
    {
        for (int x = 0; x < lw; x++)
        {
            int h = baseY - (int)(amp * (.5 + .5 * Math.Sin(x * .05 + seed)) + amp * .5 * (.5 + .5 * Math.Sin(x * .16 + seed * 2)));
            Fill(g, c1, x, h, 1, lh - h); Fill(g, c2, x, h, 1, 1);
        }
    }
    static void Tree(Graphics g, int x, int gy)
    {
        Fill(g, C(112, 74, 42), x + 3, gy - 7, 2, 7); Fill(g, C(40, 128, 62), x, gy - 13, 8, 4); Fill(g, C(54, 150, 74), x + 1, gy - 16, 6, 4);
        Fill(g, C(72, 172, 88), x + 2, gy - 19, 4, 3); Fill(g, C(120, 210, 120), x + 2, gy - 19, 2, 1);
    }

    static Bitmap Build(Size sz, Rectangle ar)   // sky bands, far mountains, hills, trees, block ground, and (in a match) the framed arena
    {
        int lw = sz.Width / U + 1, lh = sz.Height / U + 1; var bmp = new Bitmap(lw, lh);
        using var g = Graphics.FromImage(bmp); g.SmoothingMode = SmoothingMode.None; g.PixelOffsetMode = PixelOffsetMode.Half;
        Color[] sky = { C(86, 160, 250), C(108, 180, 255), C(136, 198, 255), C(166, 214, 255), C(198, 230, 255), C(226, 242, 255) };
        int band = lh * 70 / 100 / sky.Length + 1; for (int i = 0; i < sky.Length; i++) Fill(g, sky[i], 0, i * band, lw, band + 1);
        Ridge(g, lw, lh, lh * 58 / 100, 26, C(118, 118, 190), C(150, 150, 220), 3); Ridge(g, lw, lh, lh * 68 / 100, 16, C(64, 146, 108), C(92, 176, 124), 7);
        var rng = new Random(11); int gy = lh * 80 / 100;
        for (int x = 4; x < lw - 8; x += 15 + rng.Next(9)) Tree(g, x, gy - rng.Next(3));
        for (int y = gy; y < lh; y++) for (int x = 0; x < lw; x++)   // grass cap over dirt blocks (8x8 art pixels)
        {
            int k = y - gy; Color c = k == 0 ? C(120, 220, 90) : k < 3 ? C(70, 164, 64) : (((x / 8 + k / 8) & 1) == 0 ? C(138, 92, 56) : C(124, 82, 48));
            if (k >= 3 && (x % 8 == 0 || k % 8 == 0)) c = C(96, 62, 38); else if (k >= 3 && (x * 7 + y * 13) % 11 == 0) c = C(158, 108, 66);
            Fill(g, c, x, y, 1, 1);
        }
        if (ar.IsEmpty) return bmp;
        int ax = ar.X / U, ay = ar.Y / U, aw = ar.Width / U, ah = ar.Height / U, t = 3;
        for (int y = 0; y < ah; y++) for (int x = 0; x < aw; x++) Fill(g, ((x / 8 + y / 8) & 1) == 0 ? C(44, 62, 108) : C(50, 70, 118), ax + x, ay + y, 1, 1);   // calm checker keeps the balls readable
        for (int by = ay - t; by < ay + ah + t; by += t) for (int bx = ax - t; bx < ax + aw + t; bx += t)   // stone block frame just outside the collision area
        {
            if (bx >= ax && bx < ax + aw && by >= ay && by < ay + ah) continue;
            bool top = by == ay - t; Fill(g, top ? C(138, 92, 56) : C(128, 128, 150), bx, by, t, t);
            Fill(g, top ? C(158, 108, 66) : C(176, 176, 200), bx, by, t, 1); Fill(g, top ? C(158, 108, 66) : C(176, 176, 200), bx, by, 1, t);
            Fill(g, top ? C(96, 62, 38) : C(84, 84, 108), bx, by + t - 1, t, 1); Fill(g, top ? C(96, 62, 38) : C(84, 84, 108), bx + t - 1, by, 1, t);
            if (top) Fill(g, C(120, 220, 90), bx, by, t, 1); if ((bx * 5 + by * 3) % 7 == 0) Fill(g, C(70, 70, 92), bx + 1, by + 1, 1, 1);
        }
        return bmp;
    }

    public static void Scene(Graphics g, Size sz, Rectangle ar, float t)   // cached scenery + animated sun, clouds, grass, crystals, motes
    {
        if (scene == null || sceneSz != sz || sceneAr != ar) { scene?.Dispose(); scene = Build(sz, ar); sceneSz = sz; sceneAr = ar; }
        g.DrawImage(scene, 0, 0, scene.Width * U, scene.Height * U);
        int W = sz.Width, H = sz.Height, gy = (H / U + 1) * 80 / 100 * U;
        Fill(g, C(255, 232, 120), W - 90, 40, 36, 36); Fill(g, C(255, 250, 190), W - 84, 46, 12, 12);
        if (((int)(t * 2) & 1) == 0) { Fill(g, C(255, 232, 120), W - 78, 28, 12, 9); Fill(g, C(255, 232, 120), W - 78, 79, 12, 9); Fill(g, C(255, 232, 120), W - 102, 52, 9, 12); Fill(g, C(255, 232, 120), W - 51, 52, 9, 12); }
        for (int i = 0; i < 4; i++)   // drifting pixel clouds
        {
            int x = (int)Snap((t * (5 + i * 2) + i * 230) % (W + 160) - 80), y = (int)Snap(50 + i * 38);
            Fill(g, C(214, 228, 250), x, y + 9, 66, 9); Fill(g, Color.White, x + 6, y + 3, 54, 9); Fill(g, Color.White, x + 15, y - 3, 27, 9);
        }
        for (int x = 0; x < W; x += 27)   // swaying grass tufts
        {
            int sw = (int)Math.Round(Math.Sin(t * 2 + x * .1) * 1.2) * U;
            Fill(g, C(150, 235, 110), x, gy - 6, 3, 6); Fill(g, C(150, 235, 110), x + 3 + sw, gy - 9, 3, 3);
        }
        for (int i = 0; i < 2; i++)   // glowing crystals
        {
            int cx = i == 0 ? 36 : W - 54; float glow = .5f + .5f * MathF.Sin(t * 3 + i * 2);
            Fill(g, Color.FromArgb((int)(60 * glow), 170, 120, 255), cx - 12, gy - 33, 36, 36);
            var col = Mix(C(120, 80, 220), C(200, 160, 255), glow); Fill(g, col, cx, gy - 27, 9, 27); Fill(g, col, cx - 6, gy - 15, 21, 12); Fill(g, Color.White, cx + 3, gy - 24, 3, 6);
        }
        for (int i = 0; i < 14; i++)   // floating motes (never over the arena)
        {
            int x = (int)Snap((i * 97 + t * (6 + i % 4)) % W), y = (int)Snap(H - (i * 61 + t * (10 + i % 3)) % H);
            if (!ar.IsEmpty && ar.Contains(x, y)) continue; Fill(g, Color.FromArgb(170, i % 2 == 0 ? Gold : Color.White), x, y, U, U);
        }
    }

    // ---------- sprites ----------
    static Bitmap MakeBall(Color c, float rad)   // shaded, dithered pixel ball with outline and eyes
    {
        int n = (int)MathF.Ceiling(rad * 2) + 2; var bmp = new Bitmap(n, n); float m = n / 2f;
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float dx = x + .5f - m, dy = y + .5f - m, d = MathF.Sqrt(dx * dx + dy * dy); if (d > rad) continue;
            if (d > rad - 1.1f) { bmp.SetPixel(x, y, Ink); continue; }
            float l = (-dx * .45f - dy * .7f) / rad + (((x + y) & 1) == 0 ? .07f : -.07f);
            bmp.SetPixel(x, y, l > .6f ? Mix(c, Color.White, .55f) : l > .2f ? Mix(c, Color.White, .22f) : l > -.3f ? c : Mix(c, Ink, .38f));
        }
        if (rad >= 5)
        {
            int eh = rad > 12 ? 3 : 2, ew = rad > 12 ? 2 : 1, ey = (int)(m - rad * .15f), ex = (int)MathF.Round(rad * .34f);
            for (int side = -1; side <= 1; side += 2)
            {
                int x0 = (int)MathF.Round(m + side * ex) - ew / 2;
                for (int yy = 0; yy < eh; yy++) for (int xx = 0; xx < ew; xx++) bmp.SetPixel(x0 + xx, ey + yy, yy == eh - 1 && xx == (side < 0 ? ew - 1 : 0) ? Ink : Color.White);
            }
        }
        return bmp;
    }
    public static void Ball(Graphics g, PointF p, float r, Color c)
    {
        float rad = r / U; var key = (c.ToArgb(), (int)(rad * 10));
        if (!balls.TryGetValue(key, out var bmp)) balls[key] = bmp = MakeBall(c, rad);
        int n = bmp.Width * U; g.DrawImage(bmp, Snap(p.X) - n / 2f, Snap(p.Y) - n / 2f, n, n);
    }

    // ---------- UI blocks ----------
    static void Block(Graphics g, Rectangle r, Color fill, Color hi, Color lo)
    {
        Fill(g, Ink, r.X, r.Y + U, r.Width, r.Height - 2 * U); Fill(g, Ink, r.X + U, r.Y, r.Width - 2 * U, r.Height);   // notched outline
        Fill(g, fill, r.X + U, r.Y + U, r.Width - 2 * U, r.Height - 2 * U);
        Fill(g, hi, r.X + U, r.Y + U, r.Width - 2 * U, U); Fill(g, hi, r.X + U, r.Y + U, U, r.Height - 2 * U);
        Fill(g, lo, r.X + U, r.Bottom - 2 * U, r.Width - 2 * U, U); Fill(g, lo, r.Right - 2 * U, r.Y + U, U, r.Height - 2 * U);
    }
    public static void Panel(Graphics g, Rectangle r, bool hi = false, float t = 0)
    {
        if (!hi) { Block(g, r, C(58, 48, 86), C(104, 90, 150), C(38, 30, 60)); return; }
        Block(g, r, C(120, 92, 40), Gold, C(84, 60, 24));
        if (((int)(t * 4) & 1) == 0) { Fill(g, Gold, r.X, r.Y, U, U); Fill(g, Gold, r.Right - U, r.Y, U, U); Fill(g, Gold, r.X, r.Bottom - U, U, U); Fill(g, Gold, r.Right - U, r.Bottom - U, U, U); }   // blinking corner studs
    }
    public static void Button(Graphics g, Rectangle r, bool on, bool hov)
    {
        Fill(g, Color.FromArgb(90, 0, 0, 0), r.X + U, r.Bottom, r.Width - 2 * U, U);
        if (!on) Block(g, r, C(112, 108, 126), C(150, 146, 164), C(80, 76, 96)); else if (hov) Block(g, r, C(255, 178, 66), C(255, 222, 130), C(190, 100, 30)); else Block(g, r, C(238, 142, 44), C(255, 196, 100), C(170, 86, 26));
    }
    public static void Bar(Graphics g, int x, int y, int w, int h, float k, Color c)   // segmented pixel bar
    {
        Fill(g, Ink, x - U, y - U, w + 2 * U, h + 2 * U); Fill(g, C(60, 52, 84), x, y, w, h);
        int seg = U * 3, n = w / seg, on = (int)MathF.Ceiling(Math.Clamp(k, 0, 1) * n - .001f);
        for (int i = 0; i < on; i++) { Fill(g, c, x + i * seg, y, seg - 1, h); Fill(g, Mix(c, Color.White, .45f), x + i * seg, y, seg - 1, U); }
    }
}