namespace BallBrawl;

class Zone { public Kind K; public PointF P; public float R, Age, Life, Tick; public Ball Owner; }          // black hole / poison cloud
class Meteor { public Ball Owner; public float Age, Start; public bool Started; public PointF Pos; }       // Cosmic Ball meteor

// Gameplay rules for the newer characters. Pure logic: it raises GameEvents and never draws or plays sound.
partial class Sim
{
    public List<Zone> Zones = new(); public List<Meteor> Meteors = new();
    static bool IsNew(Kind k) => k >= Kind.Inferno;
    void Ev(int t, Kind k, PointF p, float scale = 1, bool ult = false) => Events.Add(new GameEvent(t, k, p, new(1, 0), ult, scale));
    void Dot(Ball o, float d) { o.HP = Math.Max(0, o.HP - d); }

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
            case Kind.Void: a.HP = Math.Min(a.D.HP, a.HP + actual * Balance.VoidDrain); break;
            case Kind.Venom: b.Poison = Math.Min((int)Balance.PoisonMax, b.Poison + 1); b.PoisonT = Balance.PoisonTime; Ev(17, a.D.K, b.P); break;
            case Kind.Solar:
                if (a.Energy >= 100) { a.Energy = 0; Area(a, cp, Balance.SolarBurstRadius, Balance.SolarBurstDmg); Ev(11, Kind.Solar, cp, Balance.SolarBurstRadius); }
                else a.Energy = Math.Min(100, a.Energy + Balance.SolarPerHit);
                break;
            case Kind.Cosmic: if (a.Hits % 3 == 0) { a.BoostT = Balance.CosmicBoostTime; Ev(11, Kind.Cosmic, a.P); } break;
            case Kind.Tempest: a.TailStacks = Math.Min((int)(Balance.TailMax / Balance.TailStep + .5f), a.TailStacks + 1); a.TailT = Balance.TailTime; Ev(11, Kind.Tempest, a.P); break;
        }
        if (a.UltT <= 0 && (a.Charge += Balance.UltPerHit) >= 100) { a.Charge = 0; Cast(a, b); }
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
