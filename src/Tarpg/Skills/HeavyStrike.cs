using SadRogue.Primitives;
using Tarpg.Core;
using Tarpg.Entities;

namespace Tarpg.Skills;

// M2 / right-mouse skill. Pick the enemy closest to the cursor cell within
// TargetRadius (so a near-miss click still connects, matching the
// click-target-radius rule for left-click target selection), then deal
// Damage to it. Range-gated against the caster so M2 reads as a "wider
// auto-attack on a cooldown" rather than a teleport-strike.
//
// Resource cost is zero — M2 is a primary input the player should be able
// to mash whenever the cooldown is up. Cooldown is the only gate.
public static class HeavyStrike
{
    private const int Damage = 25;
    private const int MaxRangeFromCaster = 2; // chebyshev tiles
    private const int TargetRadius = 1;       // chebyshev around clicked tile

    public static readonly SkillDefinition Definition = new()
    {
        Id = "heavy_strike",
        Name = "Heavy Strike",
        Description = "A decisive single blow at the cursor.",
        Resource = ResourceType.Rage,
        Cost = 0,
        CooldownSec = 1.5f,
        Glyph = '!',
        Behavior = new HeavyStrikeBehavior(),
    };

    private sealed class HeavyStrikeBehavior : ISkillBehavior
    {
        private static readonly Color HighlightColor = new(220, 90, 40);

        public void Execute(SkillContext ctx)
        {
            var caster = ctx.Caster;
            var target = ctx.Target;

            var fromCasterX = Math.Abs(target.X - caster.Position.X);
            var fromCasterY = Math.Abs(target.Y - caster.Position.Y);
            if (Math.Max(fromCasterX, fromCasterY) > MaxRangeFromCaster) return;

            Entity? best = null;
            var bestDist = int.MaxValue;
            foreach (var enemy in ctx.Hostiles)
            {
                if (enemy.IsDead) continue;
                if (ReferenceEquals(enemy, caster)) continue;
                var dx = Math.Abs(enemy.Position.X - target.X);
                var dy = Math.Abs(enemy.Position.Y - target.Y);
                var dist = Math.Max(dx, dy);
                if (dist <= TargetRadius && dist < bestDist)
                {
                    best = enemy;
                    bestDist = dist;
                }
            }

            best?.TakeDamage(Damage);

            if (ctx.Vfx is { } vfx)
            {
                // Tight 3×3 zone at the click cell so the impact reads as
                // "where the strike landed" rather than a diffuse pulse.
                var zone = new List<Position>(9);
                for (var dy = -TargetRadius; dy <= TargetRadius; dy++)
                for (var dx = -TargetRadius; dx <= TargetRadius; dx++)
                    zone.Add(new Position(target.X + dx, target.Y + dy));
                vfx.PlayAreaHighlight(zone, HighlightColor, lifeSec: 0.2f);
                vfx.PlayScreenShake(intensityPx: 3f, durationSec: 0.1f);
            }
        }
    }
}
