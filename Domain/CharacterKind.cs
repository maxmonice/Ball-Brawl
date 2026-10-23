namespace BallBrawl;

// Kind remains a stable roster/rendering identity. Gameplay behavior lives on the
// corresponding BallCharacter implementation.
enum Kind { Lightning, Sword, Juggernaut, Inferno, Frost, Void, Venom, Solar, Mecha, Cosmic, Tempest }

record Def(Kind K, string Name, Color C, int HP, float R, string Passive, string Ult);

static class Roster
{
    public static readonly Def[] All =
    {
        new(Kind.Lightning, "Lightning Ball", Color.FromArgb(40, 120, 255), 100, 28,
            "Electric Shock: every collision shocks the target. Damage starts at 0.1 and grows +0.3 per collision.",
            "Overcharge: 11 charge per collision. At 100, moves much faster and deals 2x damage for 4s (+1s each use)."),
        new(Kind.Sword, "Anime Sword Ball", Color.FromArgb(235, 235, 245), 100, 28,
            "Blade Counter: 360-degree slash on every collision (attack or defense). Damage starts at 0.1, +0.4 per collision.",
            "Anime Finisher: after 5 slashes, the next slash deals 5x damage with a cinematic sequence."),
        new(Kind.Juggernaut, "Juggernaut", Color.FromArgb(120, 50, 190), 300, 56,
            "None. 300 HP and 2x size. Collision damage starts at 0.1 and grows +0.1 per collision.", "None."),
        new(Kind.Inferno, "Inferno Ball", Color.FromArgb(255, 90, 30), 100, 28,
            "Burning Impact: hits burn the target for 0.5 dmg/s for 3s (refreshes, no stacking).", "Supernova: fiery explosion deals 12 damage in a large radius and applies a stronger burn for 3s."),
        new(Kind.Frost, "Frost Ball", Color.FromArgb(170, 220, 255), 120, 28,
            "Frostbite: each hit slows the target 5% (max 30%), lasting 2s after the latest hit.", "Absolute Zero: freezes the target for 1.5s, then deals 10 damage and releases ice shards."),
        new(Kind.Void, "Void Ball", Color.FromArgb(40, 10, 60), 90, 28,
            "Void Drain: heals for 20% of the damage it actually deals (up to max HP).", "Black Hole: a 3s gravity field pulls opponents in and deals 2 damage every 0.5s inside it."),
        new(Kind.Venom, "Venom Ball", Color.FromArgb(110, 220, 40), 110, 28,
            "Corrosion: each hit adds a poison stack (max 5); each stack deals 0.2 dmg/s for 3s (refreshes).", "Toxic Bloom: a 4s poison cloud deals 1 damage per second to opponents inside."),
        new(Kind.Solar, "Solar Ball", Color.FromArgb(255, 200, 40), 100, 28,
            "Solar Charge: +10 energy per hit; at 100 the next hit releases a burst for 5 extra damage nearby.", "Solar Flare: a radiant explosion deals 15 damage within its radius."),
        new(Kind.Mecha, "Mecha Ball", Color.FromArgb(150, 160, 175), 150, 28,
            "Adaptive Armor: after every 3 hits received, a shield cuts incoming collision damage 30% for 2s.", "Overdrive: for 4s, +40% movement speed and +50% outgoing collision damage."),
        new(Kind.Cosmic, "Cosmic Ball", Color.FromArgb(170, 70, 200), 100, 28,
            "Cosmic Bounce: every 3rd hit grants +25% movement speed for 1.5s.", "Meteor Shower: for 3s, five meteors strike near the opponent, 4 damage each in their impact radius."),
        new(Kind.Tempest, "Tempest Ball", Color.FromArgb(200, 245, 255), 85, 28,
            "Tailwind: +5% speed per hit (max 30%), lasting 2s after the latest hit.", "Cyclone: for 3s, +50% movement speed and +2 damage on collision hits."),
    };
}
