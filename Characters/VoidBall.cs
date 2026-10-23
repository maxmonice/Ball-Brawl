namespace BallBrawl;

sealed class VoidBall : ChargedBallCharacter
{
    public VoidBall(Def definition) : base(definition) { }
    public override AttackResult Attack(GameEngine engine, BallCharacter target, PointF contact) => new(Balance.BaseHit);

    protected override void ApplyPassive(GameEngine engine, BallCharacter target, float actualDamage, PointF contact) =>
        PendingHeal += actualDamage * Balance.VoidDrain;

    protected override void CastUltimate(GameEngine engine, BallCharacter target)
    {
        engine.Emit(new GameEvent(10, D.K, P, new(1, 0), true, Balance.VoidRadius));
        engine.Ultimates.AddZone(Kind.Void, this, P, Balance.VoidRadius, Balance.VoidTime);
        UltT = Balance.VoidTime;
    }
}
