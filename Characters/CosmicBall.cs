namespace BallBrawl;

sealed class CosmicBall : ChargedBallCharacter
{
    public CosmicBall(Def definition) : base(definition) { }
    public override AttackResult Attack(GameEngine engine, BallCharacter target, PointF contact) => new(Balance.BaseHit);

    protected override void ApplyPassive(GameEngine engine, BallCharacter target, float actualDamage, PointF contact)
    {
        if (Hits % 3 != 0) return;
        BoostT = Balance.CosmicBoostTime;
        engine.Emit(new GameEvent(11, D.K, P, new(1, 0), false, 1));
    }

    protected override void CastUltimate(GameEngine engine, BallCharacter target)
    {
        engine.Emit(new GameEvent(10, D.K, P, new(1, 0), true, 110));
        UltT = Balance.MeteorTime;
        engine.Ultimates.StartMeteorShower(this);
    }
}
