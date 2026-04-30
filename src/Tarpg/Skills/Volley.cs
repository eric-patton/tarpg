using System.Numerics;
using SadRogue.Primitives;
using Tarpg.Core;
using Tarpg.Movement;

namespace Tarpg.Skills;

// Hunter's Q — three arrows in a tight pattern at the cursor. AOE on a
// chebyshev-radius-1 footprint (3×3, 9 cells) centered on the click cell.
// Range-gated against the caster + LOS-gated from the caster to the cursor
// cell (the volley as a whole needs a clear loose; individual enemies in
// the splash can be behind cover from the caster's POV).
public static class Volley
{
    private const int Damage = 8;
    private const int MaxRangeFromCaster = 6;
    private const int Radius = 1;

    public static readonly SkillDefinition Definition = new()
    {
        Id = "volley",
        Name = "Volley",
        Description = "Three arrows in a tight pattern at the cursor.",
        Resource = ResourceType.Focus,
        Cost = 12,
        CooldownSec = 1.0f,
        Glyph = '"',
        Behavior = new VolleyBehavior(),
    };

    private sealed class VolleyBehavior : ISkillBehavior
    {
        private static readonly Color HighlightColor = new(120, 220, 100);

        public void Execute(SkillContext ctx)
        {
            var caster = ctx.Caster;
            var target = ctx.Target;

            var dx = Math.Abs(target.X - caster.Position.X);
            var dy = Math.Abs(target.Y - caster.Position.Y);
            if (Math.Max(dx, dy) > MaxRangeFromCaster) return;

            var aimPoint = new Vector2(target.X + 0.5f, target.Y + 0.5f);
            if (!TileLineOfSight.HasLineOfSight(ctx.Map, caster.ContinuousPosition, aimPoint))
                return;

            foreach (var enemy in ctx.Hostiles)
            {
                if (enemy.IsDead) continue;
                if (ReferenceEquals(enemy, caster)) continue;
                var ex = Math.Abs(enemy.Position.X - target.X);
                var ey = Math.Abs(enemy.Position.Y - target.Y);
                if (Math.Max(ex, ey) > Radius) continue;
                enemy.TakeDamage(Damage);
            }

            if (ctx.Vfx is { } vfx)
            {
                var zone = new List<Position>(9);
                for (var ay = -Radius; ay <= Radius; ay++)
                for (var ax = -Radius; ax <= Radius; ax++)
                    zone.Add(new Position(target.X + ax, target.Y + ay));
                vfx.PlayAreaHighlight(zone, HighlightColor, lifeSec: 0.25f);
            }
        }
    }
}
