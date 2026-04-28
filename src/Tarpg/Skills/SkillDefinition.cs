using Tarpg.Core;

namespace Tarpg.Skills;

public sealed class SkillDefinition : IRegistryEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required ResourceType Resource { get; init; }
    public required int Cost { get; init; }
    public int CooldownTicks { get; init; }
    public required ISkillBehavior Behavior { get; init; }
}
