using SadRogue.Primitives;
using Tarpg.Core;

namespace Tarpg.Bosses;

public sealed class BossDefinition : IRegistryEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ZoneId { get; init; }
    public required char Glyph { get; init; }
    public required Color Color { get; init; }
    public required int BaseHealth { get; init; }
    public required string Tagline { get; init; }

    // Item id of the signature drop offered if the player chooses LOOT
    // over binding the Echo. Boss drops scale with loop tier.
    public required string SignatureLootId { get; init; }

    // Companion id offered if the player chooses BIND over taking the loot.
    public required string EchoPactCompanionId { get; init; }

    public string? LoreSummary { get; init; }
}
