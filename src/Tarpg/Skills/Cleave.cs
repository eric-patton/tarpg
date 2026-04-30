using SadRogue.Primitives;
using Tarpg.Core;

namespace Tarpg.Skills;

// Reaver's basic horde-clearing tool. Deals weapon damage to every
// chebyshev-adjacent live enemy (the 8 surrounding tiles) on activation.
// Resource cost gates spamming; cooldown gates back-to-back animation feel.
public static class Cleave
{
    // Damage a Cleave swing deals to each adjacent target. v0 uses the
    // same value as the auto-attack BaseDamage so "full weapon damage" is
    // honest — both update together when item-driven damage lands.
    private const int Damage = 10;

    public static readonly SkillDefinition Definition = new()
    {
        Id = "cleave",
        Name = "Cleave",
        Description = "Strike all adjacent enemies for full weapon damage.",
        Resource = ResourceType.Rage,
        Cost = 10,
        CooldownSec = 1.0f,
        Glyph = 'X',
        Behavior = new CleaveBehavior(),
    };

    private sealed class CleaveBehavior : ISkillBehavior
    {
        private static readonly Color HighlightColor = new(220, 90, 40);

        public void Execute(SkillContext context)
        {
            var caster = context.Caster;
            var center = caster.Position;
            foreach (var target in context.Hostiles)
            {
                if (ReferenceEquals(target, caster)) continue;
                if (target.IsDead) continue;
                var dx = Math.Abs(target.Position.X - center.X);
                var dy = Math.Abs(target.Position.Y - center.Y);
                // Chebyshev radius 1 = the 8 surrounding tiles. Caster's own
                // tile is filtered above so a stacked enemy on the caster's
                // cell still wouldn't take damage from this swing.
                if (dx > 1 || dy > 1) continue;
                target.TakeDamage(Damage);
            }

            if (context.Vfx is { } vfx)
            {
                var ring = new List<Position>(8);
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    ring.Add(new Position(center.X + dx, center.Y + dy));
                }
                vfx.PlayAreaHighlight(ring, HighlightColor, lifeSec: 0.25f);
                vfx.PlayScreenShake(intensityPx: 2f, durationSec: 0.08f);
            }
        }
    }
}
