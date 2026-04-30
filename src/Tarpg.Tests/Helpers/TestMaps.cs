using Tarpg.Core;
using Tarpg.World;

namespace Tarpg.Tests.Helpers;

// Test fixtures for building small maps without going through BspGenerator.
// Floors are open by default; pass a list of wall positions for collision /
// LOS tests.
internal static class TestMaps
{
    public static Map OpenFloor(int width, int height)
    {
        var map = new Map(width, height, TileTypes.Wall);
        for (var x = 1; x < width - 1; x++)
        for (var y = 1; y < height - 1; y++)
            map.SetTile(new Position(x, y), TileTypes.Floor);
        return map;
    }

    public static Map OpenFloorWithWalls(int width, int height, params Position[] walls)
    {
        var map = OpenFloor(width, height);
        foreach (var w in walls)
            map.SetTile(w, TileTypes.Wall);
        return map;
    }
}
