using System.Numerics;
using Tarpg.Core;
using Tarpg.Combat;
using Tarpg.Entities;

namespace Tarpg.Sim;

// Picks the nearest live enemy, walks into melee range, and lets the
// loop's auto-attack handle the swing. Fires Q (Cleave) when 2+ enemies
// are within chebyshev-1 of the player; fires R (Whirlwind) when 3+ are
// within chebyshev-2. Walks toward the floor's threshold when no enemies
// remain so the run terminates with PlayerCleared.
//
// This is a "pressure-test" pilot — not optimal play. It exercises the
// combat / AI / regen / skill systems end-to-end so we can tune enemies,
// classes, items by aggregate outcome rather than first-principles math.
public sealed class GreedySimPilot : ISimPilot
{
    public void Tick(SimContext ctx)
    {
        var loop = ctx.Loop;
        var player = loop.Player;
        var enemies = loop.Enemies;

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

        // Re-target every tick so the pilot tracks the closest enemy as the
        // fight evolves (the wolf in front died → switch to the next pack
        // member without waiting for stale state to clear).
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
