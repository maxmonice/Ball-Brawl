namespace BallBrawl;

sealed class MechaBall : ChargedBallCharacter
{
    public MechaBall(Def definition) : base(definition) { }
    public override AttackResult Attack(GameEngine engine, BallCharacter target, PointF contact) => new(Balance.BaseHit);
    public override float AbilityMovementMultiplier => UltT > 0 ? 1 + Balance.OverSpeed : 1;
    public override float ModifyOutgoingCollisionDamage(float damage) =>
        UltT > 0 ? damage * (1 + Balance.OverDmg) : damage;
    public override float ModifyIncomingCollisionDamage(float damage) =>
        ShieldT > 0 ? damage * (1 - Balance.MechaShieldCut) : damage;

    public override void OnDamageReceived(GameEngine engine)
    {
        Received++;
        if (Received % (int)Balance.MechaShieldEvery != 0) return;
        ShieldT = Balance.MechaShieldTime;
        engine.Emit(new GameEvent(11, D.K, P, new(1, 0), false, 1));
    }

    protected override void ApplyPassive(GameEngine engine, BallCharacter target, float actualDamage, PointF contact) { }

    protected override void CastUltimate(GameEngine engine, BallCharacter target)
    {
        engine.Emit(new GameEvent(10, D.K, P, new(1, 0), true, 90));
        UltT = Balance.OverTime;
    }
}
