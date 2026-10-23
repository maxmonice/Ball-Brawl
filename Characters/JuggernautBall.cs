namespace BallBrawl;

sealed class JuggernautBall : BallCharacter
{
    public JuggernautBall(Def definition) : base(definition) { }

    public override AttackResult Attack(GameEngine engine, BallCharacter target, PointF contact)
    {
        float damage = Passive;
        Passive += .1f;
        return new AttackResult(damage);
    }
}
