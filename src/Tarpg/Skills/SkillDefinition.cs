using Tarpg.Core;

namespace Tarpg.Skills;

public sealed class SkillDefinition : IRegistryEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required ResourceType Resource { get; init; }
    public required int Cost { get; init; }

    // Seconds between successive activations. 0 = no cooldown beyond the
    // resource cost gate.
    public float CooldownSec { get; init; }

    // Glyph drawn into the skill slot in the bottom-bar HUD. Convention:
    // pick something visually associated with the action (a slash for
    // melee swings, an arrow for ranged, a swirl for AoEs, etc.).
    public required char Glyph { get; init; }

    public required ISkillBehavior Behavior { get; init; }
}
