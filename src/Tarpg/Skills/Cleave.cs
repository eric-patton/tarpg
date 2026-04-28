using Tarpg.Core;

namespace Tarpg.Skills;

// Example skill. New skills follow the same pattern:
//   1. Static class with a `public static readonly SkillDefinition Definition`.
//   2. Nested ISkillBehavior implementation.
// ContentInitializer auto-discovers the static field.
public static class Cleave
{
    public static readonly SkillDefinition Definition = new()
    {
        Id = "cleave",
        Name = "Cleave",
        Description = "Strike all adjacent enemies for full weapon damage.",
        Resource = ResourceType.Rage,
        Cost = 10,
        Behavior = new CleaveBehavior(),
    };

    private sealed class CleaveBehavior : ISkillBehavior
    {
        public void Execute(SkillContext context)
        {
            // TODO: enumerate Chebyshev-adjacent tiles to caster, hit any
            // hostile entity found there. Hooked up when combat resolution lands.
        }
    }
}
