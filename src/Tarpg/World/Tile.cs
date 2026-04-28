namespace Tarpg.World;

// One cell on the map. Per-cell visibility / explored state lives on the
// underlying RogueSharp map (see Map.IsInFov / Map.IsExploredAt); this type
// only carries the tile's mutable type reference.
public sealed class Tile
{
    public required TileTypeDefinition Type { get; set; }
}
