namespace Tarpg.World.Generation;

// Strategy reference held by ZoneDefinition.Generator. Mirrors the
// ISkillBehavior / ILegendaryEffect / IModifierBehavior pattern.
//
// floor is the descent depth (1-indexed). Reserved for per-floor difficulty
// / density / size scaling; current generators may ignore it.
public interface IZoneGenerator
{
    GeneratedFloor Generate(int width, int height, int seed, int floor);
}
