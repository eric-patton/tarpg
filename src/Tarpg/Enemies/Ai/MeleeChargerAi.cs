using System.Numerics;
using Tarpg.Entities;
using Tarpg.Movement;
using Tarpg.World;

namespace Tarpg.Enemies.Ai;

// Wolf-style chase-and-bite AI. Reuses the player's MovementController for
// continuous velocity-based motion, falls back to A* through walls.
//
// Aggro model: time-decayed memory. While the player can see this enemy
// (mutual LOS via Map.IsInFov), the aggro timer refreshes to AggroMemorySec
// and we record the player's continuous position as the chase target. When
// the player breaks line of sight, the timer ticks down — during that grace
// period the AI keeps pushing toward the *last* known position. When it
// hits zero the enemy goes idle.
public sealed class MeleeChargerAi : IEnemyAi
{
    private const float AggroMemorySec = 3.0f;
    private const float MeleeRange = 1.4f;

    private readonly MovementController _movement;
    private readonly float _attackCooldownSec;
    private float _aggroRemaining;
    private Vector2 _lastSeenPlayerPos;
    private bool _hasMemory;
    private float _attackCooldown;

    public MeleeChargerAi(EnemyDefinition def)
    {
        _movement = new MovementController(def.MoveSpeed);
        _attackCooldownSec = def.AttackCooldown;
    }

    public void Tick(Enemy self, Player player, Map map, float deltaSec, float cellAspect)
    {
        // FOV is computed from the player's perspective. By symmetry, if the
        // wolf's tile is in the player's FOV then there's an unobstructed
        // line between them — equivalent to "the wolf can see the player."
        if (map.IsInFov(self.Position))
        {
            _aggroRemaining = AggroMemorySec;
            _lastSeenPlayerPos = player.ContinuousPosition;
            _hasMemory = true;
        }
        else if (_aggroRemaining > 0f)
        {
            _aggroRemaining = MathF.Max(0f, _aggroRemaining - deltaSec);
        }

        if (_attackCooldown > 0f)
            _attackCooldown = MathF.Max(0f, _attackCooldown - deltaSec);

        if (!_hasMemory || _aggroRemaining <= 0f)
        {
            _movement.Stop();
            return;
        }

        var distanceToTarget = Vector2.Distance(self.ContinuousPosition, _lastSeenPlayerPos);
        if (distanceToTarget > MeleeRange)
        {
            _movement.RetargetTo(_lastSeenPlayerPos, self.ContinuousPosition, map);
            _movement.Tick(self, map, deltaSec, cellAspect);
            return;
        }

        // In melee range. Hold position; swing on cooldown but only if the
        // player is currently visible — otherwise we'd be hitting empty air
        // at a stale last-seen tile.
        _movement.Stop();
        if (_attackCooldown <= 0f && map.IsInFov(self.Position))
        {
            player.TakeDamage(self.Damage);
            _attackCooldown = _attackCooldownSec;
        }
    }
}
