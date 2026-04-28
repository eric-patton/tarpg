namespace Tarpg.World.Generation;

// Strategy reference held by ZoneDefinition.Generator. Mirrors the
// ISkillBehavior / ILegendaryEffect / IModifierBehavior pattern.
//
// The eventual `floor: int` parameter for multi-floor descent is a one-line
// addition to this signature plus its call sites — accepted future churn.
public interface IZoneGenerator
{
    GeneratedFloor Generate(int width, int height, int seed);
}
