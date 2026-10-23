namespace BallBrawl;

/// <summary>Owns collision damage, attribution, kill counts, and collision-scoped ability hooks.</summary>
sealed class CombatSystem
{
    readonly GameEngine engine;

    public CombatSystem(GameEngine engine) => this.engine = engine;

    public float ApplyDamage(BallCharacter target, float damage, BallCharacter source = null)
    {
        if (target == null || damage <= 0 || !target.Alive) return 0;
        float before = target.HP;
        float after = before - damage;
        if (after <= 1e-5f) after = 0;
        target.HP = after;
        float actual = before - after;
        if (source != null && source != target)
        {
            source.DamageDealt += actual;
            target.LastHit = source;
            if (after == 0) source.Kills++;
        }
        return actual;
    }

    internal void ResolveCollision(BallCharacter first, BallCharacter second, PointF firstContact, PointF secondContact)
    {
        ResolveStrike(first, second, firstContact);
        ResolveStrike(second, first, secondContact);
        Settle(first);
        Settle(second);
    }

    internal void ResolveStrike(BallCharacter attacker, BallCharacter target, PointF contact)
    {
        AttackResult attack = attacker.Attack(engine, target, contact);
        float damage = attacker.ModifyOutgoingCollisionDamage(attack.Damage);
        damage = target.ModifyIncomingCollisionDamage(damage);
        EmitHit(attacker, target, contact, attack.UltimateEffect);

        if (attack.Cinematic)
        {
            engine.StartCinematic(contact, attacker, target);
        }
        else if (attack.HitStop) engine.Hit = .05f;

        float actual = ApplyDamage(target, damage, attacker);
        target.OnDamageReceived(engine);
        attacker.Hits++;
        attacker.OnHitDealt(engine, target, actual, contact);
    }

    public void Settle(BallCharacter character)
    {
        if (character.PendingHeal > 0 && character.Alive)
            character.HP = Math.Min(character.D.HP, character.HP + character.PendingHeal);
        character.PendingHeal = 0;
    }

    public void ApplyAreaDamage(BallCharacter owner, PointF center, float radius, float damage,
        Action<BallCharacter> afterDamage = null)
    {
        foreach (var target in engine.Balls)
        {
            if (target == owner || !target.Alive || GameEngine.Distance(target.P, center) > radius + target.R) continue;
            ApplyDamage(target, damage, owner);
            afterDamage?.Invoke(target);
        }
    }

    void EmitHit(BallCharacter attacker, BallCharacter target, PointF contact, bool ultimate)
    {
        float dx = target.P.X - attacker.P.X;
        float dy = target.P.Y - attacker.P.Y;
        float distance = MathF.Max(.001f, MathF.Sqrt(dx * dx + dy * dy));
        engine.Emit(new GameEvent(0, attacker.D.K, contact, new(dx / distance, dy / distance), ultimate,
            attacker.R / 28f, attacker, target));
    }
}
