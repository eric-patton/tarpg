using SadRogue.Primitives;

namespace Tarpg.Enemies;

// Tougher, rarer Wolfwood variant. Same melee_charger brain as the regular
// wolf but with ~3x HP and 2x bite damage, so it reads as the "elite" tier
// inside the same encounter pool. Distinct glyph (uppercase W) and a colder
// color so it's identifiable at a glance.
public static class DireWolf
{
    public static readonly EnemyDefinition Definition = new()
    {
        Id = "dire_wolf",
        Name = "dire wolf",
        Glyph = 'W',
        Color = new Color(110, 90, 130),
        BaseHealth = 50,
        BaseDamage = 9,
        MoveSpeed = 4.5f,
        AttackCooldown = 1.0f,
        AiTag = "melee_charger",
        ZoneIds = new[] { "wolfwood" },
        RarityWeight = 1,
        FlavorText = "older, larger, slower to forget",
    };
}
