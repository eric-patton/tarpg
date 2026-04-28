using Tarpg.World.Generation;

namespace Tarpg.World.Zones;

// First mythic zone. Forest-themed, uses BSP for room layout. The "wolfwood"
// id matches the strings already referenced by Wolf.ZoneIds and
// WolfMother.ZoneId.
public static class Wolfwood
{
    public static readonly ZoneDefinition Definition = new()
    {
        Id = "wolfwood",
        Name = "the Wolfwood",
        Generator = new BspGenerator(),
    };
}
