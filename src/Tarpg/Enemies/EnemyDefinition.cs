using SadRogue.Primitives;
using Tarpg.Core;

namespace Tarpg.Enemies;

public sealed class EnemyDefinition : IRegistryEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required char Glyph { get; init; }
    public required Color Color { get; init; }
    public required int BaseHealth { get; init; }
    public required int BaseDamage { get; init; }

    // Tag identifies the AI archetype (e.g. "melee_charger", "ranged_kiter",
    // "ambusher"). The AI system maps tags to behaviors so new enemies can
    // reuse existing brains without writing code.
    public required string AiTag { get; init; }

    // Which zone(s) this enemy can spawn in. Zone ids match the design
    // doc themes (wolfwood, drowned_hall, hollow_court, etc.).
    public required IReadOnlyList<string> ZoneIds { get; init; }

    // 0 = infinitely common; higher = rarer. Drop tables use this for weights.
    public int RarityWeight { get; init; } = 1;

    public string? FlavorText { get; init; }
}
