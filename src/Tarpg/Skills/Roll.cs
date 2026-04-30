using Tarpg.Core;
using Tarpg.Entities;

namespace Tarpg.Skills;

// Hunter's W — disengagement dash. Mirrors Charge's straight-line walk but
// in the opposite direction, so the cursor cell becomes the "what I'm
// rolling away from" anchor. Walks one tile at a time along the away-vector,
// stops at the first wall, lands on the last walkable tile before the
// blocker. Caster doesn't take or deal damage on the way through.
//
// Treated as a teleport-style skill by GameLoopController.TryCastSkill —
// the caster's position changes, so live play converts the snap into a
// brief animated lerp via GameScreen's dash visual.
public static class Roll
{
    private const int MaxDistanceTiles = 4;

    public static readonly SkillDefinition Definition = new()
    {
        Id = "roll",
        Name = "Roll",
        Description = "Tumble away from the cursor.",
        Resource = ResourceType.Focus,
        Cost = 10,
        CooldownSec = 4.0f,
        Glyph = '~',
        Behavior = new RollBehavior(),
    };

    private sealed class RollBehavior : ISkillBehavior
    {
        public void Execute(SkillContext ctx)
        {
            var caster = ctx.Caster;
            var start = caster.Position;
            var awayFrom = ctx.Target;

            var dirX = start.X - awayFrom.X;
            var dirY = start.Y - awayFrom.Y;
            if (dirX == 0 && dirY == 0)
            {
                // Cursor on the caster — pick an arbitrary east direction so
                // the cooldown still fires instead of no-op'ing the cast.
                dirX = 1;
            }
            var maxDir = Math.Max(Math.Abs(dirX), Math.Abs(dirY));

            var lastWalkable = start;
            for (var i = 1; i <= MaxDistanceTiles; i++)
            {
                var t = (float)i / maxDir;
                var x = start.X + (int)MathF.Round(dirX * t);
                var y = start.Y + (int)MathF.Round(dirY * t);
                var p = new Position(x, y);

                if (!ctx.Map.IsWalkable(p)) break;
                lastWalkable = p;
            }

            if (lastWalkable != start)
                caster.SetTile(lastWalkable);
        }
    }
}
