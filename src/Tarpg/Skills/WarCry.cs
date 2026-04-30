using SadRogue.Primitives;
using Tarpg.Core;
using Tarpg.Entities;

namespace Tarpg.Skills;

// E skill — instant heal. The "buff" reading (Diablo-style War Cry would
// also boost damage / armor for a few seconds) is deferred until the buff
// system lands; for v0 it's a straight HP restore on a long cooldown.
// Costs Rage, so the Reaver pays for the bail-out via the damage-economy
// they were just generating.
public static class WarCry
{
    private const int HealAmount = 25;

    public static readonly SkillDefinition Definition = new()
    {
        Id = "war_cry",
        Name = "War Cry",
        Description = "Steady the wounded — restores HP.",
        Resource = ResourceType.Rage,
        Cost = 25,
        CooldownSec = 12.0f,
        Glyph = '*',
        Behavior = new WarCryBehavior(),
    };

    private sealed class WarCryBehavior : ISkillBehavior
    {
        private static readonly Color FlashColor = new(80, 220, 120);

        public void Execute(SkillContext ctx)
        {
            if (ctx.Caster is not Player p) return;
            p.Health = Math.Min(p.MaxHealth, p.Health + HealAmount);

            // Green screen flash sells the "shouted, steeled, healed" beat
            // — pure visual side effect, no gameplay impact.
            ctx.Vfx?.PlayScreenFlash(FlashColor, durationSec: 0.4f);
        }
    }
}
