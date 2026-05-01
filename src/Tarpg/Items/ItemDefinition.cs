using Tarpg.Core;

namespace Tarpg.Items;

public sealed class ItemDefinition : IRegistryEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required ItemTier Tier { get; init; }
    public required ItemSlot Slot { get; init; }
    public required char Glyph { get; init; }

    public string? FlavorText { get; init; }

    // For Legendary / Set items: the effect they grant. Null for Normal/Magic/Rare bases.
    public ILegendaryEffect? Effect { get; init; }

    // For Set items: the set this item belongs to (e.g. "wolf_mothers_wedding").
    public string? SetId { get; init; }

    // Affix pool ids this base draws from (Magic/Rare items only).
    public IReadOnlyList<string> AffixPool { get; init; } = Array.Empty<string>();

    // Flat damage added to the player's auto-attack while this item is
    // equipped (Slot == Weapon). Future skill-damage scaling will read
    // the same field — for v0 only auto-attack benefits. Default 0 so
    // non-weapon items don't accidentally contribute damage.
    public int WeaponDamageBonus { get; init; } = 0;
}
