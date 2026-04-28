using Tarpg.Core;

namespace Tarpg.World.Generation;

// Result of an IZoneGenerator.Generate call. Carries the carved Map plus the
// gameplay anchors GameScreen needs to spawn the player and enemies on it.
public sealed class GeneratedFloor
{
    public required Map Map { get; init; }
    public required Position Entry { get; init; }
    public required Position BossAnchor { get; init; }
    public required IReadOnlyList<Position> EnemySpawnPoints { get; init; }
}
