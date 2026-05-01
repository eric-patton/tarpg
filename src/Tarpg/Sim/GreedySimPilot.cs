using System.Numerics;
using Tarpg.Core;
using Tarpg.Combat;
using Tarpg.Entities;

namespace Tarpg.Sim;

// Picks the nearest live enemy, walks into melee range, and lets the
// loop's auto-attack handle the swing. Fires Q (Cleave) when 2+ enemies
// are within chebyshev-1 of the player; fires R (Whirlwind) when 3+ are
// within chebyshev-2. Fires E (War Cry) when HP drops below WarCryHpFraction
// to use the kit's only proactive heal. Drinks an HP potion below the
// PotionPanicHpFraction emergency line. Walks toward the floor's threshold
// when no enemies remain so the run terminates with PlayerCleared.
//
// Stuck detector: A* + wall-slide can leave the pilot's player parked
// against a corner with combat target still alive but unreachable. After
// StuckThresholdTicks of zero movement the pilot drops its target and
// switches to "head for the threshold" mode permanently — if it can
// reach the threshold the run ends as Cleared, otherwise the genuine
// "this floor has unreachable enemies for this pilot" timeout fires.
//
// This is a "pressure-test" pilot — not optimal play. It exercises the
// combat / AI / regen / skill systems end-to-end so we can tune enemies,
// classes, items by aggregate outcome rather than first-principles math.
public sealed class GreedySimPilot : ISimPilot
{
    // Player normally moves ~8 tiles/sim-sec; a tile transition fires every
    // ~7 ticks at 60Hz. 60 ticks (1 sim-second) of zero tile-transitions is
    // unambiguously stuck.
    private const int StuckThresholdTicks = 60;

    // Heal-trigger HP fraction. War Cry restores 25 HP at 12s cd / 25 Rage,
    // so firing it at 50% buys back roughly a full Rage-bar's worth of HP
    // over the cooldown. Below this, fire on every cooldown tick.
    private const float WarCryHpFraction = 0.5f;

    // Emergency potion drink. War Cry isn't always available (cooldown,
    // resource), so the panic threshold sits below the War Cry threshold —
    // potion is the last-ditch heal, not the primary one.
    private const float PotionPanicHpFraction = 0.3f;

    private bool _initialized;
    private Position _lastPosition;
    private int _ticksSinceMove;
    private bool _bailingToThreshold;

    public void Tick(SimContext ctx)
    {
        var loop = ctx.Loop;
        var player = loop.Player;
        var enemies = loop.Enemies;

        // Track tile-level movement for stuck detection. The tick where
        // we initialize doesn't count toward the counter — otherwise the
        // first tick falsely registers as "no movement."
        if (_initialized)
        {
            if (player.Position != _lastPosition)
            {
                _ticksSinceMove = 0;
                _bailingToThreshold = false; // re-engage if pilot is unstuck
            }
            else
            {
                _ticksSinceMove++;
            }
        }
        _lastPosition = player.Position;
        _initialized = true;

        if (_ticksSinceMove >= StuckThresholdTicks)
            _bailingToThreshold = true;

        if (_bailingToThreshold)
        {
            // Drop combat + walk to threshold. If the floor is reachable,
            // run ends as Cleared. If the threshold is also unreachable,
            // the run hits MaxTicks and records as Timeout — which now
            // means "genuinely couldn't navigate" instead of "pilot got
            // stuck on one enemy."
            if (loop.Combat.Target is not null) loop.Combat.Clear();
            var threshold = ctx.FloorThreshold;
            var goal = new Vector2(threshold.X + 0.5f, threshold.Y + 0.5f);
            loop.Movement.RetargetTo(goal, player.ContinuousPosition, loop.Map);
            return;
        }

        // Drop dead-target combat refs — controller would clear next tick anyway.
        if (loop.Combat.Target is { IsDead: true })
            loop.Combat.Clear();

        var nearest = NearestLiveEnemy(player, enemies);

        if (nearest is null)
        {
            // No enemies left: head for the threshold to clear the floor.
            loop.Combat.Clear();
            var threshold = ctx.FloorThreshold;
            var goal = new Vector2(threshold.X + 0.5f, threshold.Y + 0.5f);
            loop.Movement.RetargetTo(goal, player.ContinuousPosition, loop.Map);
            return;
        }

        // Sticky targeting with hysteresis. Without this, two enemies at
        // nearly-equal Euclidean distance cause the pilot to swap "nearest"
        // every tick as the player jitters by 0.1 tile, which makes
        // RetargetTo build conflicting paths (one north, one south) on
        // alternating frames — net player motion is zero. With this gate
        // we stick to the current target until it dies OR a candidate is
        // at least 25% closer than the current pick.
        if (loop.Combat.Target is Enemy current && !current.IsDead)
        {
            var currentDistSq = Vector2.DistanceSquared(player.ContinuousPosition, current.ContinuousPosition);
            var nearestDistSq = Vector2.DistanceSquared(player.ContinuousPosition, nearest.ContinuousPosition);
            if (nearestDistSq > currentDistSq * 0.75f)
                nearest = current;
        }

        if (!ReferenceEquals(loop.Combat.Target, nearest))
            loop.Combat.SetTarget(nearest);

        // Skill heuristics: count nearby enemies (chebyshev) and fire AOEs
        // when the cluster is dense enough to make them efficient.
        var adjacentCount = CountWithinChebyshev(player, enemies, 1);
        var nearbyCount = CountWithinChebyshev(player, enemies, 2);

        if (nearbyCount >= 3)
            TryCast(ctx, GameLoopController.SlotIndexR);
        if (adjacentCount >= 2)
            TryCast(ctx, GameLoopController.SlotIndexQ);

        // M2 (Heavy Strike) when single-target and target adjacent — small,
        // free swing that punches above auto-attack DPS at melee range.
        if (adjacentCount == 1)
            TryCast(ctx, GameLoopController.SlotIndexM2, nearest.Position);

        // Defensive cooldowns. War Cry is the proactive heal; the potion
        // is the panic button when War Cry's on cooldown / out of Rage.
        // Both gate-checks (resource cost, drink cooldown, inventory
        // count) live inside their respective TryX methods, so calling
        // them every tick that the threshold trips is safe and cheap.
        var hpFraction = (float)player.Health / Math.Max(1, player.MaxHealth);
        if (hpFraction <= WarCryHpFraction)
            TryCast(ctx, GameLoopController.SlotIndexE);
        if (hpFraction <= PotionPanicHpFraction)
            ctx.Loop.TryDrinkHealthPotion();
    }

    private static Enemy? NearestLiveEnemy(Player player, IReadOnlyList<Enemy> enemies)
    {
        Enemy? best = null;
        var bestDistSq = float.MaxValue;
        foreach (var enemy in enemies)
        {
            if (enemy.IsDead) continue;
            var d = Vector2.DistanceSquared(player.ContinuousPosition, enemy.ContinuousPosition);
            if (d < bestDistSq) { best = enemy; bestDistSq = d; }
        }
        return best;
    }

    private static int CountWithinChebyshev(Player player, IReadOnlyList<Enemy> enemies, int radius)
    {
        var count = 0;
        var pp = player.Position;
        foreach (var enemy in enemies)
        {
            if (enemy.IsDead) continue;
            if (pp.ChebyshevTo(enemy.Position) <= radius) count++;
        }
        return count;
    }

    private static void TryCast(SimContext ctx, int slot, Position? target = null)
    {
        var aim = target ?? ctx.Loop.Player.Position;
        var result = ctx.Loop.TryCastSkill(slot, aim, vfx: null);
        if (result.Success) ctx.SkillCastsThisRun++;
    }
}
