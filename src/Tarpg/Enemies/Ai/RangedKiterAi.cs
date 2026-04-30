using System.Numerics;
using Tarpg.Entities;
using Tarpg.Movement;
using Tarpg.World;

namespace Tarpg.Enemies.Ai;

// Maintains a preferred distance band from the player and fires hitscan
// damage on cooldown when in LOS + range. Closes the gap when the player
// is too far or LOS is broken; backpedals when the player closes inside the
// preferred band. No projectile entity — damage is instantaneous on the
// player. The damage number + flash from HitFeedback is the visual cue.
//
// Aggro model and FOV-symmetric LOS match the other AIs. Movement uses the
// actor's per-EnemyDefinition MoveSpeed.
public sealed class RangedKiterAi : IEnemyAi
{
    private const float AggroMemorySec = 3.0f;
    private const float AttackRangeTiles = 6.0f;
    private const float PreferredDistanceMin = 4.0f;
    private const float PreferredDistanceMax = 6.0f;
    private const float BackpedalDistanceTiles = 3.0f;

    private readonly MovementController _movement;
    private readonly float _attackCooldownSec;
    private float _aggroRemaining;
    private Vector2 _lastSeenPlayerPos;
    private bool _hasMemory;
    private float _attackCooldown;

    public RangedKiterAi(EnemyDefinition def)
    {
        _movement = new MovementController(def.MoveSpeed);
        _attackCooldownSec = def.AttackCooldown;
    }

    public void Tick(Enemy self, Player player, Map map, float deltaSec, float cellAspect)
    {
        var hasLos = map.IsInFov(self.Position);
        if (hasLos)
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

        var distance = Vector2.Distance(self.ContinuousPosition, _lastSeenPlayerPos);

        // Movement: kite at the preferred band. Backpedal when the player is
        // inside the band; advance when too far or no LOS; hold otherwise.
        if (distance < PreferredDistanceMin)
        {
            var awayDir = self.ContinuousPosition - _lastSeenPlayerPos;
            awayDir = awayDir.LengthSquared() < 0.001f
                ? new Vector2(1f, 0f)
                : Vector2.Normalize(awayDir);
            var retreatTarget = self.ContinuousPosition + awayDir * BackpedalDistanceTiles;
            _movement.RetargetTo(retreatTarget, self.ContinuousPosition, map);
            _movement.Tick(self, map, deltaSec, cellAspect);
        }
        else if (distance > PreferredDistanceMax || !hasLos)
        {
            _movement.RetargetTo(_lastSeenPlayerPos, self.ContinuousPosition, map);
            _movement.Tick(self, map, deltaSec, cellAspect);
        }
        else
        {
            _movement.Stop();
        }

        // Fire on cooldown when in range and LOS. Hitscan: instant damage,
        // no projectile entity. The HitFeedback flash + drifting damage
        // number on the player provides the "you got shot" cue.
        if (_attackCooldown <= 0f && hasLos && distance <= AttackRangeTiles)
        {
            player.TakeDamage(self.Damage);
            _attackCooldown = _attackCooldownSec;
        }
    }
}
