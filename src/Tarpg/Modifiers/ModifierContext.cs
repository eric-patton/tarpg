namespace Tarpg.Modifiers;

// Mutable bag of modifier-relevant numbers / state.
// Modifiers receive this and adjust it. Combat / drop systems read it.
// Kept open-ended so new modifier types can read/write whatever they need
// without touching a central type.
public sealed class ModifierContext
{
    public float DamageDealtMultiplier { get; set; } = 1.0f;
    public float DamageTakenMultiplier { get; set; } = 1.0f;
    public float HealingMultiplier { get; set; } = 1.0f;
    public float MovementSpeedMultiplier { get; set; } = 1.0f;
    public float DropQuantityMultiplier { get; set; } = 1.0f;
    public float FieldOfViewRadius { get; set; } = 10.0f;

    // Element-specific modifiers expressed as a simple table so we can add
    // damage types without modifying this class.
    public Dictionary<string, float> EnemyDamageByElement { get; } = new();
}
