namespace BallBrawl;

record BallResult(string Name, Def D, float Damage, int Kills, bool Won);
record MatchOutcome(IReadOnlyList<BallResult> Results, Def Winner, bool Draw);

/// <summary>Creates match engines and snapshots final statistics for the results screen.</summary>
sealed class MatchManager
{
    public GameEngine Start(IEnumerable<Def> contestants, bool infinite, float width, float height) =>
        new(contestants, infinite, width, height);

    public MatchOutcome Complete(GameEngine engine)
    {
        if (engine == null) throw new ArgumentNullException(nameof(engine));
        var results = engine.Balls.Select(character => new BallResult(
            character.Label ?? character.D.Name, character.D, character.DamageDealt,
            character.Kills, character.Alive)).ToArray();
        Def winner = engine.Winner?.D;
        return new MatchOutcome(results, winner, winner == null);
    }
}
