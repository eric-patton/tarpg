using System.Numerics;
using Tarpg.Core;
using Tarpg.World;

namespace Tarpg.Movement;

// Whether a straight line from `from` to `to` passes only through walkable tiles.
// Used to decide whether the player can drift directly toward the cursor or
// whether we need to fall back to A* waypoints.
public static class TileLineOfSight
{
    // Step size in tiles. Smaller = more accurate but slower. 0.25 means we
    // sample at least 4 points per tile, which is enough to never skip a wall
    // unless the wall is thinner than 0.25 tiles (we have no such walls).
    private const float StepTiles = 0.25f;

    public static bool HasLineOfSight(Map map, Vector2 from, Vector2 to)
    {
        var distance = Vector2.Distance(from, to);
        if (distance <= float.Epsilon) return true;

        var stepCount = (int)MathF.Ceiling(distance / StepTiles);

        for (var i = 0; i <= stepCount; i++)
        {
            var t = i / (float)stepCount;
            var p = Vector2.Lerp(from, to, t);
            var tile = new Position((int)MathF.Floor(p.X), (int)MathF.Floor(p.Y));
            if (!map.IsWalkable(tile)) return false;
        }

        return true;
    }
}
