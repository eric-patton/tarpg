using SadRogue.Primitives;

namespace Tarpg.Enemies;

// Skirmisher-tier Wolfwood unit. Bites and immediately retreats a few tiles
// away to fish for the next opening — the pacing creates a back-and-forth
// "spar" rather than a grind. Hits harder than a regular wolf to make the
// connection sting on the rare moments it lands.
public static class Wolfshade
{
    public static readonly EnemyDefinition Definition = new()
    {
        Id = "wolfshade",
        Name = "wolfshade",
        Glyph = 's',
        Color = new Color(90, 70, 90),
        BaseHealth = 22,
        BaseDamage = 6,
        MoveSpeed = 6.5f,
        AttackCooldown = 1.0f,
        AiTag = "melee_skirmisher",
        ZoneIds = new[] { "wolfwood" },
        RarityWeight = 1,
        FlavorText = "barely-there, gone before the bite registers",
    };
}
