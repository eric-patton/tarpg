using Tarpg.Entities;
using Tarpg.World;

namespace Tarpg.Enemies.Ai;

// Per-tick brain for an enemy. Resolved from EnemyDefinition.AiTag at the
// time the Enemy is created (see Enemy.Create). Each enemy holds its own
// IEnemyAi instance so per-actor state (aggro timer, last-seen tile,
// cooldowns) lives here, not on Enemy itself.
public interface IEnemyAi
{
    // self        — the enemy this AI drives.
    // player      — the threat reference. AIs only know about the player today.
    // map         — for FOV / pathfinding queries.
    // deltaSec    — frame time in seconds.
    // cellAspect  — passed through to MovementController for visual-pixel
    //               speed correction at non-square cells.
    void Tick(Enemy self, Player player, Map map, float deltaSec, float cellAspect);
}
