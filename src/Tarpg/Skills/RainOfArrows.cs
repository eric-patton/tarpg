using SadRogue.Primitives;
using Tarpg.Core;

namespace Tarpg.Skills;

// Hunter's R — saturation strike at a cursor cell. Chebyshev-radius-2
// footprint (5×5, 25 cells) at the click point, range-gated against the
// caster but not LOS-gated (the arrows arc — they hit through walls and
// over corners). Compensates for the lack of LOS check via the higher
// cost / longer cooldown / specific cursor target.
public static class RainOfArrows
{
    private const int Damage = 18;
    private const int MaxRangeFromCaster = 8;
    private const int Radius = 2;

    public static readonly SkillDefinition Definition = new()
    {
        Id = "rain_of_arrows",
        Name = "Rain of Arrows",
        Description = "Saturate a wide area at the cursor.",
        Resource = ResourceType.Focus,
        Cost = 35,
        CooldownSec = 8.0f,
        Glyph = ':',
        Behavior = new RainOfArrowsBehavior(),
    };

    private sealed class RainOfArrowsBehavior : ISkillBehavior
    {
        private static readonly Color HighlightColor = new(120, 220, 100);

        public void Execute(SkillContext ctx)
        {
            var caster = ctx.Caster;
            var target = ctx.Target;

            var dx = Math.Abs(target.X - caster.Position.X);
            var dy = Math.Abs(target.Y - caster.Position.Y);
            if (Math.Max(dx, dy) > MaxRangeFromCaster) return;

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
                var area = new List<Position>((Radius * 2 + 1) * (Radius * 2 + 1));
                for (var ay = -Radius; ay <= Radius; ay++)
                for (var ax = -Radius; ax <= Radius; ax++)
                    area.Add(new Position(target.X + ax, target.Y + ay));
                vfx.PlayAreaHighlight(area, HighlightColor, lifeSec: 0.4f);
                vfx.PlayScreenShake(intensityPx: 4f, durationSec: 0.15f);
            }
        }
    }
}
