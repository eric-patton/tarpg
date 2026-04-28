namespace Tarpg.Modifiers;

public interface IModifierBehavior
{
    // Called once when the floor is generated. Mutates the ModifierContext
    // for the duration of the floor.
    void Apply(ModifierContext context);
}
