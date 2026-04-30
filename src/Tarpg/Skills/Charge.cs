using Tarpg.Core;
using Tarpg.Entities;

namespace Tarpg.Skills;

// W skill — dash from caster toward cursor along a straight line, up to
// MaxDistanceTiles. Stops at the first wall or first enemy. First enemy
// hit takes Damage; the caster ends on the last walkable tile before the
// blocker, so a charge into a wolf parks the player adjacent to it ready
// to swing again.
//
// GameScreen detects the post-execution position change as a teleport and
// clears the player's pending movement / combat target so the dash isn't
// immediately undone by leftover walk state.
public static class Charge
{
    private const int Damage = 15;
    private const int MaxDistanceTiles = 6;

    public static readonly SkillDefinition Definition = new()
    {
        Id = "charge",
        Name = "Charge",
        Description = "Dash toward the cursor; first enemy in the path takes damage.",
        Resource = ResourceType.Rage,
        Cost = 15,
        CooldownSec = 5.0f,
        // CP437 glyph 16 = ► (right-pointing solid triangle). Reads as
        // forward-momentum even though the actual charge direction varies.
        Glyph = (char)16,
        Behavior = new ChargeBehavior(),
    };

    private sealed class ChargeBehavior : ISkillBehavior
    {
        public void Execute(SkillContext ctx)
        {
            var caster = ctx.Caster;
            var start = caster.Position;
            var target = ctx.Target;
            if (start == target) return;

            var dx = target.X - start.X;
            var dy = target.Y - start.Y;
            // Chebyshev distance from start to the cursor target; the loop
            // walks ONE tile at a time along that line, so each iteration
            // advances exactly 1 tile regardless of how far the cursor is.
            // Without this, a far cursor scaled the per-step delta and the
            // dash would land at full target distance no matter the cap.
            var totalDistance = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (totalDistance == 0) return;
            var stepCount = Math.Min(MaxDistanceTiles, totalDistance);

            var lastWalkable = start;
            for (var i = 1; i <= stepCount; i++)
            {
                var t = (float)i / totalDistance;
                var x = start.X + (int)MathF.Round(dx * t);
                var y = start.Y + (int)MathF.Round(dy * t);
                var p = new Position(x, y);

                if (!ctx.Map.IsWalkable(p)) break;

                Entity? enemyHere = null;
                foreach (var enemy in ctx.Hostiles)
                {
                    if (enemy.IsDead) continue;
                    if (enemy.Position == p) { enemyHere = enemy; break; }
                }
                if (enemyHere is not null)
                {
                    enemyHere.TakeDamage(Damage);
                    break;
                }
                lastWalkable = p;
            }

            if (lastWalkable != start)
                caster.SetTile(lastWalkable);
        }
    }
}
