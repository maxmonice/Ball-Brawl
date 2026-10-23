namespace BallBrawl;

sealed class LightningBall : BallCharacter
{
    public LightningBall(Def definition) : base(definition) { }

    public override AttackResult Attack(GameEngine engine, BallCharacter target, PointF contact)
    {
        bool wasOvercharged = UltT > 0;
        float damage = Passive * (wasOvercharged ? 2 : 1);
        Passive += .3f;
        Glow = .3f;
        target.Shock = 1f;

        if (!wasOvercharged && (Charge += 11) >= 100)
        {
            Charge = 0;
            UltT = 4 + UltCount++;
            engine.Emit(new GameEvent(1, Kind.Lightning, P, new(1, 0), true, 1));
        }

        // Preserve the original behavior: the collision that fills the meter uses normal damage,
        // while its visual hit is marked as the start of Overcharge.
        return new AttackResult(damage, UltT > 0);
    }

    public override float AbilityMovementMultiplier => UltT > 0 ? 4.5f : 1f;

    internal override void UpdateFrame(GameEngine engine, float dt)
    {
        base.UpdateFrame(engine, dt);
        if (UltT > 0)
        {
            AppendTrail(P);
        }
        else RemoveTrailPoint();
    }
}
