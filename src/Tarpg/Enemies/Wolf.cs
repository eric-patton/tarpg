using SadRogue.Primitives;

namespace Tarpg.Enemies;

public static class Wolf
{
    public static readonly EnemyDefinition Definition = new()
    {
        Id = "wolf",
        Name = "wolf",
        Glyph = 'w',
        Color = new Color(150, 130, 90),
        BaseHealth = 18,
        BaseDamage = 4,
        AiTag = "melee_charger",
        ZoneIds = new[] { "wolfwood" },
        FlavorText = "lean and quiet, the kind that learns",
    };
}
