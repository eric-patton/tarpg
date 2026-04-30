using SadRogue.Primitives;

namespace Tarpg.Enemies;

// Horde-tier Wolfwood unit. Each spawn slot the BSP picks for a pup will
// place PackSize copies clustered around the point, so the tactical feel is
// "you're surrounded by a yipping pile" rather than "you trade hits with one
// thing." Individually frail (low HP, low bite) but the swarm number adds
// up. Faster than the standard wolf so the pack can close gaps even when
// the player tries to disengage.
public static class WolfPup
{
    public static readonly EnemyDefinition Definition = new()
    {
        Id = "wolf_pup",
        Name = "wolf pup",
        Glyph = 'p',
        Color = new Color(170, 150, 110),
        BaseHealth = 5,
        BaseDamage = 1,
        MoveSpeed = 7.0f,
        AttackCooldown = 0.6f,
        AiTag = "melee_charger",
        ZoneIds = new[] { "wolfwood" },
        RarityWeight = 3,
        PackSize = 3,
        FlavorText = "yipping, half-grown, all teeth",
    };
}
