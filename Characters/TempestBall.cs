namespace BallBrawl;

sealed class TempestBall : ChargedBallCharacter
{
    public TempestBall(Def definition) : base(definition) { }
    public override AttackResult Attack(GameEngine engine, BallCharacter target, PointF contact) => new(Balance.BaseHit);
    public override float AbilityMovementMultiplier => UltT > 0 ? 1 + Balance.CycloneSpeed : 1;
    public override float ModifyOutgoingCollisionDamage(float damage) =>
        UltT > 0 ? damage + Balance.CycloneDmg : damage;

    protected override void ApplyPassive(GameEngine engine, BallCharacter target, float actualDamage, PointF contact)
    {
        int max = (int)(Balance.TailMax / Balance.TailStep + .5f);
        TailStacks = Math.Min(max, TailStacks + 1);
        TailT = Balance.TailTime;
        engine.Emit(new GameEvent(11, D.K, P, new(1, 0), false, 1));
    }

    protected override void CastUltimate(GameEngine engine, BallCharacter target)
    {
        engine.Emit(new GameEvent(10, D.K, P, new(1, 0), true, 150));
        UltT = Balance.CycloneTime;
    }
}
