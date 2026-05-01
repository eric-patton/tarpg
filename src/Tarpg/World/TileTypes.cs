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

    // Boss arena marker. Walkable so the player can approach the boss
    // and engage; visually distinct (warm gold) so it doesn't read as
    // descent. Converted to Threshold by GameLoopController when the
    // floor's boss enemy dies, exposing the descent path as the reward
    // for clearing the arena.
    public static readonly TileTypeDefinition BossAnchor = new()
    {
        Id = "boss_anchor",
        Name = "boss arena",
        Glyph = '*',
        Foreground = new Color(220, 180, 110),
        IsWalkable = true,
        IsTransparent = true,
    };
}
