using System.Numerics;
using Tarpg.Core;
using Tarpg.Entities;
using Tarpg.Movement;
using Tarpg.World;

namespace Tarpg.Sim;

// Hunter-flavored pilot: maintain a kite-band distance from the nearest
// enemy, fire QuickShot from range, retreat (Roll if available, walk-back
// otherwise) when an enemy crosses into the danger zone, drop AOE skills
// on dense clusters, Bandage at low HP. Walks toward the threshold when
// no enemies remain so the run terminates with PlayerCleared.
//
// The kiter deliberately does NOT use the auto-attack system: it never
// calls Combat.SetTarget. The reason is that GameLoopController, when a
// combat target is set, drives Movement.RetargetTo at the target every
// tick — which directly fights kiting. All damage is therefore from
// skills (QuickShot is the primary; Volley / Rain of Arrows on clusters;
// Roll for emergency disengage). At Hunter's numbers QuickShot's 12 dmg
// / 0.5s cd is roughly 2x the auto-attack's DPS anyway, so the kit
// doesn't lose much by skipping the swing.
//
// Stuck detector: same shape as GreedySimPilot, but explicitly reset
// whenever the kiter is intentionally standing still to fire — without
// that, every kite-band hold would falsely tick toward the bail trigger.
public sealed class KitingSimPilot : ISimPilot
{
    private const int StuckThresholdTicks = 60;

    // Below this chebyshev distance the kiter must retreat right now.
    // Inside this band the player can be hit by melee on the very next
    // enemy tick, so standing still is unsafe even with LOS.
    private const int DangerDistance = 2;

    // Sweet spot for QuickShot: max range matches the skill's MaxRange,
    // min sits a couple tiles outside the danger zone so the player has a
    // buffer turn to react to a fast melee enemy closing in.
    private const int IdealKiteDistanceMin = 3;
    private const int IdealKiteDistanceMax = 6;

    // How far to flee per retreat order. Long enough that one RetargetTo
    // covers several enemy steps; short enough that we don't yo-yo across
    // the floor when the threat respawns aggro.
    private const float RetreatTiles = 6f;

    // Heal threshold. 50% is when surviving a single dire-wolf swing on
    // a deep floor stops being trivially regenable, so it's the natural
    // "use the bandage" trigger.
    private const float BandageHpFraction = 0.5f;

    // Panic potion drink. Bandage at 25 Focus / 12s cd isn't always
    // available; the potion is the last-ditch heal below this fraction.
    // Below the Bandage trigger so a healthy panic-drink doesn't waste
    // a charge on a ~50% HP scratch.
    private const float PotionPanicHpFraction = 0.3f;

    // Sticky-target hysteresis: candidate must be < 75% of current target's
    // distance² to override. Prevents per-tick target swap thrash from
    // tiny player jitter — same reasoning as GreedySimPilot.
    private const float StickyOverrideRatio = 0.75f;

    // Cluster thresholds for AOE casts. We pick each enemy as a candidate
    // cluster center, count enemies within `radius` chebyshev of it, and
    // fire if that count clears the threshold and the center is within
    // skill range. O(N²) per tick is fine for the floor-cap of ~25 enemies.
    private const int VolleyClusterMin = 2;
    private const int VolleyRadius = 1;
    private const int VolleyMaxRange = 6;

    private const int RainClusterMin = 3;
    private const int RainRadius = 2;
    private const int RainMaxRange = 8;

    // QuickShot's own MaxRangeFromCaster. Kept here as a guard so we don't
    // burn the 0.5s cooldown on out-of-range casts during the close-in
    // branch — the skill itself silently no-ops past 6 chebyshev.
    private const int QuickShotMaxRange = 6;

    // Commit-fight fallback: when wall geometry pins the kiter (enemy at
    // cheby ≤ DangerDistance with no LOS), the pilot oscillates between
    // "retreat into wall" and "close in to gain LOS" — one tile in either
    // direction, no progress. After this many ticks of that state we
    // commit to a melee engagement: set Combat.Target so the loop chases
    // + auto-attacks. Resets when the target dies or LOS reopens (so we
    // can kite again the moment geometry permits).
    //
    // Threshold is well below the stuck-bail (60 ticks → walk to threshold)
    // — sequence is: pin → 3 sim-sec → commit-fight → engagement progresses
    // → if STILL no progress, the bail eventually fires as a backstop.
    private const int CommitFightThresholdTicks = 180;

    private bool _initialized;
    private Position _lastPosition;
    private int _ticksSinceMove;
    private bool _bailingToThreshold;
    private Enemy? _stickyTarget;
    private int _ticksInDangerWithoutLos;
    private bool _commitFighting;

    public void Tick(SimContext ctx)
    {
        var loop = ctx.Loop;
        var player = loop.Player;
        var enemies = loop.Enemies;
        var map = loop.Map;

        if (_initialized)
        {
            if (player.Position != _lastPosition)
            {
                _ticksSinceMove = 0;
                _bailingToThreshold = false;
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
            loop.Combat.Clear();
            RouteTo(loop, ctx.FloorThreshold, player.ContinuousPosition);
            return;
        }

        var nearest = NearestLiveEnemy(player, enemies);
        if (nearest is null)
        {
            _stickyTarget = null;
            ResetCommitFight(loop);
            RouteTo(loop, ctx.FloorThreshold, player.ContinuousPosition);
            return;
        }

        // Sticky target hysteresis (see GreedySimPilot for the regression
        // this prevents — equal-distance enemies thrashing per tick).
        if (_stickyTarget is { IsDead: false }
            && !ReferenceEquals(_stickyTarget, nearest))
        {
            var currentDistSq = Vector2.DistanceSquared(player.ContinuousPosition, _stickyTarget.ContinuousPosition);
            var nearestDistSq = Vector2.DistanceSquared(player.ContinuousPosition, nearest.ContinuousPosition);
            if (nearestDistSq > currentDistSq * StickyOverrideRatio)
                nearest = _stickyTarget;
        }
        _stickyTarget = nearest;

        // Commit-fight bookkeeping. Track ticks the threat has been
        // close-but-not-LOS-reachable — the pin pattern. Cheby ≤ 3
        // (DangerDistance + 1) catches the 1-tile oscillation that
        // happens when the pilot toggles between "retreat" (cheby=2)
        // and "close in" (cheby=3) every tick on a corridor — staying
        // strictly at cheby ≤ DangerDistance would never trip while
        // the pilot is actively oscillating.
        var cheby = player.Position.ChebyshevTo(nearest.Position);
        var losToTarget = HasLos(map, player.ContinuousPosition, nearest.Position);
        if (cheby <= IdealKiteDistanceMin && !losToTarget)
            _ticksInDangerWithoutLos++;
        else
            _ticksInDangerWithoutLos = 0;

        if (_ticksInDangerWithoutLos >= CommitFightThresholdTicks)
            _commitFighting = true;

        // Exit commit-fight when the threat dies (handled by stickyTarget
        // reassign next tick) OR when we've recovered LOS (we can kite
        // again). Auto-attack chase keeps running until then so the
        // engagement actually closes — even one swing landed lets the
        // loop's auto-attack continue while the pilot reassesses.
        if (_commitFighting && losToTarget)
            ResetCommitFight(loop);

        if (_commitFighting)
        {
            // Drive a melee engagement: set Combat.Target so the loop
            // chases + auto-attacks. Pilot stays out of Movement entirely
            // — letting the loop drive prevents the retreat/close
            // oscillation that pinned us in the first place.
            if (!ReferenceEquals(loop.Combat.Target, nearest))
                loop.Combat.SetTarget(nearest);
            return;
        }

        // Outside commit-fight, kiter never uses auto-attack. Clear any
        // residual combat target so the loop doesn't override movement
        // with chase logic.
        if (loop.Combat.Target is not null)
            loop.Combat.Clear();

        // Heal early — a successful Bandage doesn't move us, so the rest
        // of the tick's positional logic still runs after. Panic-drink
        // the HP potion below an even lower threshold so it stays a
        // last-ditch option (the kit's regen + Bandage handles the
        // common case).
        var hpFraction = (float)player.Health / Math.Max(1, player.MaxHealth);
        if (hpFraction <= BandageHpFraction)
            TryCast(ctx, GameLoopController.SlotIndexE, player.Position);
        if (hpFraction <= PotionPanicHpFraction)
            ctx.Loop.TryDrinkHealthPotion();

        // AOE on dense clusters. Both fire independently (different slots
        // / cooldowns / costs) — the cooldown / resource gates inside
        // TryCastSkill handle the "we cast it last tick" case naturally.
        var rainTarget = FindClusterCenter(enemies, RainClusterMin, RainRadius);
        if (rainTarget is { } rt
            && player.Position.ChebyshevTo(rt) <= RainMaxRange)
        {
            TryCast(ctx, GameLoopController.SlotIndexR, rt);
        }

        var volleyTarget = FindClusterCenter(enemies, VolleyClusterMin, VolleyRadius);
        if (volleyTarget is { } vt
            && player.Position.ChebyshevTo(vt) <= VolleyMaxRange
            && HasLos(map, player.ContinuousPosition, vt))
        {
            TryCast(ctx, GameLoopController.SlotIndexQ, vt);
        }

        // Position decision. If we're in the danger zone, try Roll first;
        // a successful Roll snaps us out of melee range so subsequent
        // logic can drop straight into the kite-band branch.
        var threatCheby = player.Position.ChebyshevTo(nearest.Position);
        if (threatCheby <= DangerDistance)
        {
            TryCast(ctx, GameLoopController.SlotIndexW, nearest.Position);
            threatCheby = player.Position.ChebyshevTo(nearest.Position);

            if (threatCheby <= DangerDistance)
            {
                // Roll on cooldown / no Focus — flee on foot.
                var retreatGoal = ComputeRetreatGoal(player.ContinuousPosition, nearest.ContinuousPosition);
                loop.Movement.RetargetTo(retreatGoal, player.ContinuousPosition, map);
                if (HasLos(map, player.ContinuousPosition, nearest.Position))
                    TryCast(ctx, GameLoopController.SlotIndexM2, nearest.Position);
                return;
            }
        }

        var hasLos = HasLos(map, player.ContinuousPosition, nearest.Position);

        if (threatCheby >= IdealKiteDistanceMin
            && threatCheby <= IdealKiteDistanceMax
            && hasLos)
        {
            // Kite band, LOS clear — hold position and shoot. Reset the
            // stuck counter explicitly: standing still here is intentional,
            // not a deadlock.
            loop.Movement.Stop();
            _ticksSinceMove = 0;
            TryCast(ctx, GameLoopController.SlotIndexM2, nearest.Position);
            return;
        }

        if (threatCheby > IdealKiteDistanceMax || !hasLos)
        {
            // Too far / no LOS — close in. Fire opportunistically if LOS
            // happens to be clear AND the target is within QuickShot's
            // own range gate (cheby ≤ 6) — otherwise the cast no-ops in
            // the skill behavior and burns the cooldown for nothing.
            loop.Movement.RetargetTo(nearest.ContinuousPosition, player.ContinuousPosition, map);
            if (hasLos && threatCheby <= QuickShotMaxRange)
                TryCast(ctx, GameLoopController.SlotIndexM2, nearest.Position);
            return;
        }

        // Between DangerDistance and IdealKiteDistanceMin — too close to
        // hold but Roll wasn't available / didn't move us far enough.
        // Walk away on foot.
        var fallbackGoal = ComputeRetreatGoal(player.ContinuousPosition, nearest.ContinuousPosition);
        loop.Movement.RetargetTo(fallbackGoal, player.ContinuousPosition, map);
        if (hasLos && threatCheby <= QuickShotMaxRange)
            TryCast(ctx, GameLoopController.SlotIndexM2, nearest.Position);
    }

    private void ResetCommitFight(GameLoopController loop)
    {
        if (_commitFighting && loop.Combat.Target is not null)
            loop.Combat.Clear();
        _commitFighting = false;
        _ticksInDangerWithoutLos = 0;
    }

    private static Vector2 ComputeRetreatGoal(Vector2 playerPos, Vector2 threatPos)
    {
        var dir = playerPos - threatPos;
        if (dir.LengthSquared() < 0.01f)
        {
            // Stacked on the threat — pick an arbitrary direction so
            // RetargetTo gets a well-defined goal instead of NaN.
            dir = new Vector2(1, 0);
        }
        else
        {
            dir = Vector2.Normalize(dir);
        }
        return playerPos + dir * RetreatTiles;
    }

    private static void RouteTo(GameLoopController loop, Position tile, Vector2 fromContinuous)
    {
        var goal = new Vector2(tile.X + 0.5f, tile.Y + 0.5f);
        loop.Movement.RetargetTo(goal, fromContinuous, loop.Map);
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

    // Pick the enemy whose chebyshev neighborhood holds the most live
    // enemies (including itself). Returns null if no enemy clears the
    // minimum-cluster threshold. Ties go to the first-seen enemy in the
    // list — deterministic given the enemy list ordering.
    private static Position? FindClusterCenter(IReadOnlyList<Enemy> enemies, int minEnemies, int radius)
    {
        var bestCount = minEnemies - 1;
        Position? bestCenter = null;
        foreach (var center in enemies)
        {
            if (center.IsDead) continue;
            var count = 0;
            foreach (var other in enemies)
            {
                if (other.IsDead) continue;
                if (center.Position.ChebyshevTo(other.Position) <= radius) count++;
            }
            if (count > bestCount)
            {
                bestCount = count;
                bestCenter = center.Position;
            }
        }
        return bestCenter;
    }

    private static bool HasLos(Map map, Vector2 from, Position toTile)
    {
        var to = new Vector2(toTile.X + 0.5f, toTile.Y + 0.5f);
        return TileLineOfSight.HasLineOfSight(map, from, to);
    }

    private static bool HasLos(Map map, Vector2 from, Vector2 to)
        => TileLineOfSight.HasLineOfSight(map, from, to);

    private static void TryCast(SimContext ctx, int slot, Position target)
    {
        var result = ctx.Loop.TryCastSkill(slot, target, vfx: null);
        if (result.Success) ctx.SkillCastsThisRun++;
    }
}
