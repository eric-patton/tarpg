using SadRogue.Primitives;

namespace Tarpg.Enemies;

// Ranged-tier Wolfwood unit. Keeps a 4–6 tile band from the player and lobs
// a hitscan howl on cooldown when LOS holds. Backpedals when crowded,
// advances when the player breaks line of sight. No projectile entity yet —
// damage is instantaneous on the player; HitFeedback's flash + damage
// number on the player is the "you got shot" cue.
public static class Howler
{
    public static readonly EnemyDefinition Definition = new()
    {
        Id = "howler",
        Name = "howler",
        Glyph = 'h',
        Color = new Color(130, 100, 150),
        BaseHealth = 14,
        BaseDamage = 5,
        MoveSpeed = 5.0f,
        AttackCooldown = 1.4f,
        AiTag = "ranged_kiter",
        ZoneIds = new[] { "wolfwood" },
        RarityWeight = 1,
        FlavorText = "cries from the dark and you flinch first",
    };
}
