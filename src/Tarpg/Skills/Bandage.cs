using SadRogue.Primitives;
using Tarpg.Core;
using Tarpg.Entities;

namespace Tarpg.Skills;

// Hunter's E — instant heal. Mechanically identical to the Reaver's War
// Cry (instant HP restore on a long cooldown for resource cost) — the heal
// is class-agnostic, only the flavor differs. A cream / linen flash sells
// the "binding wound" beat where War Cry's green sells "shouted, steeled."
public static class Bandage
{
    private const int HealAmount = 25;

    public static readonly SkillDefinition Definition = new()
    {
        Id = "bandage",
        Name = "Bandage",
        Description = "Bind a wound — restores HP.",
        Resource = ResourceType.Focus,
        Cost = 25,
        CooldownSec = 12.0f,
        Glyph = '+',
        Behavior = new BandageBehavior(),
    };

    private sealed class BandageBehavior : ISkillBehavior
    {
        private static readonly Color FlashColor = new(220, 220, 180);

        public void Execute(SkillContext ctx)
        {
            if (ctx.Caster is not Player p) return;
            p.Health = Math.Min(p.MaxHealth, p.Health + HealAmount);
            ctx.Vfx?.PlayScreenFlash(FlashColor, durationSec: 0.4f);
        }
    }
}
