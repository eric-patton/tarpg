using SadRogue.Primitives;

namespace Tarpg.Items;

// First-cut consumables: HP and Resource potions. Drop on enemy kill at
// LootDropChance, render on the floor as a colored '!' glyph, picked up
// on tile-cross, drunk via the 1 / 2 keys to restore HP / resource.
//
// Heal / restore amounts live as constants on this class instead of on
// ItemDefinition so we don't grow the registry-entry shape for every new
// item type. When more consumable variants land (greater potion, antidote,
// etc.) we promote these to a small ConsumableData record.
public static class Potions
{
    // Tunable: per-drink amounts and the drink-cooldown gate.
    public const int HealthPotionHealAmount = 40;
    public const int ResourcePotionRestoreAmount = 30;

    // Spam-drink gate. 0.5s is enough to feel like a deliberate action
    // without breaking the "panic-drink" usage pattern in tight fights.
    public const float DrinkCooldownSec = 0.5f;

    public static readonly ItemDefinition HealthPotion = new()
    {
        Id = "health_potion",
        Name = "health potion",
        Tier = ItemTier.Normal,
        Slot = ItemSlot.None,
        Glyph = '!',
        FlavorText = "stoppered glass; tastes of iron and rosehip",
    };

    public static readonly ItemDefinition ResourcePotion = new()
    {
        Id = "resource_potion",
        Name = "resource potion",
        Tier = ItemTier.Normal,
        Slot = ItemSlot.None,
        Glyph = '!',
        FlavorText = "warm to the touch; the inside flickers like a coal",
    };

    // Per-tier glyph colors for floor rendering. Lifted here so the
    // FloorItem entity can color its glyph by potion type without
    // hard-coding RGB triples in two places.
    public static readonly Color HealthGlyphColor = new(220, 60, 60);
    public static readonly Color ResourceGlyphColor = new(220, 140, 40);
}
