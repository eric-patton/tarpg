using RogueSharp;
using Tarpg.Core;

namespace Tarpg.World;

public sealed class Map
{
    private readonly Tile[,] _tiles;
    private readonly RogueSharp.Map _rogueMap;

    // Visibility / explored state owned here, not delegated to RogueSharp,
    // so the Euclidean-circle mask we apply on top of RogueSharp's diamond
    // FOV is the canonical truth for both the renderer and any future
    // gameplay code that needs "is this tile visible?".
    private readonly bool[,] _visible;
    private readonly bool[,] _explored;

    // Cached PathFinder. Constructing one is expensive (builds an
    // EdgeWeightedDigraph from every walkable cell) and the map is immutable
    // for the lifetime of the floor in normal play, so we keep one around
    // and only invalidate when SetTile flips a walkability bit.
    private RogueSharp.PathFinder? _cachedPathFinder;

    public int Width { get; }
    public int Height { get; }

    public Map(int width, int height, TileTypeDefinition fillType)
    {
        Width = width;
        Height = height;
        _tiles = new Tile[width, height];
        _rogueMap = new RogueSharp.Map(width, height);
        _visible = new bool[width, height];
        _explored = new bool[width, height];

        for (var x = 0; x < width; x++)
        for (var y = 0; y < height; y++)
        {
            _tiles[x, y] = new Tile { Type = fillType };
            _rogueMap.SetCellProperties(x, y, fillType.IsTransparent, fillType.IsWalkable);
        }
    }

    public bool InBounds(Position p) =>
        p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;

    public Tile this[int x, int y] => _tiles[x, y];

    public Tile this[Position p] => _tiles[p.X, p.Y];

    public TileTypeDefinition GetType(Position p) => _tiles[p.X, p.Y].Type;

    public void SetTile(Position p, TileTypeDefinition type)
    {
        _tiles[p.X, p.Y].Type = type;
        _rogueMap.SetCellProperties(p.X, p.Y, type.IsTransparent, type.IsWalkable);
        // Walkability may have changed; force the PathFinder graph to rebuild
        // on next FindPath. In practice SetTile is only called during BSP
        // generation, so this happens a couple of hundred times during floor
        // build and zero times during play.
        _cachedPathFinder = null;
    }

    public bool IsWalkable(Position p) => InBounds(p) && _tiles[p.X, p.Y].Type.IsWalkable;

    public bool IsTransparent(Position p) => InBounds(p) && _tiles[p.X, p.Y].Type.IsTransparent;

    // Wraps the underlying RogueSharp map for FOV / pathfinding consumers.
    public RogueSharp.Map RogueMap => _rogueMap;

    // Recompute the player's FOV. RogueSharp's ComputeFov uses Manhattan
    // distance internally, so its raw output is a 45°-rotated diamond. We
    // call it with a Manhattan radius wide enough to fully contain our
    // desired Euclidean circle (radius * √2), then walk the bounding box of
    // the circle and only mark tiles visible if both:
    //   (a) RogueSharp's shadowcast says they have LOS from the viewer, and
    //   (b) their Euclidean distance from the viewer is within the radius.
    // This gives us correct LOS through walls plus a properly circular
    // reveal shape. Explored is sticky in our own array — once true, stays
    // true until a fresh Map is built.
    public void ComputeFovFor(Position viewer, int radius)
    {
        var manhattanRadius = (int)Math.Ceiling(radius * Math.Sqrt(2));
        _rogueMap.ComputeFov(viewer.X, viewer.Y, manhattanRadius, lightWalls: true);

        Array.Clear(_visible, 0, _visible.Length);
        var rSq = radius * radius;
        for (var dy = -radius; dy <= radius; dy++)
        for (var dx = -radius; dx <= radius; dx++)
        {
            if (dx * dx + dy * dy > rSq) continue;
            var x = viewer.X + dx;
            var y = viewer.Y + dy;
            if (x < 0 || x >= Width || y < 0 || y >= Height) continue;
            if (!_rogueMap.IsInFov(x, y)) continue;
            _visible[x, y] = true;
            _explored[x, y] = true;
        }
    }

    public bool IsInFov(Position p) =>
        InBounds(p) && _visible[p.X, p.Y];

    public bool IsExploredAt(Position p) =>
        InBounds(p) && _explored[p.X, p.Y];

    // Computes A* path from start to end (excluding start, including end).
    // Returns null if no path exists.
    public IReadOnlyList<Position>? FindPath(Position start, Position end)
    {
        if (!InBounds(start) || !InBounds(end)) return null;
        if (!IsWalkable(end)) return null;

        var pathFinder = _cachedPathFinder ??= new PathFinder(_rogueMap);
        var startCell = _rogueMap.GetCell(start.X, start.Y);
        var endCell = _rogueMap.GetCell(end.X, end.Y);

        try
        {
            var path = pathFinder.ShortestPath(startCell, endCell);
            var result = new List<Position>();
            // RogueSharp path includes start; skip it.
            var first = true;
            foreach (var step in path.Steps)
            {
                if (first) { first = false; continue; }
                result.Add(new Position(step.X, step.Y));
            }
            return result;
        }
        catch (PathNotFoundException)
        {
            return null;
        }
    }
}
