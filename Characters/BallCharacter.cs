namespace BallBrawl;

readonly record struct AttackResult(float Damage, bool UltimateEffect = false, bool HitStop = false, bool Cinematic = false);

/// <summary>Shared state and lifecycle for every playable ball. Subclasses own their combat behavior.</summary>
abstract class BallCharacter
{
    readonly List<PointF> trail = new();

    protected BallCharacter(Def definition)
    {
        D = definition ?? throw new ArgumentNullException(nameof(definition));
        HP = definition.HP;
    }

    public Def D { get; }
    public PointF P { get; internal set; }
    public PointF V { get; internal set; }
    public float HP { get; internal set; }
    public float Passive { get; internal set; } = .1f;
    public float Charge { get; internal set; }
    public float UltT { get; internal set; }
    public float Glow { get; internal set; }
    public float Shock { get; internal set; }
    public float SlashT { get; internal set; } = -1;
    public float Cd { get; internal set; }
    public float BurnT { get; internal set; }
    public float BurnDps { get; internal set; }
    public float PoisonT { get; internal set; }
    public float SlowT { get; internal set; }
    public float FreezeT { get; internal set; }
    public float ShieldT { get; internal set; }
    public float BoostT { get; internal set; }
    public float TailT { get; internal set; }
    public float Energy { get; internal set; }
    public int Poison { get; internal set; }
    public int SlowStacks { get; internal set; }
    public int TailStacks { get; internal set; }
    public int Hits { get; internal set; }
    public int Received { get; internal set; }
    public bool Shatter { get; internal set; }
    public bool UltSlash { get; internal set; }
    public float PendingHeal { get; internal set; }
    public float DamageDealt { get; internal set; }
    public int Kills { get; internal set; }
    public BallCharacter LastHit { get; internal set; }
    public int UltCount { get; internal set; }
    public int Slashes { get; internal set; }
    public string Label { get; internal set; }
    public IReadOnlyList<PointF> Trail => trail;
    public float R => D.R;
    public bool Alive => HP > 0;

    public abstract AttackResult Attack(GameEngine engine, BallCharacter target, PointF contact);
    public virtual void OnHitDealt(GameEngine engine, BallCharacter target, float actualDamage, PointF contact) { }
    public virtual void OnDamageReceived(GameEngine engine) { }
    public virtual float AbilityMovementMultiplier => 1f;
    public virtual float ModifyOutgoingCollisionDamage(float damage) => damage;
    public virtual float ModifyIncomingCollisionDamage(float damage) => damage;
    public virtual void ActivateUltimate(GameEngine engine, BallCharacter target) { }

    internal void AppendTrail(PointF position)
    {
        trail.Add(position);
        if (trail.Count > 24) trail.RemoveAt(0);
    }

    internal void RemoveTrailPoint()
    {
        if (trail.Count > 0) trail.RemoveAt(0);
    }

    internal virtual void UpdateFrame(GameEngine engine, float dt)
    {
        if (SlashT >= 0 && (SlashT += dt) > .25f) SlashT = -1;
        if (UltT > 0 && (UltT -= dt) <= 0)
            engine.Emit(new GameEvent(2, D.K, P, new(1, 0), false, 1));
    }
}

/// <summary>Common charge-on-hit lifecycle for the eight characters with percentage based ultimates.</summary>
abstract class ChargedBallCharacter : BallCharacter
{
    protected ChargedBallCharacter(Def definition) : base(definition) { }

    protected abstract void ApplyPassive(GameEngine engine, BallCharacter target, float actualDamage, PointF contact);
    protected abstract void CastUltimate(GameEngine engine, BallCharacter target);

    public sealed override void OnHitDealt(GameEngine engine, BallCharacter target, float actualDamage, PointF contact)
    {
        ApplyPassive(engine, target, actualDamage, contact);
        if (Alive && UltT <= 0) engine.Ultimates.AddCharge(this, target, CastUltimate);
    }

    public sealed override void ActivateUltimate(GameEngine engine, BallCharacter target) => CastUltimate(engine, target);
}
