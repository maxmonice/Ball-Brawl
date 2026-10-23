namespace BallBrawl;

/// <summary>Moves balls, bounces at arena edges, and resolves pairwise elastic collisions.</summary>
sealed class PhysicsSystem
{
    readonly GameEngine engine;
    readonly Dictionary<(BallCharacter A, BallCharacter B), float> cooldowns = new();

    public PhysicsSystem(GameEngine engine) => this.engine = engine;

    public void AdvanceCooldowns(float dt)
    {
        foreach (var pair in cooldowns.Keys.ToArray()) cooldowns[pair] -= dt;
    }

    public void Move(BallCharacter character, float dt)
    {
        float speed = Math.Min(280 * engine.SpeedMul * engine.StatusEffects.MovementMultiplier(character)
            * character.AbilityMovementMultiplier, 1500);
        float magnitude = MathF.Sqrt(character.V.X * character.V.X + character.V.Y * character.V.Y);
        if (magnitude < .0001f) character.V = new PointF(1, 0);
        else character.V = new PointF(character.V.X / magnitude, character.V.Y / magnitude);

        character.P = new PointF(character.P.X + character.V.X * speed * dt,
            character.P.Y + character.V.Y * speed * dt);
        if (character.P.X < character.R) { character.P = new PointF(character.R, character.P.Y); character.V = new PointF(MathF.Abs(character.V.X), character.V.Y); }
        if (character.P.X > engine.W - character.R) { character.P = new PointF(engine.W - character.R, character.P.Y); character.V = new PointF(-MathF.Abs(character.V.X), character.V.Y); }
        if (character.P.Y < character.R) { character.P = new PointF(character.P.X, character.R); character.V = new PointF(character.V.X, MathF.Abs(character.V.Y)); }
        if (character.P.Y > engine.H - character.R) { character.P = new PointF(character.P.X, engine.H - character.R); character.V = new PointF(character.V.X, -MathF.Abs(character.V.Y)); }
    }

    public void ResolveCollisions()
    {
        for (int i = 0; i < engine.Balls.Count; i++)
        for (int j = i + 1; j < engine.Balls.Count; j++)
        {
            BallCharacter a = engine.Balls[i], b = engine.Balls[j];
            if (!a.Alive || !b.Alive) continue;
            float dx = b.P.X - a.P.X, dy = b.P.Y - a.P.Y;
            float distance = MathF.Sqrt(dx * dx + dy * dy), minimum = a.R + b.R;
            if (distance >= minimum || distance < .001f) continue;

            float nx = dx / distance, ny = dy / distance, overlap = minimum - distance;
            a.P = new PointF(a.P.X - nx * overlap / 2, a.P.Y - ny * overlap / 2);
            b.P = new PointF(b.P.X + nx * overlap / 2, b.P.Y + ny * overlap / 2);
            float massA = a.R * a.R, massB = b.R * b.R;
            float velocityA = a.V.X * nx + a.V.Y * ny;
            float velocityB = b.V.X * nx + b.V.Y * ny;
            if (velocityA - velocityB > 0)
            {
                float nextA = (velocityA * (massA - massB) + 2 * massB * velocityB) / (massA + massB);
                float nextB = (velocityB * (massB - massA) + 2 * massA * velocityA) / (massA + massB);
                a.V = new PointF(a.V.X + (nextA - velocityA) * nx, a.V.Y + (nextA - velocityA) * ny);
                b.V = new PointF(b.V.X + (nextB - velocityB) * nx, b.V.Y + (nextB - velocityB) * ny);
            }

            var key = (a, b);
            if (cooldowns.TryGetValue(key, out float cooldown) && cooldown > 0) continue;
            cooldowns[key] = .25f;
            var contactA = new PointF(a.P.X + nx * a.R, a.P.Y + ny * a.R);
            var contactB = new PointF(b.P.X - nx * b.R, b.P.Y - ny * b.R);
            engine.Combat.ResolveCollision(a, b, contactA, contactB);
        }
    }
}
