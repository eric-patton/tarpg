using Tarpg.Core;
using Tarpg.World.Generation;

namespace Tarpg.World;

// Registry entry for a mythic zone (Wolfwood, Drowned Hall, etc.). Holds the
// strategy reference used to generate floors. Enemy / boss roster is queried
// via the existing ZoneId fields on EnemyDefinition / BossDefinition rather
// than duplicated here.
public sealed class ZoneDefinition : IRegistryEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required IZoneGenerator Generator { get; init; }
}
