using System.Diagnostics;
using System.Drawing.Drawing2D;
using Timer = System.Windows.Forms.Timer;

namespace BallBrawl;

// ============================ LOGIC (no rendering) ============================
enum Kind { Lightning, Sword, Juggernaut, Inferno, Frost, Void, Venom, Solar, Mecha, Cosmic, Tempest }
record Def(Kind K, string Name, Color C, int HP, float R, string Passive, string Ult);

static class Roster
{
    public static readonly Def[] All =
    {
        new(Kind.Lightning, "Lightning Ball", Color.FromArgb(40, 120, 255), 100, 28,
            "Electric Shock: every collision shocks the target. Damage starts at 0.1 and grows +0.3 per collision.",
            "Overcharge: 11 charge per collision. At 100, moves much faster and deals 2x damage for 4s (+1s each use)."),
        new(Kind.Sword, "Anime Sword Ball", Color.FromArgb(235, 235, 245), 100, 28,
            "Blade Counter: 360-degree slash on every collision (attack or defense). Damage starts at 0.1, +0.4 per collision.",
            "Anime Finisher: after 5 slashes, the next slash deals 5x damage with a cinematic sequence."),
        new(Kind.Juggernaut, "Juggernaut", Color.FromArgb(120, 50, 190), 300, 56,
            "None. 300 HP and 2x size. Collision damage starts at 0.1 and grows +0.1 per collision.",
            "None."),
            new(Kind.Inferno, "Inferno Ball", Color.FromArgb(255, 90, 30), 100, 28, "Burning Impact: hits burn the target for 0.5 dmg/s for 3s (refreshes, no stacking).", "Supernova: fiery explosion deals 12 damage in a large radius and applies a stronger burn for 3s."),
            new(Kind.Frost, "Frost Ball", Color.FromArgb(170, 220, 255), 120, 28, "Frostbite: each hit slows the target 5% (max 30%), lasting 2s after the latest hit.", "Absolute Zero: freezes the target for 1.5s, then deals 10 damage and releases ice shards."),
            new(Kind.Void, "Void Ball", Color.FromArgb(40, 10, 60), 90, 28, "Void Drain: heals for 20% of the damage it actually deals (up to max HP).", "Black Hole: a 3s gravity field pulls opponents in and deals 2 damage every 0.5s inside it."),
            new(Kind.Venom, "Venom Ball", Color.FromArgb(110, 220, 40), 110, 28, "Corrosion: each hit adds a poison stack (max 5); each stack deals 0.2 dmg/s for 3s (refreshes).", "Toxic Bloom: a 4s poison cloud deals 1 damage per second to opponents inside."),
            new(Kind.Solar, "Solar Ball", Color.FromArgb(255, 200, 40), 100, 28, "Solar Charge: +10 energy per hit; at 100 the next hit releases a burst for 5 extra damage nearby.", "Solar Flare: a radiant explosion deals 15 damage within its radius."),
            new(Kind.Mecha, "Mecha Ball", Color.FromArgb(150, 160, 175), 150, 28, "Adaptive Armor: after every 3 hits received, a shield cuts incoming collision damage 30% for 2s.", "Overdrive: for 4s, +40% movement speed and +50% outgoing collision damage."),
            new(Kind.Cosmic, "Cosmic Ball", Color.FromArgb(170, 70, 200), 100, 28, "Cosmic Bounce: every 3rd hit grants +25% movement speed for 1.5s.", "Meteor Shower: for 3s, five meteors strike near the opponent, 4 damage each in their impact radius."),
            new(Kind.Tempest, "Tempest Ball", Color.FromArgb(200, 245, 255), 85, 28, "Tailwind: +5% speed per hit (max 30%), lasting 2s after the latest hit.", "Cyclone: for 3s, +50% movement speed and +2 damage on collision hits."),
        };
}

class Ball
{
    public Def D; public PointF P, V; public float HP, Passive = 0.1f, Charge, UltT, Glow, Shock, SlashT = -1, Cd;
    public float BurnT, BurnDps, PoisonT, SlowT, FreezeT, ShieldT, BoostT, TailT, Energy; public int Poison, SlowStacks, TailStacks, Hits, Received; public bool Shatter;   // status effects of the newer characters
    public int UltCount, Slashes; public string Label; public List<PointF> Trail = new();
    public float R => D.R; public bool Alive => HP > 0;
}

partial class Sim
{
    public List<Ball> Balls = new(); public List<GameEvent> Events = new(); public bool Infinite;
    public float W = 900, H = 600, Hit, CineT = -1, Time; public PointF CineP; static Random Rng = new();
    Dictionary<int, float> Pcd = new();
    public float SpeedMul => Infinite ? 1 : Math.Min(4f, 1 + 0.1f * MathF.Floor(Time / 2.5f)); // +0.1 every 2.5s, capped at 4x
    static float Dist(PointF p, PointF q) => MathF.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y));
    public Sim(Def[] defs, bool infinite, float w, float h)
    {
        Infinite = infinite; W = w; H = h;
        foreach (var d in defs)
        {
            double a = Rng.NextDouble() * Math.PI * 2; int total = defs.Count(x => x == d), n = Balls.Count(x => x.D == d) + 1;
            var b = new Ball { D = d, HP = d.HP, Label = total > 1 ? $"{d.Name} #{n}" : d.Name, V = new((float)Math.Cos(a), (float)Math.Sin(a)) };
            for (int t = 0; t < 60; t++)
            {
                b.P = new(b.R + (float)Rng.NextDouble() * Math.Max(1, W - 2 * b.R), b.R + (float)Rng.NextDouble() * Math.Max(1, H - 2 * b.R));
                if (Balls.All(o => Dist(o.P, b.P) > o.R + b.R + 8)) break;
            }
            Balls.Add(b);
        }
    }
    public Ball Winner => Balls.Count(b => b.Alive) == 1 ? Balls.First(b => b.Alive) : null;
    public bool Over => Balls.Count(b => b.Alive) <= 1;

    public void Step(float dt)
    {
        if (CineT >= 0) { CineT += dt; if (CineT > 0.65f) CineT = -1; return; } // cinematic freezes sim
        if (Hit > 0) { Hit -= dt; return; }                                       // hit-stop
        Time += dt; foreach (var k in Pcd.Keys.ToList()) Pcd[k] -= dt;
        
        foreach (var b in Balls.Where(b => b.Alive))
        {
            float sp = Math.Min(280 * SpeedMul * Speed(b) * (b.D.K == Kind.Lightning && b.UltT > 0 ? 4.5f : 1f), 1500);
            float m = MathF.Sqrt(b.V.X * b.V.X + b.V.Y * b.V.Y); b.V = new(b.V.X / m, b.V.Y / m);
            b.P = new(b.P.X + b.V.X * sp * dt, b.P.Y + b.V.Y * sp * dt);
            if (b.P.X < b.R) { b.P.X = b.R; b.V.X = MathF.Abs(b.V.X); }
            if (b.P.X > W - b.R) { b.P.X = W - b.R; b.V.X = -MathF.Abs(b.V.X); }
            if (b.P.Y < b.R) { b.P.Y = b.R; b.V.Y = MathF.Abs(b.V.Y); }
            if (b.P.Y > H - b.R) { b.P.Y = H - b.R; b.V.Y = -MathF.Abs(b.V.Y); }
            b.Cd -= dt; b.Glow -= dt; b.Shock -= dt; TickStatus(b, dt);
            if (b.SlashT >= 0) { b.SlashT += dt; if (b.SlashT > 0.25f) b.SlashT = -1; }
            if (b.UltT > 0) { b.UltT -= dt; if (b.UltT <= 0) Events.Add(new GameEvent(2, b.D.K, b.P, new(1, 0), false, 1)); }
            if (b.D.K == Kind.Lightning && b.UltT > 0) { b.Trail.Add(b.P); if (b.Trail.Count > 24) b.Trail.RemoveAt(0); }
            else if (b.Trail.Count > 0) b.Trail.RemoveAt(0);
            if (Infinite && b.HP < b.D.HP * 0.25f) b.HP = b.D.HP;   // decorative battle never ends
        }
        TickWorld(dt);
        for (int i = 0; i < Balls.Count; i++) for (int j = i + 1; j < Balls.Count; j++)
        {
            Ball a = Balls[i], b = Balls[j]; if (!a.Alive || !b.Alive) continue;
            float dx = b.P.X - a.P.X, dy = b.P.Y - a.P.Y, d = MathF.Sqrt(dx * dx + dy * dy), min = a.R + b.R;
            if (d >= min || d < 0.001f) continue;
            float nx = dx / d, ny = dy / d, ov = min - d;
            a.P = new(a.P.X - nx * ov / 2, a.P.Y - ny * ov / 2); b.P = new(b.P.X + nx * ov / 2, b.P.Y + ny * ov / 2);
            float ma = a.R * a.R, mb = b.R * b.R;
            float va = a.V.X * nx + a.V.Y * ny, vb = b.V.X * nx + b.V.Y * ny;
            if (va - vb > 0)   // approaching: elastic response along normal
            {
                float na = (va * (ma - mb) + 2 * mb * vb) / (ma + mb), nb = (vb * (mb - ma) + 2 * ma * va) / (ma + mb);
                a.V = new(a.V.X + (na - va) * nx, a.V.Y + (na - va) * ny); b.V = new(b.V.X + (nb - vb) * nx, b.V.Y + (nb - vb) * ny);
            }
            int key = i * 10 + j; if (Pcd.TryGetValue(key, out var pc) && pc > 0) continue; Pcd[key] = 0.25f;
            Strike(a, b, new(a.P.X + nx * a.R, a.P.Y + ny * a.R)); Strike(b, a, new(b.P.X - nx * b.R, b.P.Y - ny * b.R));
        }
    }

    void Hitfx(Ball a, Ball b, PointF cp, bool ult)
    { float dx = b.P.X - a.P.X, dy = b.P.Y - a.P.Y, d = MathF.Max(0.001f, MathF.Sqrt(dx * dx + dy * dy)); Events.Add(new GameEvent(0, a.D.K, cp, new(dx / d, dy / d), ult, a.R / 28f, a, b)); }

    void Strike(Ball a, Ball b, PointF cp)  // a deals damage to b exactly once per collision
    {
        float dmg; bool ult = false;
        switch (a.D.K)
        {
            case Kind.Lightning:
                dmg = a.Passive * (a.UltT > 0 ? 2 : 1); a.Passive += 0.3f; a.Glow = 0.3f; b.Shock = 1f;
                if (a.UltT <= 0 && (a.Charge += 11) >= 100) { a.Charge = 0; a.UltT = 4 + a.UltCount++; Events.Add(new GameEvent(1, Kind.Lightning, a.P, new(1, 0), true, 1)); }
                Hitfx(a, b, cp, a.UltT > 0);
                break;
            case Kind.Sword:
                if (a.Slashes >= 5) { ult = true; dmg = 5 * a.Passive; a.Slashes = 0; } else { dmg = a.Passive; a.Slashes++; }
                a.Passive += 0.4f; a.SlashT = 0;
                Hitfx(a, b, cp, ult);
                if (ult) { CineT = 0; CineP = cp; } else Hit = 0.05f;
                break;
            case Kind.Juggernaut:
                dmg = a.Passive; a.Passive += 0.1f;
                Hitfx(a, b, cp, false);
                break;
            default:   // newer characters: flat base hit, effects in Abilities.cs
                dmg = Balance.BaseHit; Hitfx(a, b, cp, false);
                break;
        }
        dmg = Incoming(a, b, dmg); float before = b.HP; b.HP = Math.Max(0, b.HP - dmg); AfterHit(a, b, before - b.HP, cp);
    }
}

// ============================ UI / RENDERING ============================
enum State { Menu, Select, Match, Results }

class Game : Form
{
    static readonly Color Theme = Color.FromArgb(0xED, 0xE8, 0xD0), Ink = Color.FromArgb(60, 50, 40);
    Vfx vfx = new(); ImpactFx impact = new(); float prevCine = -1, curCine = -1, toast;
    State st; Sim bg, match; Timer tm = new() { Interval = 15 }; Stopwatch sw = Stopwatch.StartNew(); double lastT;
    float fade; Point mouse; List<Def> pick = new(); Def view; Def winner; bool draw; float matchTime, endDelay; Random rng = new();
    Rectangle bPlay, bExit, bBack, bStart, bAgain, bMenu;

    public Game()
    {
        Text = "Ball Brawl"; ClientSize = new(900, 600); MinimumSize = new(700, 520); DoubleBuffered = true; BackColor = Theme;
        tm.Tick += (_, _) => Tick(); tm.Start();
        Audio.Init(Path.Combine(AppContext.BaseDirectory, "Assets", "Audio")); FormClosed += (_, _) => { Audio.Shutdown(); impact.Dispose(); }; KeyDown += OnKey;
        MouseMove += (_, e) => mouse = e.Location; MouseDown += (_, e) => Click(e.Location); Resize += (_, _) => Layout();
        Go(State.Menu);
    }

    Rectangle Arena() { int w = Math.Max(240, ClientSize.Width - 500), h = Math.Min(ClientSize.Height - 100, 400); return new((ClientSize.Width - w) / 2, (ClientSize.Height - h) / 2, w, h); }

    void OnKey(object o, KeyEventArgs e)   // Up/Down = master volume, M = mute
    {
        if (e.KeyCode == Keys.Up) Audio.Master = Math.Min(1, Audio.Master + .1f); else if (e.KeyCode == Keys.Down) Audio.Master = Math.Max(0, Audio.Master - .1f);
        else if (e.KeyCode == Keys.M) Audio.Master = Audio.Master > 0 ? 0 : .8f;
        else if (e.KeyCode == Keys.F) Settings.ReduceFlash = !Settings.ReduceFlash; else if (e.KeyCode == Keys.G) Settings.Shake = Settings.Shake > .6f ? .5f : Settings.Shake > .1f ? 0f : 1f; else return; toast = 1.5f;
    }

    void Layout()
    {
        int cx = ClientSize.Width / 2, h = ClientSize.Height;
        bPlay = new(cx - 100, h / 2 - 10, 200, 56); bExit = new(cx - 100, h / 2 + 62, 200, 56);
        bBack = new(20, h - 70, 120, 48); bStart = new(ClientSize.Width - 240, h - 70, 220, 48);
        bAgain = new(cx - 150, h / 2 + 90, 300, 52); bMenu = new(cx - 150, h / 2 + 156, 300, 52);
        if (bg != null) { bg.W = ClientSize.Width; bg.H = ClientSize.Height; } if (match != null) { var a = Arena(); match.W = a.Width; match.H = a.Height; }
    }

    void Go(State s)
    {
        st = s; fade = 1; Layout(); vfx.Clear(); impact.Clear(); prevCine = -1; curCine = -1;
        if (s != State.Menu) bg = null;                                  // stop background battle
        if (s == State.Menu) { match = null; bg = NewBg(); }
        if (s == State.Select) { match = null; pick.Clear(); view = Roster.All[0]; }
    }

    Sim NewBg()
    {
        var all = Roster.All.OrderBy(_ => rng.Next()).ToArray();
        return new Sim(all.Length >= 2 ? all.Take(2).ToArray() : all, true, ClientSize.Width, ClientSize.Height);
    }

    void Click(Point p)
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
        var ar = Arena(); match = new Sim(pick.ToArray(), false, ar.Width, ar.Height); matchTime = 0; endDelay = 0; Audio.Play("match_start", 1f); vfx.Clear(); impact.Clear(); st = State.Match; fade = 1;
    }

    bool Pump(Sim s, float dt)   // drains gameplay events into the renderer's effect system
    {
        Audio.Scale = ReferenceEquals(s, bg) ? .25f : 1f; foreach (var e in s.Events) { vfx.Handle(e); Audio.OnEvent(e); if (e.T == 0 && e.K == Kind.Sword) impact.Start(e, (float)sw.Elapsed.TotalSeconds); } s.Events.Clear(); if (s.CineT >= 0) Audio.Cine(prevCine, s.CineT);
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
                Audio.Play("match_end", 1f); winner = match.Winner?.D; draw = winner == null; match = null; st = State.Results; fade = 1; Layout();
            }
        }
        Invalidate();
    }

    Rectangle ChipRect(int i) => new((ClientSize.Width - 5 * 48) / 2 + i * 48, 475, 40, 40);
    Rectangle CardRect(int i) { int cols = 6, w = 128, h = 98, gap = 10, x0 = (ClientSize.Width - (cols * (w + gap) - gap)) / 2; return new(x0 + i % cols * (w + gap), 68 + i / cols * (h + gap), w, h); }

    // ---- drawing ----
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; g.Clear(Theme);
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

    void Txt(Graphics g, string s, float size, float x, float y, bool bold = false, Color? c = null)
    {
        using var f = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
        using var br = new SolidBrush(c ?? Ink); var sz = g.MeasureString(s, f); g.DrawString(s, f, br, x - sz.Width / 2, y - sz.Height / 2);
    }

    void Btn(Graphics g, Rectangle r, string t, bool on)
    {
        bool hov = on && r.Contains(mouse);
        using var br = new SolidBrush(!on ? Color.FromArgb(190, 185, 170) : hov ? Color.FromArgb(90, 75, 60) : Ink);
        using var path = Round(r, 14); g.FillPath(br, path);
        Txt(g, t, 16, r.X + r.Width / 2, r.Y + r.Height / 2, true, Theme);
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
            using (var br = new SolidBrush(Color.FromArgb(sel ? 255 : 200, 250, 247, 235))) g.FillRectangle(br, r);
            using (var pen = new Pen(sel ? Color.OrangeRed : Ink, sel ? 4 : 1)) g.DrawRectangle(pen, r);
            DrawBall(g, new Ball { D = d, HP = d.HP, P = new(r.X + 64, r.Y + 50) }, 0, d.K == Kind.Juggernaut ? 0.38f : 0.75f);
            Txt(g, d.Name, 9, r.X + 64, r.Bottom - 12, true); int cnt = pick.Count(x => x == d); if (cnt > 0) Txt(g, "x" + cnt, 14, r.Right - 24, r.Y + 20, true, Color.OrangeRed);
        }
        if (view != null)
        {
            int idx = Array.IndexOf(Roster.All, view); var hov = Enumerable.Range(0, Roster.All.Length).Where(i => CardRect(i).Contains(mouse)).ToList();
            if (hov.Count > 0) view = Roster.All[hov[0]];
            var box = new RectangleF(ClientSize.Width / 2 - 330, 285, 660, 160); using var f = new Font("Segoe UI", 11); using var fb = new Font("Segoe UI", 12, FontStyle.Bold); using var br = new SolidBrush(Ink);
            g.DrawString($"{view.Name}   -   HP {view.HP}   -   Size {(view.R > 30 ? "2x" : "1x")}", fb, br, box.X, box.Y);
            g.DrawString("Passive: " + view.Passive, f, br, new RectangleF(box.X, box.Y + 30, box.Width, 60));
            g.DrawString("Ultimate: " + view.Ult, f, br, new RectangleF(box.X, box.Y + 95, box.Width, 60));
        }
        Txt(g, $"Selected {pick.Count}/5  (click a chip to remove, min 2)", 10, ClientSize.Width / 2, 458);
        for (int i = 0; i < pick.Count; i++)
        {
            var c = ChipRect(i); using var cb = new SolidBrush(pick[i].C); using var cp = new Pen(Ink, 2);
            g.FillEllipse(cb, c); g.DrawEllipse(cp, c);
        }
        Btn(g, bBack, "Back", true); Btn(g, bStart, "Start Match", pick.Count >= 2);
    }

    void DrawResults(Graphics g)
    {
        int cx = ClientSize.Width / 2, cy = ClientSize.Height / 2;
        Txt(g, draw ? "DRAW" : "VICTORY", 44, cx, cy - 170, true);
        if (!draw) { DrawBall(g, new Ball { D = winner, HP = 1, P = new(cx, cy - 80) }, 0, 1.2f); Txt(g, winner.Name + " wins!", 20, cx, cy + 10, true); }
        Txt(g, $"Match time: {matchTime:0.0}s", 12, cx, cy + 50);
        Btn(g, bAgain, "Pick New Characters", true); Btn(g, bMenu, "Main Menu", true);
    }

    void DrawWorld(Graphics g, Sim s, bool hud)
    {
        float t = s.CineT; curCine = t; int mode = 0;
        // the old full-arena black/white inversion is replaced by ImpactFx's localized panels (no strobing)
        Color bgc = mode == 1 ? Color.White : mode == 2 ? Color.Black : Theme; var ar = hud ? Arena() : ClientRectangle;
        if (hud) { using var fb = new SolidBrush(bgc); g.FillRectangle(fb, ar); } else g.Clear(bgc);
        g.SetClip(ar); g.TranslateTransform(ar.X + (hud ? vfx.ShakeX : 0), ar.Y + (hud ? vfx.ShakeY : 0));
        float z = 1; var sh = PointF.Empty;
        if (t >= .36f && t < .61f) { z = 1 + 0.5f * MathF.Sin(MathF.PI * (t - .36f) / .25f); sh = new(rng.Next(-4, 5) * Settings.Shake, rng.Next(-4, 5) * Settings.Shake); }   // punch-in during the burst
        g.TranslateTransform(s.CineP.X + sh.X, s.CineP.Y + sh.Y); g.ScaleTransform(z, z); g.TranslateTransform(-s.CineP.X, -s.CineP.Y);
        if (mode == 0) { CharArt.World(g, s, (float)sw.Elapsed.TotalSeconds); vfx.DrawBack(g); }
        foreach (var b in s.Balls.Where(b => b.Alive)) DrawBall(g, b, mode, 1);
        if (mode == 0) vfx.DrawFront(g);
        impact.Draw(g, new RectangleF(0, 0, s.W, s.H), (float)sw.Elapsed.TotalSeconds, t >= 0);
        
        g.ResetTransform();
        vfx.DrawFlash(g, ar); g.ResetClip(); if (hud) { using var bp = new Pen(Ink, 4); g.DrawRectangle(bp, ar); DrawHud(g, s); }
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
    void DrawBall(Graphics g, Ball b, int mode, float scale)
    {
        float r = b.R * scale; var rc = new RectangleF(b.P.X - r, b.P.Y - r, 2 * r, 2 * r);
        if (mode > 0) { g.FillEllipse(mode == 1 ? Brushes.Black : Brushes.White, rc); }
        else
        {
            for (int i = 0; i < b.Trail.Count; i++) { using var tb = new SolidBrush(Color.FromArgb(10 + i * 6, Color.DeepSkyBlue)); float tr = r * i / b.Trail.Count; g.FillEllipse(tb, b.Trail[i].X - tr, b.Trail[i].Y - tr, 2 * tr, 2 * tr); }
            vfx.DrawAura(g, b);
            using var path = new GraphicsPath(); path.AddEllipse(rc);
            using var pg = new PathGradientBrush(path) { CenterColor = ControlPaint.Light(b.D.C), SurroundColors = new[] { b.D.C }, CenterPoint = new(b.P.X - r / 3, b.P.Y - r / 3) };
            g.FillPath(pg, path); using var op = new Pen(Color.FromArgb(70, 60, 50), 2); g.DrawEllipse(op, rc);
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

    void DrawHud(Graphics g, Sim s)
    {
        var ar = Arena(); int pw = 200, ph = 150, left = (s.Balls.Count + 1) / 2;
        using (var fh = new Font("Segoe UI", 10, FontStyle.Bold)) g.DrawString($"Speed x{s.SpeedMul:0.0}    Time {s.Time:0}s", fh, new SolidBrush(Ink), ar.X, ar.Y - 24);
        for (int i = 0; i < s.Balls.Count; i++)
        {
            var b = s.Balls[i]; bool isL = i < left; int k = isL ? i : i - left;
            int x = isL ? Math.Max(10, ar.X - pw - 20) : ar.Right + 20, y = 10 + k * ph;
            float dmg = b.Passive * (b.D.K == Kind.Lightning && b.UltT > 0 ? 2 : 1), pct; string ult;
            if (b.D.K == Kind.Lightning) { if (b.UltT > 0) { pct = 100; ult = $"ACTIVE {b.UltT:0.0}s"; } else { pct = b.Charge; ult = $"{b.Charge:0}%"; } }
            else if (b.D.K == Kind.Sword) { pct = b.Slashes * 20; ult = $"{pct:0}% ({b.Slashes}/5)"; }
            else if (b.D.K >= Kind.Inferno) { pct = b.UltT > 0 ? 100 : b.Charge; ult = b.UltT > 0 ? $"ACTIVE {b.UltT:0.0}s" : $"{b.Charge:0}%"; dmg = Balance.BaseHit; }
            else { pct = 0; ult = "No ultimate"; }
            using var f = new Font("Segoe UI", 11, FontStyle.Bold); using var fs = new Font("Segoe UI", 10); using var ink = new SolidBrush(b.Alive ? Ink : Color.Gray);
            g.DrawString(b.Label + (b.Alive ? "" : " - OUT"), f, ink, x, y);
            g.DrawString($"Health  {b.HP:0.0} / {b.D.HP}", fs, ink, x, y + 30); Bar(g, x, y + 52, pw, 14, b.HP / b.D.HP, Color.LimeGreen);
            g.DrawString("Ultimate  " + ult, fs, ink, x, y + 78); Bar(g, x, y + 100, pw, 14, pct / 100, b.D.K == Kind.Sword ? Color.Gray : b.D.C);
            g.DrawString($"Damage  {dmg:0.00}  {CharArt.Tags(b)}", fs, ink, x, y + 126);
        }
    }

    static void Bar(Graphics g, int x, int y, int w, int h, float k, Color c)
    { using var back = new SolidBrush(Color.FromArgb(70, 60, 50, 40)); using var fill = new SolidBrush(c); g.FillRectangle(back, x, y, w, h); g.FillRectangle(fill, x, y, w * Math.Clamp(k, 0, 1), h); }
}

static class Program
{
    [STAThread] static void Main() { ApplicationConfiguration.Initialize(); Application.Run(new Game()); }
}
