namespace BallBrawl;

sealed class Zone
{
    public Kind Kind { get; init; }
    public PointF Position { get; init; }
    public float Radius { get; init; }
    public float Age { get; set; }
    public float Life { get; init; }
    public float Tick { get; set; }
    public BallCharacter Owner { get; init; }
}

sealed class Meteor
{
    public BallCharacter Owner { get; init; }
    public float Age { get; set; }
    public float Start { get; init; }
    public bool Started { get; set; }
    public PointF Position { get; set; }
}

/// <summary>Handles ultimate charge and timed ultimate world objects, including zones and meteors.</summary>
sealed class UltimateSystem
{
    readonly GameEngine engine;
    readonly List<Zone> zones = new();
    readonly List<Meteor> meteors = new();

    public UltimateSystem(GameEngine engine) => this.engine = engine;
    public IReadOnlyList<Zone> Zones => zones;
    public IReadOnlyList<Meteor> Meteors => meteors;

    internal void AddCharge(BallCharacter character, BallCharacter target, Action<GameEngine, BallCharacter> cast)
    {
        if ((character.Charge += Balance.UltPerHit) < 100) return;
        character.Charge = 0;
        cast(engine, target);
    }

    internal void AddZone(Kind kind, BallCharacter owner, PointF position, float radius, float duration) =>
        zones.Add(new Zone { Kind = kind, Owner = owner, Position = position, Radius = radius, Life = duration });

    internal void StartMeteorShower(BallCharacter owner)
    {
        for (int i = 0; i < (int)Balance.MeteorCount; i++)
            meteors.Add(new Meteor
            {
                Owner = owner,
                Start = .2f + i * (Balance.MeteorTime - Balance.MeteorFall - .2f) / (Balance.MeteorCount - 1)
            });
    }

    internal void Update(float dt)
    {
        UpdateZones(dt);
        UpdateMeteors(dt);
    }

    void UpdateZones(float dt)
    {
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            Zone zone = zones[i];
            bool venom = zone.Kind == Kind.Venom;
            float interval = venom ? 1f : Balance.VoidTick;
            zone.Age += dt;
            zone.Tick += dt;

            if (!venom)
            foreach (var target in engine.Balls)
            {
                float distance = GameEngine.Distance(target.P, zone.Position);
                if (target == zone.Owner || !target.Alive || distance > zone.Radius + target.R || distance < 12) continue;
                float pull = Math.Min(Balance.VoidPull * dt, distance - 12) / distance;
                target.P = new PointF(target.P.X + (zone.Position.X - target.P.X) * pull,
                    target.P.Y + (zone.Position.Y - target.P.Y) * pull);
            }

            while (zone.Tick >= interval)
            {
                zone.Tick -= interval;
                float damage = venom ? Balance.BloomDps : Balance.VoidTickDmg;
                foreach (var target in engine.Balls)
                    if (target != zone.Owner && target.Alive &&
                        GameEngine.Distance(target.P, zone.Position) <= zone.Radius + target.R)
                        engine.Combat.ApplyDamage(target, damage, zone.Owner);
            }

            if (zone.Age >= zone.Life)
            {
                engine.Emit(new GameEvent(venom ? 16 : 15, zone.Kind, zone.Position, new(1, 0), false, zone.Radius));
                zones.RemoveAt(i);
            }
        }
    }

    void UpdateMeteors(float dt)
    {
        for (int i = meteors.Count - 1; i >= 0; i--)
        {
            Meteor meteor = meteors[i];
            meteor.Age += dt;
            if (!meteor.Started && meteor.Age >= meteor.Start)
            {
                BallCharacter target = engine.Balls
                    .Where(candidate => candidate != meteor.Owner && candidate.Alive)
                    .OrderBy(candidate => GameEngine.Distance(candidate.P, meteor.Owner.P))
                    .FirstOrDefault();
                if (target == null) { meteors.RemoveAt(i); continue; }

                meteor.Started = true;
                meteor.Position = new PointF(
                    Math.Clamp(target.P.X + engine.NextRandom(-80, 80), 30, engine.W - 30),
                    Math.Clamp(target.P.Y + engine.NextRandom(-80, 80), 30, engine.H - 30));
                engine.Emit(new GameEvent(12, Kind.Cosmic, meteor.Position, new(1, 0), false, Balance.MeteorRadius));
            }
            else if (meteor.Started && meteor.Age >= meteor.Start + Balance.MeteorFall)
            {
                engine.Combat.ApplyAreaDamage(meteor.Owner, meteor.Position, Balance.MeteorRadius, Balance.MeteorDmg);
                engine.Emit(new GameEvent(13, Kind.Cosmic, meteor.Position, new(1, 0), false, Balance.MeteorRadius));
                meteors.RemoveAt(i);
            }
        }
    }
}
