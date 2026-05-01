using Tarpg.Core;
using Tarpg.World;
using Tarpg.World.Generation;

namespace Tarpg.Tests.World.Generation;

public class BspGeneratorTests
{
    private const int Width = 80;
    private const int Height = 30;

    [Fact]
    public void Generate_SameSeed_ProducesIdenticalLayout()
    {
        var gen = new BspGenerator();
        var a = gen.Generate(Width, Height, seed: 12345, floor: 1);
        var b = gen.Generate(Width, Height, seed: 12345, floor: 1);

        Assert.Equal(a.Entry, b.Entry);
        Assert.Equal(a.BossAnchor, b.BossAnchor);
        Assert.Equal(a.EnemySpawnPoints.Count, b.EnemySpawnPoints.Count);

        // Tile-by-tile parity. Catches any new RNG path or non-deterministic
        // collection order that sneaks in.
        for (var x = 0; x < Width; x++)
        for (var y = 0; y < Height; y++)
        {
            var p = new Position(x, y);
            Assert.Equal(a.Map[p].Type.Id, b.Map[p].Type.Id);
        }
    }

    [Fact]
    public void Generate_EntryAndAnchorAreWalkable()
    {
        var gen = new BspGenerator();
        var floor = gen.Generate(Width, Height, seed: 42, floor: 1);

        Assert.True(floor.Map.IsWalkable(floor.Entry));
        Assert.True(floor.Map.IsWalkable(floor.BossAnchor));
    }

    [Fact]
    public void Generate_EnemySpawnsAreWalkable()
    {
        var gen = new BspGenerator();
        var floor = gen.Generate(Width, Height, seed: 99, floor: 5);

        foreach (var spawn in floor.EnemySpawnPoints)
            Assert.True(floor.Map.IsWalkable(spawn),
                $"Enemy spawn at ({spawn.X},{spawn.Y}) is not walkable.");
    }

    [Fact]
    public void Generate_ThresholdReachableFromEntry()
    {
        var gen = new BspGenerator();
        var floor = gen.Generate(Width, Height, seed: 7, floor: 1);

        // BFS over walkable tiles from the entry; assert the threshold tile
        // is reachable. BspGenerator already throws on disconnected rooms,
        // so this also doubles as "the connectivity-verify pass ran."
        var reached = ReachableFrom(floor.Map, floor.Entry);
        Assert.True(reached[floor.BossAnchor.X, floor.BossAnchor.Y],
            "Threshold (boss anchor) should be reachable from entry.");
    }

    [Fact]
    public void Generate_HigherFloor_IncreasesSpawnSlotCount()
    {
        var gen = new BspGenerator();

        // Same seed for layout parity; different floor depth so spawn count
        // ramps. BSP generator uses floor only to compute MaxEnemySlots.
        var f1 = gen.Generate(Width, Height, seed: 1, floor: 1);
        var f10 = gen.Generate(Width, Height, seed: 1, floor: 10);

        // Slot count is bounded above by MaxEnemySlotsCap (12). Floor 1
        // caps at 7, floor 10 caps at 12 (or however many candidate rooms
        // accept a placement). At minimum, deeper floors >= shallower.
        Assert.True(f10.EnemySpawnPoints.Count >= f1.EnemySpawnPoints.Count);
    }

    [Fact]
    public void Generate_NonBossFloor_PlacesThresholdAtAnchor()
    {
        var gen = new BspGenerator();
        var floor = gen.Generate(Width, Height, seed: 42, floor: 1);

        Assert.Equal(TileTypes.Threshold.Id, floor.Map[floor.BossAnchor].Type.Id);
    }

    [Fact]
    public void Generate_BossFloor_PlacesBossAnchorAtAnchor()
    {
        // F5 is the v0 entry in BspGenerator.BossFloors; the farthest
        // room should hold a BossAnchor tile (not Threshold) so the
        // boss-spawn pipeline takes over from the descent pipeline.
        var gen = new BspGenerator();
        var floor = gen.Generate(Width, Height, seed: 42, floor: 5);

        Assert.Contains(5, BspGenerator.BossFloors);
        Assert.Equal(TileTypes.BossAnchor.Id, floor.Map[floor.BossAnchor].Type.Id);
    }

    private static bool[,] ReachableFrom(Map map, Position start)
    {
        var visited = new bool[map.Width, map.Height];
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        visited[start.X, start.Y] = true;

        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            TryEnqueue(map, visited, queue, new Position(p.X - 1, p.Y));
            TryEnqueue(map, visited, queue, new Position(p.X + 1, p.Y));
            TryEnqueue(map, visited, queue, new Position(p.X, p.Y - 1));
            TryEnqueue(map, visited, queue, new Position(p.X, p.Y + 1));
        }

        return visited;
    }

    private static void TryEnqueue(Map map, bool[,] visited, Queue<Position> queue, Position p)
    {
        if (!map.IsWalkable(p)) return;
        if (visited[p.X, p.Y]) return;
        visited[p.X, p.Y] = true;
        queue.Enqueue(p);
    }
}
