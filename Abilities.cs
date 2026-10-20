namespace BallBrawl;

class Zone { public Kind K; public PointF P; public float R, Age, Life, Tick; public Ball Owner; }          // black hole / poison cloud
class Meteor { public Ball Owner; public float Age, Start; public bool Started; public PointF Pos; }       // Cosmic Ball meteor

// Gameplay rules for the newer characters. Pure logic: it raises GameEvents and never draws or plays sound.
partial class Sim
{
    public List<Zone> Zones = new(); public List<Meteor> Meteors = new();
    static bool IsNew(Kind k) => k >= Kind.Inferno;
    void Ev(int t, Kind k, PointF p, float scale = 1, bool ult = false) => Events.Add(new GameEvent(t, k, p, new(1, 0), ult, scale));
    void Dot(Ball o, float d) => ApplyDamage(o, d);

    // The single place health is reduced. Returns the HP actually removed (never more than the target had), so lifesteal can't exceed real damage.
    // Dead targets take nothing. Float residue (e.g. 0.3-0.1-0.1-0.1 = 3e-8) is snapped to exactly 0 so lethal damage always kills.
    float ApplyDamage(Ball o, float d)
    {
        if (d <= 0 || !o.Alive) return 0;
        float before = o.HP, after = before - d; if (after <= 1e-5f) after = 0;
        o.HP = after; return before - after;
    }

    // Lifesteal is applied after BOTH sides of a collision have dealt damage, and only to a ball that is still alive,
    // so a ball killed in a collision can never be healed back to life by its own hit in that same collision.
    void Settle(Ball o)
    {
        if (o.PendingHeal > 0 && o.Alive) o.HP = Math.Min(o.D.HP, o.HP + o.PendingHeal);
        o.PendingHeal = 0;
    }

    // movement multiplier from every active status effect (freeze = 0)
    public float Speed(Ball b)
    {
        if (b.FreezeT > 0) return 0;
        float m = 1 - Math.Min(Balance.FrostSlowMax, b.SlowStacks * Balance.FrostSlowStep);
        if (b.BoostT > 0) m *= 1 + Balance.CosmicBoost;
        if (b.TailStacks > 0) m *= 1 + Math.Min(Balance.TailMax, b.TailStacks * Balance.TailStep);
        if (b.UltT > 0 && b.D.K == Kind.Tempest) m *= 1 + Balance.CycloneSpeed;
        if (b.UltT > 0 && b.D.K == Kind.Mecha) m *= 1 + Balance.OverSpeed;
        return m;
    }

    // collision-damage modifiers (applied once per collision, inside Strike)
    float Incoming(Ball a, Ball b, float dmg)
    {
        if (a.UltT > 0 && a.D.K == Kind.Mecha) dmg *= 1 + Balance.OverDmg;
        if (a.UltT > 0 && a.D.K == Kind.Tempest) dmg += Balance.CycloneDmg;
        if (b.ShieldT > 0 && b.D.K == Kind.Mecha) dmg *= 1 - Balance.MechaShieldCut;
        return dmg;
    }

    void Area(Ball owner, PointF c, float rad, float dmg, Action<Ball> also = null)
    { foreach (var o in Balls) { if (o == owner || !o.Alive || Dist(o.P, c) > rad + o.R) continue; Dot(o, dmg); also?.Invoke(o); } }

    // passives, receive-side effects and ultimate charging; `actual` = HP really removed
    void AfterHit(Ball a, Ball b, float actual, PointF cp)
    {
        a.Hits++;
        if (b.D.K == Kind.Mecha && ++b.Received % (int)Balance.MechaShieldEvery == 0) { b.ShieldT = Balance.MechaShieldTime; Ev(11, Kind.Mecha, b.P); }
        if (!IsNew(a.D.K)) return;
        switch (a.D.K)
        {
            case Kind.Inferno: b.BurnT = Balance.InfBurnTime; b.BurnDps = Math.Max(b.BurnDps, Balance.InfBurnDps); Ev(17, a.D.K, b.P); break;   // refresh, never stack
            case Kind.Frost: b.SlowStacks = Math.Min((int)(Balance.FrostSlowMax / Balance.FrostSlowStep + .5f), b.SlowStacks + 1); b.SlowT = Balance.FrostSlowTime; Ev(17, a.D.K, b.P); break;
            case Kind.Void: a.PendingHeal += actual * Balance.VoidDrain; break;   // 20% of damage actually dealt, settled after the collision
            case Kind.Venom: b.Poison = Math.Min((int)Balance.PoisonMax, b.Poison + 1); b.PoisonT = Balance.PoisonTime; Ev(17, a.D.K, b.P); break;
            case Kind.Solar:
                if (a.Energy >= 100) { a.Energy = 0; Area(a, cp, Balance.SolarBurstRadius, Balance.SolarBurstDmg); Ev(11, Kind.Solar, cp, Balance.SolarBurstRadius); }
                else a.Energy = Math.Min(100, a.Energy + Balance.SolarPerHit);
                break;
            case Kind.Cosmic: if (a.Hits % 3 == 0) { a.BoostT = Balance.CosmicBoostTime; Ev(11, Kind.Cosmic, a.P); } break;
            case Kind.Tempest: a.TailStacks = Math.Min((int)(Balance.TailMax / Balance.TailStep + .5f), a.TailStacks + 1); a.TailT = Balance.TailTime; Ev(11, Kind.Tempest, a.P); break;
        }
        if (a.Alive && a.UltT <= 0 && (a.Charge += Balance.UltPerHit) >= 100) { a.Charge = 0; Cast(a, b); }
    }

    void Cast(Ball a, Ball b)
    {
        float rad = a.D.K switch { Kind.Inferno => Balance.InfUltRadius, Kind.Solar => Balance.SolarFlareRadius, Kind.Void => Balance.VoidRadius, Kind.Venom => Balance.BloomRadius, Kind.Frost => 130f, Kind.Mecha => 90f, Kind.Cosmic => 110f, _ => 150f };
        Ev(10, a.D.K, a.D.K == Kind.Frost ? b.P : a.P, rad, true);
        switch (a.D.K)
        {
            case Kind.Inferno: Area(a, a.P, Balance.InfUltRadius, Balance.InfUltDmg, o => { o.BurnT = Balance.InfBurnTime; o.BurnDps = Math.Max(o.BurnDps, Balance.InfUltBurnDps); }); break;
            case Kind.Frost: b.FreezeT = Balance.FrostFreeze; b.Shatter = true; break;
            case Kind.Void: Zones.Add(new Zone { K = Kind.Void, P = a.P, R = Balance.VoidRadius, Life = Balance.VoidTime, Owner = a }); a.UltT = Balance.VoidTime; break;
            case Kind.Venom: Zones.Add(new Zone { K = Kind.Venom, P = a.P, R = Balance.BloomRadius, Life = Balance.BloomTime, Owner = a }); a.UltT = Balance.BloomTime; break;
            case Kind.Solar: Area(a, a.P, Balance.SolarFlareRadius, Balance.SolarFlareDmg); break;
            case Kind.Mecha: a.UltT = Balance.OverTime; break;
            case Kind.Cosmic:
                a.UltT = Balance.MeteorTime;
                for (int i = 0; i < (int)Balance.MeteorCount; i++) Meteors.Add(new Meteor { Owner = a, Start = .2f + i * (Balance.MeteorTime - Balance.MeteorFall - .2f) / (Balance.MeteorCount - 1) });
                break;
            case Kind.Tempest: a.UltT = Balance.CycloneTime; break;
        }
    }

    // Run with: dotnet run -- --selftest   (uses the real Strike/ApplyDamage/Settle code)
    internal static string SelfTest()
    {
        var log = new System.Text.StringBuilder(); int fail = 0;
        void Check(string name, bool ok) { if (!ok) fail++; log.AppendLine((ok ? "PASS  " : "FAIL  ") + name); }
        static bool Near(float a, float b) => MathF.Abs(a - b) < 1e-4f;
        Def D(Kind k) => Roster.All.First(d => d.K == k);
        var s = new Sim(new[] { D(Kind.Void), D(Kind.Lightning) }, false, 400, 400); var v = s.Balls[0]; var l = s.Balls[1]; var p = new PointF(200, 200);
        v.HP = 100; Check("1  100 HP takes damage", Near(s.ApplyDamage(v, 1f), 1f) && Near(v.HP, 99));
        v.HP = 1; s.ApplyDamage(v, .5f); Check("2  1 HP takes damage", Near(v.HP, .5f));
        v.HP = .3f; s.ApplyDamage(v, .1f); Check("3  0.3 -> ~0.2", Near(v.HP, .2f));
        s.ApplyDamage(v, .1f); Check("4  0.2 -> ~0.1", Near(v.HP, .1f) && v.Alive);
        s.ApplyDamage(v, .1f); Check("5a 0.1 - 0.1 reaches exactly 0", v.HP == 0 && !v.Alive);
        v.HP = .1f; float got = s.ApplyDamage(v, 5f); Check("5b lethal overkill -> 0, removes only 0.1", v.HP == 0 && Near(got, .1f));
        Check("6  dead ball takes nothing, stays dead", s.ApplyDamage(v, 1f) == 0 && v.HP == 0 && !v.Alive);
        v.HP = 50; l.HP = 100; s.Strike(v, l, p); s.Settle(v); s.Settle(l); Check("7a lifesteal heals 20% of damage dealt", Near(v.HP, 50.2f) && Near(l.HP, 99f));
        v.HP = v.D.HP - .1f; s.Strike(v, l, p); s.Settle(v); Check("7b lifesteal capped at max HP", Near(v.HP, v.D.HP));
        for (int order = 0; order < 2; order++)   // lethal collision in both processing orders: the Void Ball must stay dead
        {
            v.HP = .05f; l.HP = 100; v.PendingHeal = 0;
            if (order == 0) { s.Strike(l, v, p); s.Strike(v, l, p); } else { s.Strike(v, l, p); s.Strike(l, v, p); }
            s.Settle(v); s.Settle(l); Check($"8  lethal collision, order {order}: stays dead (no resurrection)", v.HP == 0 && !v.Alive);
        }
        log.AppendLine(fail == 0 ? "\nALL PASSED" : $"\n{fail} FAILED"); return log.ToString();
    }

    // timed per-ball effects; paused automatically during hit-stop / cinematics because Step returns early
    void TickStatus(Ball b, float dt)
    {
        if (b.BurnT > 0) { float d = Math.Min(dt, b.BurnT); b.BurnT -= dt; Dot(b, b.BurnDps * d); if (b.BurnT <= 0) b.BurnDps = 0; }
        if (b.PoisonT > 0) { float d = Math.Min(dt, b.PoisonT); b.PoisonT -= dt; Dot(b, Balance.PoisonDps * b.Poison * d); if (b.PoisonT <= 0) b.Poison = 0; }
        if (b.SlowT > 0 && (b.SlowT -= dt) <= 0) b.SlowStacks = 0;
        if (b.TailT > 0 && (b.TailT -= dt) <= 0) b.TailStacks = 0;
        if (b.BoostT > 0) b.BoostT -= dt;
        if (b.ShieldT > 0) b.ShieldT -= dt;
        if (b.FreezeT > 0 && (b.FreezeT -= dt) <= 0 && b.Shatter) { b.Shatter = false; Dot(b, Balance.FrostUltDmg); Ev(14, Kind.Frost, b.P); }
    }

    // zones (interval damage, capped gravity pull) and meteors (warning, then one impact each)
    void TickWorld(float dt)
    {
        for (int i = Zones.Count - 1; i >= 0; i--)
        {
            var z = Zones[i]; bool venom = z.K == Kind.Venom; float iv = venom ? 1f : Balance.VoidTick; z.Age += dt; z.Tick += dt;
            if (!venom) foreach (var o in Balls)
            {
                float d = Dist(o.P, z.P); if (o == z.Owner || !o.Alive || d > z.R + o.R || d < 12) continue;
                float k = Math.Min(Balance.VoidPull * dt, d - 12) / d; o.P = new(o.P.X + (z.P.X - o.P.X) * k, o.P.Y + (z.P.Y - o.P.Y) * k);   // never pulled past the centre
            }
            while (z.Tick >= iv) { z.Tick -= iv; float dmg = venom ? Balance.BloomDps : Balance.VoidTickDmg; foreach (var o in Balls) if (o != z.Owner && o.Alive && Dist(o.P, z.P) <= z.R + o.R) Dot(o, dmg); }
            if (z.Age >= z.Life) { Ev(venom ? 16 : 15, z.K, z.P, z.R); Zones.RemoveAt(i); }
        }
        for (int i = Meteors.Count - 1; i >= 0; i--)
        {
            var m = Meteors[i]; m.Age += dt;
            if (!m.Started && m.Age >= m.Start)
            {
                var t = Balls.Where(o => o != m.Owner && o.Alive).OrderBy(o => Dist(o.P, m.Owner.P)).FirstOrDefault();
                if (t == null) { Meteors.RemoveAt(i); continue; }
                m.Started = true; m.Pos = new(Math.Clamp(t.P.X + (float)(Rng.NextDouble() * 160 - 80), 30, W - 30), Math.Clamp(t.P.Y + (float)(Rng.NextDouble() * 160 - 80), 30, H - 30)); Ev(12, Kind.Cosmic, m.Pos, Balance.MeteorRadius);
            }
            else if (m.Started && m.Age >= m.Start + Balance.MeteorFall) { Area(m.Owner, m.Pos, Balance.MeteorRadius, Balance.MeteorDmg); Ev(13, Kind.Cosmic, m.Pos, Balance.MeteorRadius); Meteors.RemoveAt(i); }
        }
    }
}