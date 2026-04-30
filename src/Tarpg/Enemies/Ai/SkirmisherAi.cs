using System.Numerics;
using Tarpg.Entities;
using Tarpg.Movement;
using Tarpg.World;

namespace Tarpg.Enemies.Ai;

// Bite-and-retreat melee. Like MeleeChargerAi the actor chases the player
// into melee and swings, but immediately after a successful swing it picks
// a tile RetreatDistance away from the player and drifts there for
// RetreatDurationSec — opening a gap, then re-engaging. The pacing creates
// a fishing-for-openings rhythm rather than the constant grind of a charger.
//
// Aggro model is shared with MeleeChargerAi (FOV-symmetric mutual LOS, time
// decay on memory). Movement uses the actor's per-EnemyDefinition MoveSpeed.
public sealed class SkirmisherAi : IEnemyAi
{
    private const float AggroMemorySec = 3.0f;
    private const float MeleeRange = 1.4f;
    private const float RetreatDistanceTiles = 4.0f;
    private const float RetreatDurationSec = 0.6f;

    private readonly MovementController _movement;
    private readonly float _attackCooldownSec;
    private float _aggroRemaining;
    private Vector2 _lastSeenPlayerPos;
    private bool _hasMemory;
    private float _attackCooldown;
    private float _retreatRemaining;
    private Vector2 _retreatTarget;

    public SkirmisherAi(EnemyDefinition def)
    {
        _movement = new MovementController(def.MoveSpeed);
        _attackCooldownSec = def.AttackCooldown;
    }

    public void Tick(Enemy self, Player player, Map map, float deltaSec, float cellAspect)
    {
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

        if (_retreatRemaining > 0f)
            _retreatRemaining = MathF.Max(0f, _retreatRemaining - deltaSec);

        if (!_hasMemory || _aggroRemaining <= 0f)
        {
            _movement.Stop();
            return;
        }

        // Retreat phase wins over chase: even if the player is right next to
        // us, we hold the gap. RetargetTo uses drift-on-unreachable so the
        // retreat target being inside a wall just means we slide along it.
        if (_retreatRemaining > 0f)
        {
            _movement.RetargetTo(_retreatTarget, self.ContinuousPosition, map);
            _movement.Tick(self, map, deltaSec, cellAspect);
            return;
        }

        var distanceToTarget = Vector2.Distance(self.ContinuousPosition, _lastSeenPlayerPos);
        if (distanceToTarget > MeleeRange)
        {
            _movement.RetargetTo(_lastSeenPlayerPos, self.ContinuousPosition, map);
            _movement.Tick(self, map, deltaSec, cellAspect);
            return;
        }

        _movement.Stop();
        if (_attackCooldown <= 0f && map.IsInFov(self.Position))
        {
            player.TakeDamage(self.Damage);
            _attackCooldown = _attackCooldownSec;

            // Pick a retreat point RetreatDistance tiles away in the direction
            // away from the player. If we're directly on the player (degenerate
            // overlap), fall back to retreating east.
            var awayDir = self.ContinuousPosition - player.ContinuousPosition;
            awayDir = awayDir.LengthSquared() < 0.001f
                ? new Vector2(1f, 0f)
                : Vector2.Normalize(awayDir);
            _retreatTarget = self.ContinuousPosition + awayDir * RetreatDistanceTiles;
            _retreatRemaining = RetreatDurationSec;
        }
    }
}
