namespace BallBrawl;

readonly record struct GameEvent(int T, Kind K, PointF P, PointF N, bool Ult, float Scale,
    BallCharacter Src = null, BallCharacter Dst = null);

/// <summary>Queues simulation events for presentation systems to consume after each simulation tick.</summary>
sealed class GameEventManager
{
    readonly List<GameEvent> pending = new();

    public void Publish(GameEvent gameEvent) => pending.Add(gameEvent);

    public IReadOnlyList<GameEvent> Drain()
    {
        if (pending.Count == 0) return Array.Empty<GameEvent>();
        var events = pending.ToArray();
        pending.Clear();
        return events;
    }
}
