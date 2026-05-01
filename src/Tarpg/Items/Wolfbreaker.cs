namespace Tarpg.Items;

// Example Legendary. Adding a new Legendary is this exact pattern:
//   1. A static class with a `public static readonly ItemDefinition Definition = ...`.
//   2. A nested ILegendaryEffect implementation describing the behavior.
// ContentInitializer will discover it automatically via reflection.
public static class Wolfbreaker
{
    public static readonly ItemDefinition Definition = new()
    {
        Id = "wolfbreaker",
        Name = "Wolfbreaker",
        Tier = ItemTier.Legendary,
        Slot = ItemSlot.Weapon,
        Glyph = ')',
        FlavorText = "carved by a hunter who could not stop",
        Effect = new WolfbreakerEffect(),
        // Tuned to ~2x the next-tier Magic weapon (IronBlade +6) so the
        // first-boss reward feels like a meaningful upgrade — auto-attack
        // jumps from 16 dmg (IronBlade) to 22 dmg (Wolfbreaker), shifting
        // the kit's DPS curve enough that the player notices on F6+.
        WeaponDamageBonus = 12,
    };

    private sealed class WolfbreakerEffect : ILegendaryEffect
    {
        public string Description =>
            "Your basic attacks against bleeding enemies deal +50% damage and refund Rage.";
    }
}
