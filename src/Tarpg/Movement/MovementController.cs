using System.Numerics;
using Tarpg.Core;
using Tarpg.Entities;
using Tarpg.World;

namespace Tarpg.Movement;

// Continuous Diablo-style movement on a tile-based world.
//
// Responsibilities:
//  - Track the player's intended destination (set by mouse input).
//  - When the destination has direct line of sight, drift toward it each frame.
//  - When walls block the line, fall back to A* and walk a queue of waypoints
//    (re-checking LOS each tick so we straighten out as soon as the wall clears).
//  - Resolve wall collision via axis-separated tile checks (slide along walls).
public sealed class MovementController
{
    // Player baseline. Enemies override per-EnemyDefinition.MoveSpeed via
    // their AI's constructor.
    public const float DefaultTilesPerSecond = 8f;

    private readonly float _tilesPerSecond;

    public MovementController(float tilesPerSecond = DefaultTilesPerSecond)
    {
        _tilesPerSecond = tilesPerSecond;
    }

    // How close (in tiles) we need to be to a waypoint to consider it reached.
    private const float WaypointArriveDistance = 0.15f;

    // How close we need to be to the final target to "stop".
    private const float TargetArriveDistance = 0.05f;

    // Tiny epsilon used when clamping against walls so the entity's position
    // stays strictly inside the walkable tile after a collision.
    private const float WallEpsilon = 0.001f;

    private Vector2? _finalTarget;
    private readonly Queue<Vector2> _waypoints = new();

    public bool HasGoal => _finalTarget.HasValue || _waypoints.Count > 0;

    public void Stop()
    {
        _finalTarget = null;
        _waypoints.Clear();
    }

    // Set the destination. Called whenever the user moves the cursor while
    // the mouse button is held, or single-clicks somewhere.
    public void RetargetTo(Vector2 worldTarget, Vector2 currentPos, Map map)
    {
        _finalTarget = worldTarget;
        _waypoints.Clear();

        if (TileLineOfSight.HasLineOfSight(map, currentPos, worldTarget))
            return;

        // Wall in the way — fall back to A* on the tile grid.
        var startTile = new Position((int)MathF.Floor(currentPos.X), (int)MathF.Floor(currentPos.Y));
        var endTile = new Position((int)MathF.Floor(worldTarget.X), (int)MathF.Floor(worldTarget.Y));

        // If the click landed on a wall, route to the closest walkable tile
        // we can reach. Cheap version: if endTile is unwalkable, try the four
        // neighbors. If none are reachable (clicked deep inside a wall block,
        // or out of bounds), keep _finalTarget set so the entity drifts
        // straight at the cursor — collision sliding handles the wall, and
        // the player just walks "in that direction" instead of doing nothing.
        if (!map.IsWalkable(endTile))
        {
            var fallback = FindWalkableNeighbor(map, endTile);
            if (fallback is null) return;
            endTile = fallback.Value;
        }

        // A* may still fail if the target is in a disconnected region; same
        // fallback applies — drift toward the click.
        var path = map.FindPath(startTile, endTile);
        if (path is null) return;

        // Defensive: drop the start tile if it appears in the returned
        // path. Map.FindPath already strips it, but if a future caller
        // wires RogueSharp directly the duplicate-start would pin the
        // player (Tick "arrives" at waypoints[0] = current cell every
        // frame and never advances).
        foreach (var step in path)
        {
            if (step.X == startTile.X && step.Y == startTile.Y) continue;
            _waypoints.Enqueue(new Vector2(step.X + 0.5f, step.Y + 0.5f));
        }
    }

    // cellAspect = glyphHeight / glyphWidth. The default IBM 8x16 font is 2.0;
    // a square font is 1.0. We use it to make the player's *visual* speed
    // (pixels per second) uniform regardless of direction. Without it, an
    // 8x16 font makes vertical movement look ~2x faster than horizontal.
    public void Tick(Entity entity, Map map, float deltaSec, float cellAspect = 1.0f)
    {
        if (!HasGoal) return;

        // If we're following waypoints but LOS to the final target has opened up,
        // ditch the waypoints and head straight there.
        if (_waypoints.Count > 0 && _finalTarget.HasValue &&
            TileLineOfSight.HasLineOfSight(map, entity.ContinuousPosition, _finalTarget.Value))
        {
            _waypoints.Clear();
        }

        var goal = NextGoal(entity.ContinuousPosition);
        if (goal is null) { Stop(); return; }

        var toGoal = goal.Value - entity.ContinuousPosition;
        var distance = toGoal.Length();

        var arriveThreshold = _waypoints.Count > 0 ? WaypointArriveDistance : TargetArriveDistance;
        if (distance <= arriveThreshold)
        {
            if (_waypoints.Count > 0) _waypoints.Dequeue();
            else _finalTarget = null;
            return;
        }

        // Aspect-corrected normalization. We treat the world as if vertical
        // tiles were `cellAspect` times "longer" — that way moving 1 vertical
        // tile takes `cellAspect` × the time of moving 1 horizontal tile, and
        // the visual pixel-speed is constant in any direction.
        var dx = toGoal.X;
        var dy = toGoal.Y;
        var correctedDistance = MathF.Sqrt(dx * dx + dy * dy * cellAspect * cellAspect);
        if (correctedDistance < float.Epsilon) return;

        var step = new Vector2(dx, dy) / correctedDistance * _tilesPerSecond * deltaSec;

        // Don't overshoot the goal in a single frame.
        if (step.LengthSquared() > distance * distance)
            step = toGoal;

        entity.ContinuousPosition = ResolveCollision(entity.ContinuousPosition, step, map);
    }

    private Vector2? NextGoal(Vector2 from) =>
        _waypoints.Count > 0 ? _waypoints.Peek() : _finalTarget;

    // Axis-separated tile collision. X is resolved first, then Y based on the
    // new X. Blocked axes clamp the entity flush against the wall edge so the
    // visual rests right next to it (Diablo-style).
    private static Vector2 ResolveCollision(Vector2 from, Vector2 step, Map map)
    {
        var newX = from.X + step.X;
        var tileX = new Position((int)MathF.Floor(newX), (int)MathF.Floor(from.Y));
        if (!map.IsWalkable(tileX))
        {
            newX = step.X > 0
                ? MathF.Floor(from.X) + 1f - WallEpsilon
                : MathF.Floor(from.X) + WallEpsilon;
        }

        var newY = from.Y + step.Y;
        var tileY = new Position((int)MathF.Floor(newX), (int)MathF.Floor(newY));
        if (!map.IsWalkable(tileY))
        {
            newY = step.Y > 0
                ? MathF.Floor(from.Y) + 1f - WallEpsilon
                : MathF.Floor(from.Y) + WallEpsilon;
        }

        return new Vector2(newX, newY);
    }

    private static Position? FindWalkableNeighbor(Map map, Position p)
    {
        if (map.IsWalkable(p.North)) return p.North;
        if (map.IsWalkable(p.South)) return p.South;
        if (map.IsWalkable(p.East))  return p.East;
        if (map.IsWalkable(p.West))  return p.West;
        return null;
    }
}
