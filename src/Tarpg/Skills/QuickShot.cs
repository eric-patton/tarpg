using System.Numerics;
using SadRogue.Primitives;
using Tarpg.Core;
using Tarpg.Entities;
using Tarpg.Movement;

namespace Tarpg.Skills;

// Hunter's M2 — fast hitscan single-target shot. Reads the cursor cell as
// the aim point, picks the nearest live enemy within TargetRadius (so a
// near-miss click still connects, matching the click-target-radius rule),
// then hitscan-damages it iff there's an unobstructed line from the caster.
//
// Resource cost is zero — M2 is a primary input the Hunter should mash to
// keep pressure between bigger skills. Cooldown is the only gate.
public static class QuickShot
{
    private const int Damage = 12;
    private const int MaxRangeFromCaster = 6; // chebyshev tiles
    private const int TargetRadius = 1;       // chebyshev around clicked tile

    public static readonly SkillDefinition Definition = new()
    {
        Id = "quick_shot",
        Name = "Quick Shot",
        Description = "Loose a fast arrow at the cursor.",
        Resource = ResourceType.Focus,
        Cost = 0,
        CooldownSec = 0.5f,
        Glyph = '\'',
        Behavior = new QuickShotBehavior(),
    };

    private sealed class QuickShotBehavior : ISkillBehavior
    {
        private static readonly Color HighlightColor = new(120, 220, 100);

        public void Execute(SkillContext ctx)
        {
            var caster = ctx.Caster;
            var target = ctx.Target;

            var dx = Math.Abs(target.X - caster.Position.X);
            var dy = Math.Abs(target.Y - caster.Position.Y);
            if (Math.Max(dx, dy) > MaxRangeFromCaster) return;

            Entity? best = null;
            var bestDist = int.MaxValue;
            foreach (var enemy in ctx.Hostiles)
            {
                if (enemy.IsDead) continue;
                if (ReferenceEquals(enemy, caster)) continue;
                var ex = Math.Abs(enemy.Position.X - target.X);
                var ey = Math.Abs(enemy.Position.Y - target.Y);
                var dist = Math.Max(ex, ey);
                if (dist <= TargetRadius && dist < bestDist)
                {
                    best = enemy;
                    bestDist = dist;
                }
            }
            if (best is null) return;

            // LOS gate — the arrow needs an unobstructed path. Walls in
            // between mean the shot hits the wall and no damage applies.
            if (!TileLineOfSight.HasLineOfSight(ctx.Map, caster.ContinuousPosition, best.ContinuousPosition))
                return;

            best.TakeDamage(Damage);

            ctx.Vfx?.PlayAreaHighlight(new[] { best.Position }, HighlightColor, lifeSec: 0.15f);
        }
    }
}
