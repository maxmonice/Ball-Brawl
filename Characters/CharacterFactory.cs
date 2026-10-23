namespace BallBrawl;

/// <summary>Creates fresh character instances for contestants, previews, and background battles.</summary>
static class CharacterFactory
{
    public static BallCharacter Create(Def definition) => definition.K switch
    {
        Kind.Lightning => new LightningBall(definition),
        Kind.Sword => new SwordBall(definition),
        Kind.Juggernaut => new JuggernautBall(definition),
        Kind.Inferno => new InfernoBall(definition),
        Kind.Frost => new FrostBall(definition),
        Kind.Void => new VoidBall(definition),
        Kind.Venom => new VenomBall(definition),
        Kind.Solar => new SolarBall(definition),
        Kind.Mecha => new MechaBall(definition),
        Kind.Cosmic => new CosmicBall(definition),
        Kind.Tempest => new TempestBall(definition),
        _ => throw new ArgumentOutOfRangeException(nameof(definition), definition.K, "No playable character is registered for this kind.")
    };
}
