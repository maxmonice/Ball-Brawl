namespace BallBrawl;

// Central tuning for character mechanics shared across their dedicated implementations.
static class Balance
{
    // ASSUMPTIONS (the spec gave no values): base collision damage and ultimate charge per collision for the new characters.
    public const float BaseHit = 1.0f, UltPerHit = 12f;
    public const float InfBurnDps = .5f, InfBurnTime = 3, InfUltDmg = 12, InfUltRadius = 170, InfUltBurnDps = 1.2f;
    public const float FrostSlowStep = .05f, FrostSlowMax = .30f, FrostSlowTime = 2, FrostFreeze = 1.5f, FrostUltDmg = 10;
    public const float VoidDrain = .2f, VoidTime = 3, VoidRadius = 160, VoidTickDmg = 2, VoidTick = .5f, VoidPull = 110;
    public const float PoisonDps = .2f, PoisonTime = 3, PoisonMax = 5, BloomTime = 4, BloomRadius = 140, BloomDps = 1;
    public const float SolarPerHit = 10, SolarBurstDmg = 5, SolarBurstRadius = 90, SolarFlareDmg = 15, SolarFlareRadius = 200;
    public const float MechaShieldEvery = 3, MechaShieldTime = 2, MechaShieldCut = .30f, OverTime = 4, OverSpeed = .40f, OverDmg = .50f;
    public const float CosmicBoost = .25f, CosmicBoostTime = 1.5f, MeteorTime = 3, MeteorCount = 5, MeteorDmg = 4, MeteorRadius = 70, MeteorFall = .7f;
    public const float TailStep = .05f, TailMax = .30f, TailTime = 2, CycloneTime = 3, CycloneSpeed = .5f, CycloneDmg = 2;
}
