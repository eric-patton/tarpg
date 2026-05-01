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

    // Movement speed in tiles/second. Player baseline is 8 (see GameScreen).
    // Standard wolves are ~6, fast horde swarmers ~7, slow elites ~4–5.
    public float MoveSpeed { get; init; } = 6.0f;

    // Seconds between successive attack swings / shots for this enemy. Maps
    // to the "attack rate" stat — lower = faster attacker.
    public float AttackCooldown { get; init; } = 0.8f;

    // Pack size for horde-tier units. The spawn pipeline places this many
    // copies around each BSP-chosen spawn point (filling outward by chebyshev
    // ring through walkable tiles). Default 1 = solo enemy.
    public int PackSize { get; init; } = 1;

    // Tag identifies the AI archetype (e.g. "melee_charger", "ranged_kiter",
    // "melee_skirmisher"). The AI system maps tags to behaviors so new
    // enemies can reuse existing brains without writing code.
    public required string AiTag { get; init; }

    // Which zone(s) this enemy can spawn in. Zone ids match the design
    // doc themes (wolfwood, drowned_hall, hollow_court, etc.).
    public required IReadOnlyList<string> ZoneIds { get; init; }

    // Spawn-roll weight. Higher = more likely to roll. Standard weighted-pick
    // semantics: an enemy with weight 4 spawns 4x as often as a weight-1 enemy
    // in the same zone. 0 disables spawn rolls (e.g., quest- or boss-only).
    public int RarityWeight { get; init; } = 1;

    // Boss flag. Used by the loop's boss-death handler to convert the
    // floor's BossAnchor tile to Threshold (= unlock descent) when this
    // enemy dies. Regular enemies have no such effect on terrain.
    public bool IsBoss { get; init; } = false;

    public string? FlavorText { get; init; }
}
