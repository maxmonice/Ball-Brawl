namespace BallBrawl;

sealed class FrostBall : ChargedBallCharacter
{
    public FrostBall(Def definition) : base(definition) { }
    public override AttackResult Attack(GameEngine engine, BallCharacter target, PointF contact) => new(Balance.BaseHit);

    protected override void ApplyPassive(GameEngine engine, BallCharacter target, float actualDamage, PointF contact)
    {
        engine.StatusEffects.ApplySlow(target);
        engine.Emit(new GameEvent(17, D.K, target.P, new(1, 0), false, 1));
    }

    protected override void CastUltimate(GameEngine engine, BallCharacter target)
    {
        engine.Emit(new GameEvent(10, D.K, target.P, new(1, 0), true, 130));
        target.FreezeT = Balance.FrostFreeze;
        target.Shatter = true;
    }
}
