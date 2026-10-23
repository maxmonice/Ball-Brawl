namespace BallBrawl;

sealed class SolarBall : ChargedBallCharacter
{
    public SolarBall(Def definition) : base(definition) { }
    public override AttackResult Attack(GameEngine engine, BallCharacter target, PointF contact) => new(Balance.BaseHit);

    protected override void ApplyPassive(GameEngine engine, BallCharacter target, float actualDamage, PointF contact)
    {
        // The original meter bursts on the collision after it reaches 100, not on the 10th hit.
        if (Energy >= 100)
        {
            Energy = 0;
            engine.ApplyAreaDamage(this, contact, Balance.SolarBurstRadius, Balance.SolarBurstDmg);
            engine.Emit(new GameEvent(11, D.K, contact, new(1, 0), false, Balance.SolarBurstRadius));
        }
        else Energy = Math.Min(100, Energy + Balance.SolarPerHit);
    }

    protected override void CastUltimate(GameEngine engine, BallCharacter target)
    {
        engine.Emit(new GameEvent(10, D.K, P, new(1, 0), true, Balance.SolarFlareRadius));
        engine.ApplyAreaDamage(this, P, Balance.SolarFlareRadius, Balance.SolarFlareDmg);
    }
}
