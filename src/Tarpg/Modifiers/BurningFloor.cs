namespace Tarpg.Modifiers;

// Example modifier. Adding a new floor modifier:
//   1. Static class with `public static readonly ModifierDefinition Definition`.
//   2. Nested IModifierBehavior implementation that mutates ModifierContext.
public static class BurningFloor
{
    public static readonly ModifierDefinition Definition = new()
    {
        Id = "burning",
        Name = "Burning",
        Description = "Echoes deal +30% fire damage.",
        Weight = 100,
        DifficultyTier = 1,
        LootBonusPercent = 12,
        Behavior = new BurningBehavior(),
    };

    private sealed class BurningBehavior : IModifierBehavior
    {
        public void Apply(ModifierContext context)
        {
            context.EnemyDamageByElement.TryGetValue("fire", out var current);
            context.EnemyDamageByElement["fire"] = current + 0.30f;
        }
    }
}
