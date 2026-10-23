using System.Diagnostics;
using System.Drawing.Drawing2D;
using Timer = System.Windows.Forms.Timer;

namespace BallBrawl;

// ============================ UI / RENDERING ============================
enum State { Menu, Select, Match, Results }

class GameForm : Form
{
    static readonly Color Theme = Color.FromArgb(0xED, 0xE8, 0xD0), Ink = Color.FromArgb(0xED, 0xE8, 0xD0);
    List<BallResult> results = new();
    Vfx vfx = new(); ImpactFx impact = new(); float prevCine = -1, curCine = -1, toast;
    readonly MatchManager matches = new();
    State st; GameEngine bg, match; Timer tm = new() { Interval = 15 }; Stopwatch sw = Stopwatch.StartNew(); double lastT;
    float fade; Point mouse; List<Def> pick = new(); Def view; Def winner; bool draw; float matchTime, endDelay; Random rng = new();
    Rectangle bPlay, bExit, bBack, bStart, bAgain, bMenu;

    public GameForm()
    {
        Text = "Ball Brawl"; ClientSize = new(900, 600); MinimumSize = new(700, 520); DoubleBuffered = true; BackColor = Theme;
        tm.Tick += (_, _) => Tick(); tm.Start();
        Audio.Init(Path.Combine(AppContext.BaseDirectory, "Assets", "Audio")); FormClosed += (_, _) => { tm.Stop(); tm.Dispose(); Audio.Shutdown(); vfx.Dispose(); impact.Dispose(); CharArt.Dispose(); SwordArt.Dispose(); Pix.Dispose(); }; KeyDown += OnKey;
        MouseMove += (_, e) => mouse = e.Location; MouseDown += (_, e) => HandleClick(e.Location); Resize += (_, _) => ArrangeControls();
        Go(State.Menu);
    }

    Rectangle Arena() { int w = Math.Max(243, ClientSize.Width - 500), h = Math.Min(ClientSize.Height - 100, 400); w -= w % 9; h -= h % 9; return new((ClientSize.Width - w) / 2 / 3 * 3, (ClientSize.Height - h) / 2 / 3 * 3, w, h); }   // multiples of the art-pixel grid

    void OnKey(object o, KeyEventArgs e)   // Up/Down = master volume, M = mute
    {
        if (e.KeyCode == Keys.Up) Audio.Master = Math.Min(1, Audio.Master + .1f); else if (e.KeyCode == Keys.Down) Audio.Master = Math.Max(0, Audio.Master - .1f);
        else if (e.KeyCode == Keys.M) Audio.Master = Audio.Master > 0 ? 0 : .8f;
        else if (e.KeyCode == Keys.F) Settings.ReduceFlash = !Settings.ReduceFlash; else if (e.KeyCode == Keys.G) Settings.Shake = Settings.Shake > .6f ? .5f : Settings.Shake > .1f ? 0f : 1f; else return; toast = 1.5f;
    }

    void ArrangeControls()
    {
        int cx = ClientSize.Width / 2, h = ClientSize.Height;
        bPlay = new(cx - 100, h / 2 - 10, 200, 56); bExit = new(cx - 100, h / 2 + 62, 200, 56);
        bBack = new(20, h - 70, 120, 48); bStart = new(ClientSize.Width - 240, h - 70, 220, 48);
        bAgain = new(cx - 150, h / 2 + 90, 300, 52); bMenu = new(cx - 150, h / 2 + 156, 300, 52);
        if (bg != null) { bg.W = ClientSize.Width; bg.H = ClientSize.Height; } if (match != null) { var a = Arena(); match.W = a.Width; match.H = a.Height; }
    }

    void Go(State s)
    {
        st = s; fade = 1; ArrangeControls(); vfx.Clear(); impact.Clear(); prevCine = -1; curCine = -1;
        if (s != State.Menu) bg = null;                                  // stop background battle
        if (s == State.Menu) { match = null; bg = NewBg(); }
        if (s == State.Select) { match = null; pick.Clear(); view = Roster.All[0]; }
    }

    GameEngine NewBg()
    {
        var all = Roster.All.OrderBy(_ => rng.Next()).ToArray();
        return new GameEngine(all.Length >= 2 ? all.Take(2).ToArray() : all, true, ClientSize.Width, ClientSize.Height);
    }

    void HandleClick(Point p)
    {
        if (fade > 0.5f) return; if (st != State.Match) Audio.Play("click", .5f, .05f);
        if (st == State.Menu) { if (bPlay.Contains(p)) Go(State.Select); else if (bExit.Contains(p)) Close(); }
        else if (st == State.Select)
        {
            if (bBack.Contains(p)) Go(State.Menu);
            else if (bStart.Contains(p) && pick.Count >= 2) StartMatch();
            else if (Enumerable.Range(0, pick.Count).Any(i => ChipRect(i).Contains(p))) pick.RemoveAt(Enumerable.Range(0, pick.Count).First(i => ChipRect(i).Contains(p)));
            else for (int i = 0; i < Roster.All.Length; i++) if (CardRect(i).Contains(p))
            {
                var d = Roster.All[i]; view = d;
                if (pick.Count < 5) pick.Add(d);
            }
        }
        else if (st == State.Results) { if (bAgain.Contains(p)) Go(State.Select); else if (bMenu.Contains(p)) Go(State.Menu); }
    }

    void StartMatch()
    {
        var ar = Arena(); match = matches.Start(pick.ToArray(), false, ar.Width, ar.Height); matchTime = 0; endDelay = 0; Audio.Play("match_start", 1f); vfx.Clear(); impact.Clear(); st = State.Match; fade = 1;
    }

    bool Pump(GameEngine s, float dt)   // drains gameplay events into the renderer's effect system
    {
        Audio.Scale = ReferenceEquals(s, bg) ? .25f : 1f; foreach (var e in s.Events.Drain()) { vfx.Handle(e); Audio.OnEvent(e); if (e.T == 20) impact.Start(e, (float)sw.Elapsed.TotalSeconds); else if (e.T == 21) impact.Finish(); } if (s.CineT >= 0) Audio.Cine(prevCine, s.CineT);
        if (prevCine >= 0 && s.CineT < 0) { vfx.Shake(.6f); }   // snap-back flash
        prevCine = s.CineT; float vdt = s.Hit > 0 ? 0 : dt;   // effects freeze together with hit-stop
        vfx.Ambient(s, vdt); Audio.Ambient(s, vdt); vfx.Update(vdt); impact.Track(s, dt, (float)sw.Elapsed.TotalSeconds); impact.Update(dt); return true;
    }

    void Tick()
    {
        double now = sw.Elapsed.TotalSeconds; float dt = (float)Math.Min(now - lastT, 0.05); lastT = now; toast = Math.Max(0, toast - dt); Audio.Update(dt); fade = Math.Max(0, fade - dt * 3);
        if (st == State.Menu) { if (bg != null) Pump(bg, dt); bg?.Step(dt); }
        else if (st == State.Match && match != null && Pump(match, dt))
        {
            if (!match.Over || match.CineT >= 0) { match.Step(dt); matchTime += dt; }
            else if ((endDelay += dt) > 1.2f)
            {
                Audio.Play("match_end", 1f); MatchOutcome outcome = matches.Complete(match); results = outcome.Results.ToList(); winner = outcome.Winner; draw = outcome.Draw; match = null; st = State.Results; fade = 1; ArrangeControls();
            }
        }
        Invalidate();
    }

    Rectangle ChipRect(int i) => new((ClientSize.Width - 5 * 48) / 2 + i * 48, 475, 40, 40);
    Rectangle CardRect(int i) { int cols = 6, w = 128, h = 98, gap = 10, x0 = (ClientSize.Width - (cols * (w + gap) - gap)) / 2; return new(x0 + i % cols * (w + gap), 68 + i / cols * (h + gap), w, h); }

    // ---- drawing ----
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.None; g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        g.Clear(Theme); Pix.Scene(g, ClientSize, st == State.Match ? Arena() : Rectangle.Empty, (float)sw.Elapsed.TotalSeconds);
        switch (st)
        {
            case State.Menu:
                if (bg != null) DrawWorld(g, bg, false);
                Txt(g, "BALL BRAWL", 54, ClientSize.Width / 2, ClientSize.Height / 2 - 110, true);
                Btn(g, bPlay, "Play", true); Btn(g, bExit, "Exit", true); break;
            case State.Select: DrawSelect(g); break;
            case State.Match: if (match != null) DrawWorld(g, match, true); break;
            case State.Results: DrawResults(g); break;
        }
        if (toast > 0) Txt(g, $"Volume {Audio.Master * 100:0}% (Up/Down, M)   Reduce flashing: {(Settings.ReduceFlash ? "ON" : "off")} (F)   Shake {Settings.Shake * 100:0}% (G)", 11, ClientSize.Width / 2, ClientSize.Height - 18);
        if (fade > 0) { using var br = new SolidBrush(Color.FromArgb((int)(fade * 255), Theme)); g.FillRectangle(br, ClientRectangle); }
    }

    void Txt(Graphics g, string s, float size, float x, float y, bool bold = false, Color? c = null)   // pixel-style text: crisp mono font with a dark outline
    {
        using var f = new Font("Consolas", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point); var sz = g.MeasureString(s, f); float px = x - sz.Width / 2, py = y - sz.Height / 2;
        using (var sh = new SolidBrush(Pix.Ink)) foreach (var (dx, dy) in new[] { (-2, 0), (2, 0), (0, -2), (0, 2), (2, 2) }) g.DrawString(s, f, sh, px + dx, py + dy);
        using var fg = new SolidBrush(c ?? Pix.Cream); g.DrawString(s, f, fg, px, py);
    }

    void Btn(Graphics g, Rectangle r, string t, bool on)   // hover lifts the button
    {
        bool hov = on && r.Contains(mouse); var rr = hov ? new Rectangle(r.X, r.Y - 3, r.Width, r.Height) : r;
        Pix.Button(g, rr, on, hov); Txt(g, t, 16, rr.X + rr.Width / 2, rr.Y + rr.Height / 2 - 2, true, on ? Color.White : Color.FromArgb(200, 196, 210));
    }
    static GraphicsPath Round(Rectangle r, int d)
    {
        var p = new GraphicsPath(); p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }

    void DrawSelect(Graphics g)
    {
        Txt(g, "Choose 2 to 5 Characters (click a card to add)", 28, ClientSize.Width / 2, 45, true);
        for (int i = 0; i < Roster.All.Length; i++)
        {
            var d = Roster.All[i]; var r = CardRect(i); bool sel = pick.Contains(d);
            Pix.Panel(g, r, sel || r.Contains(mouse), (float)sw.Elapsed.TotalSeconds);
            BallCharacter preview = CharacterFactory.Create(d); preview.P = new(r.X + 64, r.Y + 50);
            DrawBall(g, preview, 0, d.K == Kind.Juggernaut ? 0.38f : 0.75f);
            Txt(g, d.Name, 9, r.X + 64, r.Bottom - 12, true); int cnt = pick.Count(x => x == d); if (cnt > 0) Txt(g, "x" + cnt, 14, r.Right - 24, r.Y + 20, true, Color.OrangeRed);
        }
        if (view != null)
        {
            int idx = Array.IndexOf(Roster.All, view); var hov = Enumerable.Range(0, Roster.All.Length).Where(i => CardRect(i).Contains(mouse)).ToList();
            if (hov.Count > 0) view = Roster.All[hov[0]];
            var box = new RectangleF(ClientSize.Width / 2 - 330, 285, 660, 160); Pix.Panel(g, new Rectangle((int)box.X - 12, (int)box.Y - 9, (int)box.Width + 24, (int)box.Height + 12)); using var f = new Font("Consolas", 11); using var fb = new Font("Consolas", 12, FontStyle.Bold); using var br = new SolidBrush(Ink);
            g.DrawString($"{view.Name}   -   HP {view.HP}   -   Size {(view.R > 30 ? "2x" : "1x")}", fb, br, box.X, box.Y);
            g.DrawString("Passive: " + view.Passive, f, br, new RectangleF(box.X, box.Y + 30, box.Width, 60));
            g.DrawString("Ultimate: " + view.Ult, f, br, new RectangleF(box.X, box.Y + 95, box.Width, 60));
        }
        Pix.Panel(g, new Rectangle(ClientSize.Width / 2 - 170, 444, 340, 80)); Txt(g, $"Selected {pick.Count}/5  (click a chip to remove, min 2)", 10, ClientSize.Width / 2, 458);
        for (int i = 0; i < pick.Count; i++)
        {
            var c = ChipRect(i); using var cb = new SolidBrush(pick[i].C); using var cp = new Pen(Ink, 2);
            g.FillEllipse(cb, c); g.DrawEllipse(cp, c);
        }
        Btn(g, bBack, "Back", true); Btn(g, bStart, "Start Match", pick.Count >= 2);
    }

    void DrawResults(Graphics g)
    {
        int cx = ClientSize.Width / 2, oy = ClientSize.Height / 2 - 300; float t = (float)sw.Elapsed.TotalSeconds;
        Txt(g, draw ? "DRAW" : "VICTORY", 30, cx, oy + 34, true);
        if (!draw) { BallCharacter champion = CharacterFactory.Create(winner); champion.HP = 1; champion.P = new(cx, oy + 96); DrawBall(g, champion, 0, winner.K == Kind.Juggernaut ? 0.5f : 0.85f); Txt(g, winner.Name + " wins!", 16, cx, oy + 150, true); }
        Txt(g, $"Match time: {matchTime:0.0}s", 10, cx, oy + 174);
        int rows = results.Count, rh = 26, top = oy + 190, px = cx - 300, y0 = top + 24 + rh / 2 - 6;
        Pix.Panel(g, new Rectangle(px, top, 600, 24 + rh * (rows + 1) + 6));
        Txt(g, "BALL", 10, px + 140, y0, true, Pix.Gold); Txt(g, "DAMAGE", 10, px + 330, y0, true, Pix.Gold); Txt(g, "KILLS", 10, px + 430, y0, true, Pix.Gold); Txt(g, "RESULT", 10, px + 520, y0, true, Pix.Gold);
        for (int i = 0; i < rows; i++)   // one row per ball: total damage dealt and kills
        {
            var r = results[i]; int y = y0 + rh * (i + 1);
            if (r.Won) Pix.Panel(g, new Rectangle(px + 10, y - rh / 2 - 1, 580, rh), true, t);
            using (var cb = new SolidBrush(r.D.C)) g.FillRectangle(cb, px + 20, y - 6, 12, 12);
            Txt(g, r.Name, 10, px + 140, y, r.Won); Txt(g, r.Damage.ToString("0.0"), 10, px + 330, y, r.Won); Txt(g, r.Kills.ToString(), 10, px + 430, y, r.Won);
            Txt(g, r.Won ? "WINNER" : "OUT", 10, px + 520, y, true, r.Won ? Pix.Gold : Color.FromArgb(220, 130, 130));
        }
        Btn(g, bAgain, "Pick New Characters", true); Btn(g, bMenu, "Main Menu", true);
    }

    void DrawWorld(Graphics g, GameEngine s, bool hud)
    {
        float t = s.CineT; curCine = t; int mode = 0;
        // the old full-arena black/white inversion is replaced by ImpactFx's localized panels (no strobing)
        Color bgc = mode == 1 ? Color.White : mode == 2 ? Color.Black : Theme; var ar = hud ? Arena() : ClientRectangle;
        // background and arena floor come from Pix.Scene
        g.SetClip(ar); g.TranslateTransform(ar.X + (hud ? vfx.ShakeX : 0), ar.Y + (hud ? vfx.ShakeY : 0));
        float z = 1; var sh = PointF.Empty;
        if (t >= .36f && t < .61f) { z = 1 + 0.5f * MathF.Sin(MathF.PI * (t - .36f) / .25f); sh = new(rng.Next(-4, 5) * Settings.Shake, rng.Next(-4, 5) * Settings.Shake); }   // punch-in during the burst
        g.TranslateTransform(s.CineP.X + sh.X, s.CineP.Y + sh.Y); g.ScaleTransform(z, z); g.TranslateTransform(-s.CineP.X, -s.CineP.Y);
        if (mode == 0) { CharArt.World(g, s, (float)sw.Elapsed.TotalSeconds); vfx.DrawBack(g); }
        foreach (var b in s.Balls.Where(b => b.Alive)) DrawBall(g, b, mode, 1);
        if (mode == 0) vfx.DrawFront(g);
        impact.Draw(g, new RectangleF(0, 0, s.W, s.H), (float)sw.Elapsed.TotalSeconds, t >= 0);
        
        g.ResetTransform();
        vfx.DrawFlash(g, ar); g.ResetClip(); if (hud) DrawHud(g, s);
    }

    void DrawBurst(Graphics g, PointF p, float k)
    {
        using var br = new SolidBrush(Color.FromArgb((int)(255 * (1 - k * 0.6f)), 20, 20, 20)); float len = 60 + 260 * k;
        for (int i = 0; i < 28; i++)
        {
            double a = i * Math.PI * 2 / 28, w = 0.06; float l = len * (i % 2 == 0 ? 1 : 0.6f);
            g.FillPolygon(br, new PointF[] { new(p.X + (float)Math.Cos(a) * 30, p.Y + (float)Math.Sin(a) * 30), new(p.X + (float)Math.Cos(a - w) * l, p.Y + (float)Math.Sin(a - w) * l), new(p.X + (float)Math.Cos(a + w) * l, p.Y + (float)Math.Sin(a + w) * l) });
        }
    }

    // Placeholder procedural art. Swap for AI-generated sprites later (see Assets plan); logic does not depend on it.
    void DrawBall(Graphics g, BallCharacter b, int mode, float scale)
    {
        float r = b.R * scale; var rc = new RectangleF(b.P.X - r, b.P.Y - r, 2 * r, 2 * r);
        if (mode > 0) { g.FillEllipse(mode == 1 ? Brushes.Black : Brushes.White, rc); }
        else
        {
            for (int i = 0; i < b.Trail.Count; i++) { using var tb = new SolidBrush(Color.FromArgb(10 + i * 6, Color.DeepSkyBlue)); float tr = r * i / b.Trail.Count; g.FillEllipse(tb, b.Trail[i].X - tr, b.Trail[i].Y - tr, 2 * tr, 2 * tr); }
            vfx.DrawAura(g, b);
            Pix.Ball(g, b.P, r, b.D.C);
            if (b.Shock > 0) { using var sp = new Pen(Color.Cyan, 2); var q = new Random((int)(b.Shock * 20)); for (int i = 0; i < 6; i++) { double a = q.NextDouble() * 6.28; g.DrawLine(sp, b.P.X + (float)Math.Cos(a) * r, b.P.Y + (float)Math.Sin(a) * r, b.P.X + (float)Math.Cos(a + .3) * (r + 14), b.P.Y + (float)Math.Sin(a + .3) * (r + 14)); } }
        }
        if (mode == 0) { float tt = (float)sw.Elapsed.TotalSeconds; CharArt.Status(g, b, tt); if (b.D.K >= Kind.Inferno) CharArt.Body(g, b, tt); }
        if (b.D.K == Kind.Sword)   // weapon is its own component (Sword.cs), drawn after the ball so it never hides behind it
        {
            float t = (float)sw.Elapsed.TotalSeconds, rest = SwordArt.Rest(b, t), sc = r / 28f, ang = rest; bool slashing = b.SlashT >= 0;
            if (slashing && curCine >= 0) { ang = rest - 0.9f + 0.03f * MathF.Sin(t * 90); sc *= 1.3f; }   // ultimate freeze: sword cocked back, enlarged
            else if (slashing) { float p = b.SlashT / 0.25f; ang = SwordArt.SlashAngle(rest, b.SlashT); }   // wind-up, then fast 360 swing
            if (slashing && mode == 0 && curCine < 0) { SwordArt.DrawGhost(g, b.P, sc, ang - 0.3f, 90); SwordArt.DrawGhost(g, b.P, sc, ang - 0.6f, 45); vfx.DrawSlash(g, b, ang, SwordArt.Reach(r) * .85f); }
            SwordArt.Draw(g, b.P, sc, ang, mode);
        }
    }

    void DrawHud(Graphics g, GameEngine s)
    {
        var ar = Arena(); int pw = 200, ph = 150, left = (s.Balls.Count + 1) / 2;
        Txt(g, $"Speed x{s.SpeedMul:0.0}    Time {s.Time:0}s", 10, ar.X + ar.Width / 2, ar.Y - 22, true);
        for (int i = 0; i < s.Balls.Count; i++)
        {
            var b = s.Balls[i]; bool isL = i < left; int k = isL ? i : i - left;
            int x = isL ? Math.Max(10, ar.X - pw - 20) : ar.Right + 20, y = 10 + k * ph;
            float dmg = b.Passive * (b.D.K == Kind.Lightning && b.UltT > 0 ? 2 : 1), pct; string ult;
            if (b.D.K == Kind.Lightning) { if (b.UltT > 0) { pct = 100; ult = $"ACTIVE {b.UltT:0.0}s"; } else { pct = b.Charge; ult = $"{b.Charge:0}%"; } }
            else if (b.D.K == Kind.Sword) { pct = b.Slashes * 20; ult = $"{pct:0}% ({b.Slashes}/5)"; }
            else if (b.D.K >= Kind.Inferno) { pct = b.UltT > 0 ? 100 : b.Charge; ult = b.UltT > 0 ? $"ACTIVE {b.UltT:0.0}s" : $"{b.Charge:0}%"; dmg = Balance.BaseHit; }
            else { pct = 0; ult = "No ultimate"; }
            using var f = new Font("Consolas", 10, FontStyle.Bold); using var fs = new Font("Consolas", 9); using var ink = new SolidBrush(b.Alive ? Pix.Cream : Color.Gray);
            Pix.Panel(g, new Rectangle(x - 9, y - 6, pw + 18, 148)); g.DrawString(b.Label + (b.Alive ? "" : " - OUT"), f, ink, x, y);
            g.DrawString($"Health  {(b.HP > 0 && b.HP < 1 ? b.HP.ToString("0.00") : b.HP.ToString("0.0"))} / {b.D.HP}", fs, ink, x, y + 30); Bar(g, x, y + 52, pw, 14, b.HP / b.D.HP, Color.LimeGreen);
            g.DrawString("Ultimate  " + ult, fs, ink, x, y + 78); Bar(g, x, y + 100, pw, 14, pct / 100, b.D.K == Kind.Sword ? Color.Gray : b.D.C);
            g.DrawString($"Damage  {dmg:0.00}  {CharArt.Tags(b)}", fs, ink, x, y + 126);
        }
    }

    static void Bar(Graphics g, int x, int y, int w, int h, float k, Color c) => Pix.Bar(g, x, y, w, h, k, c);
}
