namespace Tarpg.Items;

// Legendary items in TARPG transform a skill or grant a build-defining passive.
// Each Legendary's behavior lives in an ILegendaryEffect implementation
// referenced by the ItemDefinition. New Legendaries = new effect class + definition.
public interface ILegendaryEffect
{
    // Human-readable description shown in the tooltip.
    string Description { get; }

    // TODO: hook points into the combat / skill systems will be added as those
    // systems land. The interface exists now so item definitions can reference it.
}
