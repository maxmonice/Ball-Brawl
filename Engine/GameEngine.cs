namespace BallBrawl;

/// <summary>Coordinates simulation systems and owns the authoritative state of one battle.</summary>
sealed class GameEngine
{
    readonly List<BallCharacter> balls = new();
    readonly Random random = new();

    public GameEngine(IEnumerable<Def> definitions, bool infinite, float width, float height)
    {
        if (definitions == null) throw new ArgumentNullException(nameof(definitions));
        Infinite = infinite;
        W = width;
        H = height;
        Events = new GameEventManager();
        Combat = new CombatSystem(this);
        StatusEffects = new StatusEffectSystem(this);
        Ultimates = new UltimateSystem(this);
        Physics = new PhysicsSystem(this);

        Def[] roster = definitions.ToArray();
        foreach (Def definition in roster)
        {
            double angle = random.NextDouble() * Math.PI * 2;
            int total = roster.Count(candidate => candidate == definition);
            int ordinal = balls.Count(character => character.D == definition) + 1;
            BallCharacter character = CharacterFactory.Create(definition);
            character.Label = total > 1 ? $"{definition.Name} #{ordinal}" : definition.Name;
            character.V = new PointF((float)Math.Cos(angle), (float)Math.Sin(angle));
            for (int attempt = 0; attempt < 60; attempt++)
            {
                character.P = new PointF(
                    character.R + (float)random.NextDouble() * Math.Max(1, W - 2 * character.R),
                    character.R + (float)random.NextDouble() * Math.Max(1, H - 2 * character.R));
                if (balls.All(other => Distance(other.P, character.P) > other.R + character.R + 8)) break;
            }
            balls.Add(character);
        }
    }

    public IReadOnlyList<BallCharacter> Balls => balls;
    public IReadOnlyList<Zone> Zones => Ultimates.Zones;
    public IReadOnlyList<Meteor> Meteors => Ultimates.Meteors;
    public GameEventManager Events { get; }
    public CombatSystem Combat { get; }
    public StatusEffectSystem StatusEffects { get; }
    public UltimateSystem Ultimates { get; }
    public PhysicsSystem Physics { get; }
    public bool Infinite { get; }
    public float W { get; set; } = 900;
    public float H { get; set; } = 600;
    public float Hit { get; internal set; }
    public float CineT { get; internal set; } = -1;
    public float Time { get; private set; }
    public PointF CineP { get; private set; }
    public float SpeedMul => Infinite ? 1 : Math.Min(4f, 1 + .1f * MathF.Floor(Time / 2.5f));
    public BallCharacter Winner => balls.Count(character => character.Alive) == 1
        ? balls.First(character => character.Alive)
        : null;
    public bool Over => balls.Count(character => character.Alive) <= 1;

    internal static float Distance(PointF p, PointF q)
    {
        float dx = p.X - q.X, dy = p.Y - q.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    internal float NextRandom(float minimum, float maximum) => minimum + (float)random.NextDouble() * (maximum - minimum);
    internal void Emit(GameEvent gameEvent) => Events.Publish(gameEvent);

    internal void StartCinematic(PointF contact, BallCharacter attacker, BallCharacter target)
    {
        CineT = 0;
        CineP = contact;
        Emit(new GameEvent(20, Kind.Sword, contact, new(1, 0), true, 1, attacker, target));
    }

    public void Step(float dt)
    {
        if (CineT >= 0)
        {
            CineT += dt;
            if (CineT > .65f)
            {
                CineT = -1;
                Emit(new GameEvent(21, Kind.Sword, CineP, new(1, 0), true, 1));
            }
            return;
        }
        if (Hit > 0) { Hit -= dt; return; }

        Time += dt;
        Physics.AdvanceCooldowns(dt);
        foreach (BallCharacter character in balls.Where(candidate => candidate.Alive))
        {
            Physics.Move(character, dt);
            character.Cd -= dt;
            character.Glow -= dt;
            character.Shock -= dt;
            StatusEffects.Update(character, dt);
            character.UpdateFrame(this, dt);
            if (Infinite && character.HP < character.D.HP * .25f) character.HP = character.D.HP;
        }

        Ultimates.Update(dt);
        Physics.ResolveCollisions();
    }

    internal void ApplyAreaDamage(BallCharacter owner, PointF center, float radius, float damage,
        Action<BallCharacter> afterDamage = null) =>
        Combat.ApplyAreaDamage(owner, center, radius, damage, afterDamage);

    /// <summary>Small executable regression suite retained for the --selftest command.</summary>
    internal static string SelfTest()
    {
        var log = new System.Text.StringBuilder();
        int fail = 0;
        void Check(string name, bool ok)
        {
            if (!ok) fail++;
            log.AppendLine((ok ? "PASS  " : "FAIL  ") + name);
        }
        static bool Near(float a, float b) => MathF.Abs(a - b) < 1e-4f;
        Def Definition(Kind kind) => Roster.All.First(definition => definition.K == kind);

        var engine = new GameEngine(new[] { Definition(Kind.Void), Definition(Kind.Lightning) }, false, 400, 400);
        var voidBall = engine.Balls[0];
        var lightning = engine.Balls[1];
        var contact = new PointF(200, 200);
        voidBall.HP = 100;
        Check("1  100 HP takes damage", Near(engine.Combat.ApplyDamage(voidBall, 1f), 1f) && Near(voidBall.HP, 99));
        voidBall.HP = 1;
        engine.Combat.ApplyDamage(voidBall, .5f);
        Check("2  1 HP takes damage", Near(voidBall.HP, .5f));
        voidBall.HP = .3f;
        engine.Combat.ApplyDamage(voidBall, .1f);
        Check("3  0.3 -> ~0.2", Near(voidBall.HP, .2f));
        engine.Combat.ApplyDamage(voidBall, .1f);
        Check("4  0.2 -> ~0.1", Near(voidBall.HP, .1f) && voidBall.Alive);
        engine.Combat.ApplyDamage(voidBall, .1f);
        Check("5a 0.1 - 0.1 reaches exactly 0", voidBall.HP == 0 && !voidBall.Alive);
        voidBall.HP = .1f;
        float overkill = engine.Combat.ApplyDamage(voidBall, 5f);
        Check("5b lethal overkill -> 0, removes only 0.1", voidBall.HP == 0 && Near(overkill, .1f));
        Check("6  dead ball takes nothing, stays dead", engine.Combat.ApplyDamage(voidBall, 1f) == 0 && voidBall.HP == 0 && !voidBall.Alive);

        voidBall.HP = 50;
        lightning.HP = 100;
        engine.Combat.ResolveStrike(voidBall, lightning, contact);
        engine.Combat.Settle(voidBall);
        Check("7a lifesteal heals 20% of damage dealt", Near(voidBall.HP, 50.2f) && Near(lightning.HP, 99f));
        voidBall.HP = voidBall.D.HP - .1f;
        engine.Combat.ResolveStrike(voidBall, lightning, contact);
        engine.Combat.Settle(voidBall);
        Check("7b lifesteal capped at max HP", Near(voidBall.HP, voidBall.D.HP));

        for (int order = 0; order < 2; order++)
        {
            voidBall.HP = .05f;
            lightning.HP = 100;
            voidBall.PendingHeal = 0;
            if (order == 0)
            {
                engine.Combat.ResolveStrike(lightning, voidBall, contact);
                engine.Combat.ResolveStrike(voidBall, lightning, contact);
            }
            else
            {
                engine.Combat.ResolveStrike(voidBall, lightning, contact);
                engine.Combat.ResolveStrike(lightning, voidBall, contact);
            }
            engine.Combat.Settle(voidBall);
            engine.Combat.Settle(lightning);
            Check($"8  lethal collision, order {order}: stays dead (no resurrection)", voidBall.HP == 0 && !voidBall.Alive);
        }

        var definitions = Roster.All.ToArray();
        var instances = definitions.Select(CharacterFactory.Create).ToArray();
        Check("9 all eleven roster entries create distinct character classes",
            instances.Length == 11 && instances.Select(character => character.GetType()).Distinct().Count() == 11);
        var duplicateEngine = new GameEngine(new[] { Definition(Kind.Solar), Definition(Kind.Solar) }, false, 400, 400);
        Check("10 duplicate selections create separate labeled contestants",
            duplicateEngine.Balls.Count == 2 && duplicateEngine.Balls[0] != duplicateEngine.Balls[1] &&
            duplicateEngine.Balls[0].Label == "Solar Ball #1" && duplicateEngine.Balls[1].Label == "Solar Ball #2");

        var collisionEngine = new GameEngine(new[] { Definition(Kind.Void), Definition(Kind.Lightning) }, false, 400, 400);
        BallCharacter collisionVoid = collisionEngine.Balls[0], collisionLightning = collisionEngine.Balls[1];
        collisionVoid.HP = collisionLightning.HP = .5f;
        collisionEngine.Combat.ResolveCollision(collisionVoid, collisionLightning, contact, contact);
        Check("11 simultaneous collision resolves both attacks and settles lifesteal after both hits",
            collisionLightning.HP == 0 && collisionVoid.HP == .5f && collisionVoid.Kills == 1 &&
            Near(collisionVoid.DamageDealt, .5f) && Near(collisionLightning.DamageDealt, .1f));

        var armorEngine = new GameEngine(new[] { Definition(Kind.Lightning), Definition(Kind.Mecha) }, false, 400, 400);
        BallCharacter armorAttacker = armorEngine.Balls[0], mecha = armorEngine.Balls[1];
        for (int hit = 0; hit < 3; hit++) armorEngine.Combat.ResolveStrike(armorAttacker, mecha, contact);
        float shieldedDamage = armorAttacker.ModifyOutgoingCollisionDamage(1f);
        shieldedDamage = mecha.ModifyIncomingCollisionDamage(shieldedDamage);
        Check("12 Mecha armor activates after the third received hit and reduces later collision damage",
            mecha.Received == 3 && mecha.ShieldT > 0 && Near(shieldedDamage, .7f));

        bool allUltimatesWork = true;
        foreach (Def definition in Roster.All.Where(candidate => candidate.K >= Kind.Inferno))
        {
            var abilityEngine = new GameEngine(new[] { definition, Definition(Kind.Lightning) }, false, 400, 400);
            BallCharacter caster = abilityEngine.Balls[0], target = abilityEngine.Balls[1];
            caster.P = target.P = contact;
            caster.ActivateUltimate(abilityEngine, target);
            allUltimatesWork &= definition.K switch
            {
                Kind.Inferno => target.BurnT > 0 && target.HP < target.D.HP,
                Kind.Frost => target.FreezeT > 0 && target.Shatter,
                Kind.Void => abilityEngine.Zones.Count == 1 && caster.UltT > 0,
                Kind.Venom => abilityEngine.Zones.Count == 1 && caster.UltT > 0,
                Kind.Solar => target.HP < target.D.HP,
                Kind.Mecha => caster.UltT > 0,
                Kind.Cosmic => abilityEngine.Meteors.Count == (int)Balance.MeteorCount && caster.UltT > 0,
                Kind.Tempest => caster.UltT > 0,
                _ => false
            };
        }
        Check("13 every charged character dispatches its own ultimate implementation", allUltimatesWork);

        log.AppendLine(fail == 0 ? "\nALL PASSED" : $"\n{fail} FAILED");
        return log.ToString();
    }
}
