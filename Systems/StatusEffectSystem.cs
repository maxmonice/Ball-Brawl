namespace BallBrawl;

/// <summary>Applies and advances timed status effects without knowing about rendering or character kinds.</summary>
sealed class StatusEffectSystem
{
    readonly GameEngine engine;

    public StatusEffectSystem(GameEngine engine) => this.engine = engine;

    public float MovementMultiplier(BallCharacter character)
    {
        if (character.FreezeT > 0) return 0;
        float multiplier = 1 - Math.Min(Balance.FrostSlowMax, character.SlowStacks * Balance.FrostSlowStep);
        if (character.BoostT > 0) multiplier *= 1 + Balance.CosmicBoost;
        if (character.TailStacks > 0)
            multiplier *= 1 + Math.Min(Balance.TailMax, character.TailStacks * Balance.TailStep);
        return multiplier;
    }

    public void ApplyBurn(BallCharacter target, float damagePerSecond, float duration)
    {
        target.BurnT = duration;
        target.BurnDps = Math.Max(target.BurnDps, damagePerSecond);
    }

    public void ApplySlow(BallCharacter target)
    {
        int max = (int)(Balance.FrostSlowMax / Balance.FrostSlowStep + .5f);
        target.SlowStacks = Math.Min(max, target.SlowStacks + 1);
        target.SlowT = Balance.FrostSlowTime;
    }

    public void ApplyPoison(BallCharacter target)
    {
        target.Poison = Math.Min((int)Balance.PoisonMax, target.Poison + 1);
        target.PoisonT = Balance.PoisonTime;
    }

    public void Update(BallCharacter character, float dt)
    {
        if (character.BurnT > 0)
        {
            float activeTime = Math.Min(dt, character.BurnT);
            character.BurnT -= dt;
            engine.Combat.ApplyDamage(character, character.BurnDps * activeTime, character.LastHit);
            if (character.BurnT <= 0) character.BurnDps = 0;
        }
        if (character.PoisonT > 0)
        {
            float activeTime = Math.Min(dt, character.PoisonT);
            character.PoisonT -= dt;
            engine.Combat.ApplyDamage(character, Balance.PoisonDps * character.Poison * activeTime, character.LastHit);
            if (character.PoisonT <= 0) character.Poison = 0;
        }
        if (character.SlowT > 0 && (character.SlowT -= dt) <= 0) character.SlowStacks = 0;
        if (character.TailT > 0 && (character.TailT -= dt) <= 0) character.TailStacks = 0;
        if (character.BoostT > 0) character.BoostT -= dt;
        if (character.ShieldT > 0) character.ShieldT -= dt;
        if (character.FreezeT > 0 && (character.FreezeT -= dt) <= 0 && character.Shatter)
        {
            character.Shatter = false;
            engine.Combat.ApplyDamage(character, Balance.FrostUltDmg, character.LastHit);
            engine.Emit(new GameEvent(14, Kind.Frost, character.P, new(1, 0), false, 1));
        }
    }
}
