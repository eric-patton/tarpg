using SadRogue.Primitives;
using Tarpg.Core;
using Tarpg.Entities;

namespace Tarpg.Skills;

// R skill — single-pulse AOE around the caster. The "channelled while
// held, drain Rage over time" reading is deferred until a channel state
// machine exists; for v0 it's a one-shot bigger Cleave: chebyshev radius
// 2 instead of 1, more Rage, more cooldown, and damage on each hit so
// HitFeedback's flash + damage numbers fire on every enemy in range at
// once.
public static class Whirlwind
{
    private const int Damage = 15;
    private const int Radius = 2;

    public static readonly SkillDefinition Definition = new()
    {
        Id = "whirlwind",
        Name = "Whirlwind",
        Description = "Spin and strike all nearby enemies.",
        Resource = ResourceType.Rage,
        Cost = 30,
        CooldownSec = 6.0f,
        Glyph = '%',
        Behavior = new WhirlwindBehavior(),
    };

    private sealed class WhirlwindBehavior : ISkillBehavior
    {
        private static readonly Color HighlightColor = new(220, 90, 40);

        public void Execute(SkillContext ctx)
        {
            var caster = ctx.Caster;
            var center = caster.Position;
            foreach (var enemy in ctx.Hostiles)
            {
                if (enemy.IsDead) continue;
                if (ReferenceEquals(enemy, caster)) continue;
                var dx = Math.Abs(enemy.Position.X - center.X);
                var dy = Math.Abs(enemy.Position.Y - center.Y);
                if (Math.Max(dx, dy) > Radius) continue;
                enemy.TakeDamage(Damage);
            }

            if (ctx.Vfx is { } vfx)
            {
                // Full chebyshev-radius-2 footprint, including the caster's
                // own tile so the spin reads as a complete spinning ring
                // around the player (not just the enemy-affecting outline).
                var area = new List<Position>((Radius * 2 + 1) * (Radius * 2 + 1));
                for (var dy = -Radius; dy <= Radius; dy++)
                for (var dx = -Radius; dx <= Radius; dx++)
                    area.Add(new Position(center.X + dx, center.Y + dy));
                vfx.PlayAreaHighlight(area, HighlightColor, lifeSec: 0.3f);
                vfx.PlayScreenShake(intensityPx: 5f, durationSec: 0.15f);
            }
        }
    }
}
