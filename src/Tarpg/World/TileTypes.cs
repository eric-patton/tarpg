using SadRogue.Primitives;

namespace Tarpg.World;

// Default tile types. New tile types can be added by dropping a similar
// `public static readonly TileTypeDefinition Foo = new() { ... }` field
// in any file in this assembly — the ContentInitializer will pick it up.
public static class TileTypes
{
    public static readonly TileTypeDefinition Floor = new()
    {
        Id = "floor",
        Name = "stone floor",
        Glyph = '.',
        Foreground = new Color(80, 80, 80),
        IsWalkable = true,
        IsTransparent = true,
    };

    public static readonly TileTypeDefinition Wall = new()
    {
        Id = "wall",
        Name = "stone wall",
        Glyph = '#',
        Foreground = new Color(160, 140, 110),
        IsWalkable = false,
        IsTransparent = false,
    };

    public static readonly TileTypeDefinition Door = new()
    {
        Id = "door",
        Name = "wooden door",
        Glyph = '+',
        Foreground = new Color(140, 100, 60),
        IsWalkable = true,
        IsTransparent = false,
    };

    public static readonly TileTypeDefinition Threshold = new()
    {
        Id = "threshold",
        Name = "the Threshold",
        Glyph = '>',
        Foreground = new Color(180, 80, 200),
        IsWalkable = true,
        IsTransparent = true,
    };
}
