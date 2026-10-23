namespace BallBrawl;

sealed class SwordBall : BallCharacter
{
    public SwordBall(Def definition) : base(definition) { }

    public override AttackResult Attack(GameEngine engine, BallCharacter target, PointF contact)
    {
        bool finisher = Slashes >= 5;
        float damage = finisher ? 5 * Passive : Passive;
        if (finisher) Slashes = 0;
        else Slashes++;
        Passive += .4f;
        SlashT = 0;
        UltSlash = finisher;
        return new AttackResult(damage, finisher, HitStop: !finisher, Cinematic: finisher);
    }
}
