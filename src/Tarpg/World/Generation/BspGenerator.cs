using Tarpg.Core;

namespace Tarpg.World.Generation;

// Binary space partitioning generator. Splits the inner play area into a
// handful of leaves, carves a room in each, then connects sibling leaves
// with L-shaped corridors. Connectivity is guaranteed by construction
// (every internal node connects its two subtree-spanning leaf groups).
public sealed class BspGenerator : IZoneGenerator
{
    // Min size for a leaf still allowed to split. Tuned so 80x30 averages
    // about 4–7 leaves with sane aspect ratios. Bumping MinLeaf* up gives
    // fewer larger rooms; bumping MaxDepth gives more chances to subdivide.
    private const int MinLeafWidth = 12;
    private const int MinLeafHeight = 8;
    private const int MaxDepth = 4;

    private const int MinRoomWidth = 4;
    private const int MinRoomHeight = 3;
    private const int RoomEdgeMargin = 1;

    // Chebyshev tiles between the entry and any spawn room's center. Below
    // the threshold the room gets dropped from the candidate pool, so the
    // player has a few-tile breather before encountering enemies. Used as a
    // *room-level* check so we don't waste 20 random attempts on a room
    // that's entirely too close.
    private const int MinSpawnDistanceFromEntry = 4;

    // Spawn slot count = the number of (center, def) decisions the spawn
    // pipeline makes per floor. With horde packs (PackSize > 1) one slot can
    // expand to several actual enemies, so this is an upper bound on the
    // *spawn calls*, not on the live-enemy count.
    private const int MaxEnemySlotsBase = 6;
    private const int MaxEnemySlotsCap = 12;

    // Force a split direction when a leaf is markedly wider than tall (or
    // vice versa). Below the threshold, the split direction is random.
    private const float SplitAspectRatio = 1.25f;

    public GeneratedFloor Generate(int width, int height, int seed, int floor)
    {
        var rng = new Random(seed);

        // Slot count ramps with descent depth, capped so even endgame floors
        // don't produce arbitrarily large hordes. Stat scaling (HP/Damage
        // multiplier) is applied separately at spawn time by GameScreen.
        var maxEnemySlots = Math.Min(MaxEnemySlotsCap, MaxEnemySlotsBase + floor);

        // Start with a wall-filled grid; we carve floors out of it.
        var map = new Map(width, height, TileTypes.Wall);

        // Inner play area excludes the outer border so generated rooms /
        // corridors never bleed into row/col 0 or width-1 / height-1.
        var rootBounds = new Rect(1, 1, width - 2, height - 2);
        var root = new BspNode(rootBounds);
        Split(root, depth: 0, rng);

        // Walk leaves, generate one room rect per leaf, carve them. Carving
        // rooms first means corridor segments inside an existing room are
        // no-ops — corridors only meaningfully cut through wall spans.
        // No CA roughening pass for v0 — deferred until we add a flood-fill
        // connectivity-repair step. Tracked in docs/STATUS.md.
        var rooms = new List<Rect>();
        GenerateAndCarveRooms(root, map, rng, rooms);

        if (rooms.Count < 2)
            throw new InvalidOperationException(
                $"BSP produced fewer than 2 rooms (seed={seed}). " +
                "Check MinLeaf* constants vs. map dimensions.");

        // No doors at room↔corridor junctions for v0 — Wolfwood is forest;
        // opaque doors would over-pop FOV in a 5-room procgen. Doors get
        // placed deliberately when town buildings or boss-arena gates land.
        ConnectSubtree(root, map, rng);

        // Entry tile = a random walkable cell in the first room.
        var entryRoom = rooms[0];
        var entry = RandomWalkableInRect(map, entryRoom, rng);

        // Connectivity should be guaranteed by construction now that
        // corridors target room midpoints, but flood-fill from the entry
        // and verify before we ship the floor — surfaces the seed for
        // repro if a future change reintroduces a topology bug.
        VerifyAllRoomsReachable(map, entry, rooms, seed);

        // Boss anchor = a random walkable cell in the room farthest (by
        // chebyshev distance of room centers) from the entry room. Marked
        // with a Threshold tile so the player can see "this is where the
        // next thing happens" until the real boss spawn lands.
        var bossRoom = FarthestRoom(rooms, entryRoom);
        var bossAnchor = RandomWalkableInRect(map, bossRoom, rng);
        map.SetTile(bossAnchor, TileTypes.Threshold);

        var spawns = ChooseEnemySpawns(
            map, rooms, entryRoom, bossRoom, entry, bossAnchor, maxEnemySlots, rng);

        return new GeneratedFloor
        {
            Map = map,
            Entry = entry,
            BossAnchor = bossAnchor,
            EnemySpawnPoints = spawns,
        };
    }

    // ---- BSP split ----

    private static void Split(BspNode node, int depth, Random rng)
    {
        if (depth >= MaxDepth) return;

        var b = node.Bounds;
        var canSplitVertically = b.Width >= 2 * MinLeafWidth;
        var canSplitHorizontally = b.Height >= 2 * MinLeafHeight;
        if (!canSplitVertically && !canSplitHorizontally) return;

        bool splitVertically;
        if (canSplitVertically && !canSplitHorizontally) splitVertically = true;
        else if (!canSplitVertically && canSplitHorizontally) splitVertically = false;
        else if (b.Width > b.Height * SplitAspectRatio) splitVertically = true;
        else if (b.Height > b.Width * SplitAspectRatio) splitVertically = false;
        else splitVertically = rng.Next(2) == 0;

        if (splitVertically)
        {
            var min = MinLeafWidth;
            var max = b.Width - MinLeafWidth;
            if (max < min) return;
            var splitAt = rng.Next(min, max + 1);
            node.Left = new BspNode(new Rect(b.X, b.Y, splitAt, b.Height));
            node.Right = new BspNode(new Rect(b.X + splitAt, b.Y, b.Width - splitAt, b.Height));
        }
        else
        {
            var min = MinLeafHeight;
            var max = b.Height - MinLeafHeight;
            if (max < min) return;
            var splitAt = rng.Next(min, max + 1);
            node.Left = new BspNode(new Rect(b.X, b.Y, b.Width, splitAt));
            node.Right = new BspNode(new Rect(b.X, b.Y + splitAt, b.Width, b.Height - splitAt));
        }

        Split(node.Left!, depth + 1, rng);
        Split(node.Right!, depth + 1, rng);
    }

    // ---- Rooms ----

    private static void GenerateAndCarveRooms(BspNode node, Map map, Random rng, List<Rect> rooms)
    {
        if (node.IsLeaf)
        {
            var room = GenerateRoom(node.Bounds, rng);
            CarveRoom(map, room);
            rooms.Add(room);
            node.Room = room;
            return;
        }
        if (node.Left is not null) GenerateAndCarveRooms(node.Left, map, rng, rooms);
        if (node.Right is not null) GenerateAndCarveRooms(node.Right, map, rng, rooms);

        // Inherit a representative room from the left subtree (or the right
        // if the left is somehow null). ConnectSubtree uses this when the
        // parent corridor is between this node and its sibling — picking
        // a real room midpoint guarantees the corridor lands inside walkable
        // space rather than dead-ending in a wall.
        node.Room = node.Left?.Room ?? node.Right!.Room;
    }

    private static Rect GenerateRoom(Rect leaf, Random rng)
    {
        var maxRoomW = leaf.Width - 2 * RoomEdgeMargin;
        var maxRoomH = leaf.Height - 2 * RoomEdgeMargin;
        var roomW = rng.Next(MinRoomWidth, maxRoomW + 1);
        var roomH = rng.Next(MinRoomHeight, maxRoomH + 1);
        var slackX = maxRoomW - roomW;
        var slackY = maxRoomH - roomH;
        var roomX = leaf.X + RoomEdgeMargin + rng.Next(0, slackX + 1);
        var roomY = leaf.Y + RoomEdgeMargin + rng.Next(0, slackY + 1);
        return new Rect(roomX, roomY, roomW, roomH);
    }

    private static void CarveRoom(Map map, Rect room)
    {
        for (var x = room.X; x < room.X + room.Width; x++)
        for (var y = room.Y; y < room.Y + room.Height; y++)
            map.SetTile(new Position(x, y), TileTypes.Floor);
    }

    // ---- Corridors ----

    private static void ConnectSubtree(BspNode node, Map map, Random rng)
    {
        if (node.IsLeaf) return;
        if (node.Left is not null && node.Right is not null)
        {
            // Midpoint of representative ROOMS (not leaf bounding boxes) so
            // the corridor endpoints are guaranteed to fall inside floor
            // tiles. The previous bounds-midpoint approach could land both
            // endpoints in the wall margin between the room and the leaf
            // edge, leaving the room sealed.
            var a = MidpointOf(node.Left.Room);
            var b = MidpointOf(node.Right.Room);
            CarveCorridorL(map, a, b, rng);
        }
        if (node.Left is not null) ConnectSubtree(node.Left, map, rng);
        if (node.Right is not null) ConnectSubtree(node.Right, map, rng);
    }

    private static void CarveCorridorL(Map map, Position a, Position b, Random rng)
    {
        if (rng.Next(2) == 0)
        {
            CarveHorizontal(map, a.X, b.X, a.Y);
            CarveVertical(map, a.Y, b.Y, b.X);
        }
        else
        {
            CarveVertical(map, a.Y, b.Y, a.X);
            CarveHorizontal(map, a.X, b.X, b.Y);
        }
    }

    private static void CarveHorizontal(Map map, int x1, int x2, int y)
    {
        var min = Math.Min(x1, x2);
        var max = Math.Max(x1, x2);
        for (var x = min; x <= max; x++)
            map.SetTile(new Position(x, y), TileTypes.Floor);
    }

    private static void CarveVertical(Map map, int y1, int y2, int x)
    {
        var min = Math.Min(y1, y2);
        var max = Math.Max(y1, y2);
        for (var y = min; y <= max; y++)
            map.SetTile(new Position(x, y), TileTypes.Floor);
    }

    private static Position MidpointOf(Rect r) =>
        new(r.X + r.Width / 2, r.Y + r.Height / 2);

    // ---- Picking gameplay anchors ----

    private static Position RandomWalkableInRect(Map map, Rect rect, Random rng)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var x = rng.Next(rect.X, rect.X + rect.Width);
            var y = rng.Next(rect.Y, rect.Y + rect.Height);
            var p = new Position(x, y);
            if (map.IsWalkable(p)) return p;
        }
        // Defensive fallback: the carved room is guaranteed walkable, so we
        // should always succeed via the random path. Scan in order to be safe.
        for (var x = rect.X; x < rect.X + rect.Width; x++)
        for (var y = rect.Y; y < rect.Y + rect.Height; y++)
        {
            var p = new Position(x, y);
            if (map.IsWalkable(p)) return p;
        }
        throw new InvalidOperationException(
            $"No walkable tile in room ({rect.X},{rect.Y}) {rect.Width}x{rect.Height}.");
    }

    // BFS from `entry` over walkable tiles. Throws if any room has no tile
    // reachable from the entry — the caller's BSP / corridor logic produced
    // a disconnected map. Seed is in the message so the floor can be reproduced.
    private static void VerifyAllRoomsReachable(Map map, Position entry, List<Rect> rooms, int seed)
    {
        var visited = new bool[map.Width, map.Height];
        var queue = new Queue<Position>();
        queue.Enqueue(entry);
        visited[entry.X, entry.Y] = true;

        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            TryEnqueue(map, visited, queue, new Position(p.X - 1, p.Y));
            TryEnqueue(map, visited, queue, new Position(p.X + 1, p.Y));
            TryEnqueue(map, visited, queue, new Position(p.X, p.Y - 1));
            TryEnqueue(map, visited, queue, new Position(p.X, p.Y + 1));
        }

        foreach (var room in rooms)
        {
            var anyReached = false;
            for (var x = room.X; x < room.X + room.Width && !anyReached; x++)
            for (var y = room.Y; y < room.Y + room.Height && !anyReached; y++)
            {
                if (visited[x, y]) anyReached = true;
            }
            if (!anyReached)
                throw new InvalidOperationException(
                    $"BSP produced a disconnected room at ({room.X},{room.Y}) " +
                    $"{room.Width}x{room.Height} (seed={seed}). " +
                    "ConnectSubtree corridor logic regressed.");
        }
    }

    private static void TryEnqueue(Map map, bool[,] visited, Queue<Position> queue, Position p)
    {
        if (!map.IsWalkable(p)) return;
        if (visited[p.X, p.Y]) return;
        visited[p.X, p.Y] = true;
        queue.Enqueue(p);
    }

    private static Rect FarthestRoom(List<Rect> rooms, Rect from)
    {
        var fromCenter = new Position(from.CenterX, from.CenterY);
        Rect best = default;
        var bestDist = -1;
        foreach (var r in rooms)
        {
            if (r.Equals(from)) continue;
            var c = new Position(r.CenterX, r.CenterY);
            var d = fromCenter.ChebyshevTo(c);
            if (d > bestDist) { bestDist = d; best = r; }
        }
        return best;
    }

    private static IReadOnlyList<Position> ChooseEnemySpawns(
        Map map, List<Rect> rooms, Rect entryRoom, Rect bossRoom,
        Position entry, Position bossAnchor, int maxSlots, Random rng)
    {
        var occupied = new HashSet<Position> { entry, bossAnchor };
        var spawns = new List<Position>();

        // Candidate pool: every non-entry, non-boss room whose center is at
        // least MinSpawnDistanceFromEntry chebyshev tiles from the entry.
        // Skipping by room center (instead of per-tile) means a too-close
        // room never enters the pool and never wastes 20 attempts.
        var candidateRooms = new List<Rect>();
        foreach (var room in rooms)
        {
            if (room.Equals(entryRoom) || room.Equals(bossRoom)) continue;
            var center = new Position(room.CenterX, room.CenterY);
            if (entry.ChebyshevTo(center) < MinSpawnDistanceFromEntry) continue;
            candidateRooms.Add(room);
        }
        if (candidateRooms.Count == 0) return spawns;

        // First pass: one spawn per candidate room, in shuffled order so we
        // don't bias toward whichever rooms BSP traversal happened to visit
        // first. Guarantees that — up to the cap — every reachable
        // far-enough room gets at least one inhabitant. No more dead empty
        // rooms while the cap is consumed by the first three iterated.
        ShuffleInPlace(candidateRooms, rng);
        foreach (var room in candidateRooms)
        {
            if (spawns.Count >= maxSlots) break;
            TryPlaceInRoom(map, room, occupied, spawns, rng);
        }

        // Second pass: remaining slots distributed randomly across the same
        // pool. Some rooms end up with 2–3 spawns (clumps); most have 1.
        // Bail when consecutive picks fail — that means the rooms are
        // saturated and forcing more would loop forever.
        var consecutiveFailures = 0;
        var failureLimit = candidateRooms.Count * 2;
        while (spawns.Count < maxSlots && consecutiveFailures < failureLimit)
        {
            var room = candidateRooms[rng.Next(candidateRooms.Count)];
            if (TryPlaceInRoom(map, room, occupied, spawns, rng))
                consecutiveFailures = 0;
            else
                consecutiveFailures++;
        }

        return spawns;
    }

    private static bool TryPlaceInRoom(
        Map map, Rect room, HashSet<Position> occupied,
        List<Position> spawns, Random rng)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var x = rng.Next(room.X, room.X + room.Width);
            var y = rng.Next(room.Y, room.Y + room.Height);
            var p = new Position(x, y);
            if (!map.IsWalkable(p)) continue;
            if (occupied.Contains(p)) continue;
            occupied.Add(p);
            spawns.Add(p);
            return true;
        }
        return false;
    }

    private static void ShuffleInPlace<T>(IList<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ---- Internal helpers ----

    private sealed class BspNode
    {
        public Rect Bounds;
        public BspNode? Left;
        public BspNode? Right;

        // Representative room for this subtree. For a leaf, it's the room
        // carved inside the leaf's bounds. For an internal node, it's
        // inherited from the left subtree (any leaf-room works since the
        // subtree is connected internally) so the parent's corridor has a
        // guaranteed-walkable point to aim at.
        public Rect Room;

        public BspNode(Rect bounds) { Bounds = bounds; }
        public bool IsLeaf => Left is null && Right is null;
    }

    private readonly record struct Rect(int X, int Y, int Width, int Height)
    {
        public int CenterX => X + Width / 2;
        public int CenterY => Y + Height / 2;
    }
}
