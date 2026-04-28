using Tarpg.Core;

namespace Tarpg.Modifiers;

public sealed class ModifierDefinition : IRegistryEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }      // short label, e.g. "Burning"
    public required string Description { get; init; } // tooltip text shown to player
    public required IModifierBehavior Behavior { get; init; }

    // Roll weight when generating floor mods. Lower = rarer.
    public int Weight { get; init; } = 100;

    // Higher = more dangerous. Used by floor generation to balance multi-mod stacks.
    public int DifficultyTier { get; init; } = 1;

    // Drop quality bonus contributed by this modifier (additive, in percent).
    public int LootBonusPercent { get; init; } = 10;
}
