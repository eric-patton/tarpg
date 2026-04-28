using SadRogue.Primitives;
using Tarpg.Core;

namespace Tarpg.Classes;

public sealed class WalkerClassDefinition : IRegistryEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Tagline { get; init; }
    public required Color GlyphColor { get; init; }
    public required ResourceType Resource { get; init; }
    public required string Description { get; init; }

    // Skill ids granted at level 1. More skills unlocked through level-up
    // and item drops; that data lives in the progression system.
    public IReadOnlyList<string> StartingSkillIds { get; init; } = Array.Empty<string>();

    // Base stats at level 1. Per-class growth curves applied on level-up.
    public int BaseHealth { get; init; } = 50;
    public int BaseResource { get; init; } = 100;
}
