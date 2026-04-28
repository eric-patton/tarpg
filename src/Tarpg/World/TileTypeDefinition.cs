using SadRogue.Primitives;
using Tarpg.Core;

namespace Tarpg.World;

public sealed class TileTypeDefinition : IRegistryEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required char Glyph { get; init; }
    public required Color Foreground { get; init; }
    public Color Background { get; init; } = Color.Black;
    public required bool IsWalkable { get; init; }
    public required bool IsTransparent { get; init; }
}
