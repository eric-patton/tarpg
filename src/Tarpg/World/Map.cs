using RogueSharp;
using Tarpg.Core;

namespace Tarpg.World;

public sealed class Map
{
    private readonly Tile[,] _tiles;
    private readonly RogueSharp.Map _rogueMap;

    public int Width { get; }
    public int Height { get; }

    public Map(int width, int height, TileTypeDefinition fillType)
    {
        Width = width;
        Height = height;
        _tiles = new Tile[width, height];
        _rogueMap = new RogueSharp.Map(width, height);

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
    }

    public bool IsWalkable(Position p) => InBounds(p) && _tiles[p.X, p.Y].Type.IsWalkable;

    public bool IsTransparent(Position p) => InBounds(p) && _tiles[p.X, p.Y].Type.IsTransparent;

    // Wraps the underlying RogueSharp map for FOV / pathfinding consumers.
    public RogueSharp.Map RogueMap => _rogueMap;

    // Recomputes RogueSharp's FOV in-place around the viewer. Each call clears
    // the prior IsInFov flags; IsExplored is sticky. lightWalls=true makes
    // opaque tiles at the FOV boundary themselves visible (so you see the wall
    // you're up against), which matches the standard roguelike expectation.
    public void ComputeFovFor(Position viewer, int radius) =>
        _rogueMap.ComputeFov(viewer.X, viewer.Y, radius, lightWalls: true);

    public bool IsInFov(Position p) =>
        InBounds(p) && _rogueMap.IsInFov(p.X, p.Y);

    public bool IsExploredAt(Position p) =>
        InBounds(p) && _rogueMap.IsExplored(p.X, p.Y);

    // Computes A* path from start to end (excluding start, including end).
    // Returns null if no path exists.
    public IReadOnlyList<Position>? FindPath(Position start, Position end)
    {
        if (!InBounds(start) || !InBounds(end)) return null;
        if (!IsWalkable(end)) return null;

        var pathFinder = new PathFinder(_rogueMap);
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
