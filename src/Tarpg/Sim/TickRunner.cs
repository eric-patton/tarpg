using SadRogue.Primitives;
using Tarpg.Combat;
using Tarpg.Core;
using Tarpg.Enemies;
using Tarpg.Entities;
using Tarpg.Movement;
using Tarpg.Skills;
using Tarpg.World;

namespace Tarpg.Sim;

// Headless single-floor simulation runner. Builds a fresh floor + player +
// enemy roster from a SimConfig, drives the loop tick-by-tick under the
// supplied pilot, accumulates metrics, returns when the player clears
// (threshold or all enemies dead), dies, or hits MaxTicks.
//
// All RNG is derived from cfg.Seed so two runs with the same config produce
// identical timelines — required for reproducible balance tuning.
public static class TickRunner
{
    private const int PackSpreadRadiusMax = 3;
    private const float HpScalePerFloor = 0.15f;
    private const float DmgScalePerFloor = 0.10f;

    public static SimResult Run(SimConfig cfg, ISimPilot pilot)
    {
        var rng = new Random(cfg.Seed);
        var floorSeed = rng.Next();

        ContentInitializer.Initialize();
        var zone = Registries.Zones.Get(cfg.ZoneId);
        var classDef = Registries.Classes.Get(cfg.ClassId);
        var generated = zone.Generator.Generate(cfg.MapWidth, cfg.MapHeight, floorSeed, cfg.Floor);

        var player = Player.Create(classDef, generated.Entry);
        var enemies = new List<Enemy>();
        var floorItems = new List<Tarpg.Entities.FloorItem>();
        var movement = new MovementController();
        var combat = new CombatController();
        var loop = new GameLoopController(player, enemies, generated.Map, movement, combat, floorItems);

        WireDefaultSkills(loop, classDef);

        // Spawn enemies. Mirrors GameScreen's SpawnPack + ApplyFloorScaling
        // logic so per-floor difficulty matches live play 1:1.
        foreach (var spawn in generated.EnemySpawnPoints)
        {
            var def = PickEnemyForZone(zone, rng);
            SpawnPack(def, spawn, enemies, generated.Map, player.Position, cfg.Floor);
        }

        // Boss spawn — mirrors GameScreen.LoadFloor's boss-floor branch
        // so sim measures the same encounter shape as live play. Without
        // this the BossAnchor tile placed by BSP would never convert to
        // Threshold and the floor would time out for both pilots.
        if (Tarpg.World.Generation.BspGenerator.BossFloors.Contains(cfg.Floor))
        {
            var boss = Enemy.Create(Tarpg.Enemies.WolfMother.Definition, generated.BossAnchor);
            ApplyFloorScaling(boss, cfg.Floor);
            enemies.Add(boss);
        }

        generated.Map.ComputeFovFor(player.Position, GameLoopController.FovRadius);

        // Metrics accumulate via Damaged / Died subscriptions on every entity
        // (player + every enemy at spawn time).
        var damageDealt = 0;
        var damageTaken = 0;
        var hpMin = player.Health;
        var killsByEnemyId = new Dictionary<string, int>();

        player.Damaged += (e, d) =>
        {
            damageTaken += d;
            if (player.Health < hpMin) hpMin = player.Health;
        };

        var initialEnemyCount = enemies.Count;
        foreach (var enemy in enemies)
        {
            var id = enemy.Definition.Id;
            var capturedEnemy = enemy;
            enemy.Damaged += (e, d) => damageDealt += d;
            enemy.Died += e =>
            {
                killsByEnemyId[id] = killsByEnemyId.GetValueOrDefault(id) + 1;
                // Mirror GameScreen's loot-drop behavior so sim measures
                // the same equipment economy as live play. Boss drops
                // (Wolfbreaker) skip the random LootDropper roll —
                // they're deterministic per the boss-loot contract.
                if (capturedEnemy.Definition.IsBoss)
                {
                    if (Registries.Items.TryGet("wolfbreaker", out var loot))
                        floorItems.Add(Tarpg.Entities.FloorItem.Create(
                            loot, capturedEnemy.Position, new SadRogue.Primitives.Color(220, 180, 110)));
                }
                else
                {
                    var dropped = Tarpg.Items.LootDropper.RollDrop(
                        capturedEnemy, rng, GameLoopController.LootDropChance);
                    if (dropped is not null) floorItems.Add(dropped);
                }
            };
        }

        var ctx = new SimContext
        {
            Loop = loop,
            FloorThreshold = generated.BossAnchor,
            Rng = rng,
        };

        var lastPlayerTile = player.Position;
        SimOutcome outcome = SimOutcome.Timeout;
        var ticks = 0;

        // Position trace for timeout diagnostics — sample every 10 sim-seconds
        // so a stuck-pilot run leaves a breadcrumb trail. Cleared earlier in
        // the function (we can't tell from inside the loop whether the run
        // will time out, so we always collect; only DumpTimeoutDiagnostic
        // emits them on actual timeouts).
        var trace = new List<(int Tick, Position Tile, int CombatTargetHp)>();
        var traceEverySimSec = 1f;
        var nextTraceAtSec = 0f;

        while (ticks < cfg.MaxTicks)
        {
            pilot.Tick(ctx);

            loop.Tick(cfg.TickDeltaSec, cellAspect: 1.0f, frozen: false, lastPlayerTile);

            // Reap dead so subsequent pilot ticks see only live targets.
            for (var i = enemies.Count - 1; i >= 0; i--)
            {
                if (enemies[i].IsDead) enemies.RemoveAt(i);
            }

            if (loop.PlayerDied) { outcome = SimOutcome.PlayerDied; break; }
            if (loop.SteppedOnThreshold) { outcome = SimOutcome.PlayerCleared; break; }
            if (enemies.Count == 0)
            {
                // Pilot will route toward threshold next tick; let it
                // continue rather than declaring victory immediately so
                // the threshold-step path still gets exercised.
                if (lastPlayerTile == generated.BossAnchor)
                {
                    outcome = SimOutcome.PlayerCleared;
                    break;
                }
            }

            var simSec = ticks * cfg.TickDeltaSec;
            if (simSec >= nextTraceAtSec)
            {
                trace.Add((ticks, player.Position, combat.Target?.Health ?? -1));
                nextTraceAtSec += traceEverySimSec;
            }

            lastPlayerTile = player.Position;
            ticks++;
        }

        if (outcome == SimOutcome.Timeout)
        {
            DumpTimeoutDiagnostic(cfg, player, enemies, generated.BossAnchor, trace);
            DumpNeighborhood(generated.Map, player.Position);
        }

        return new SimResult
        {
            Outcome = outcome,
            TicksElapsed = ticks,
            SimSeconds = ticks * cfg.TickDeltaSec,
            InitialEnemyCount = initialEnemyCount,
            EnemiesKilled = initialEnemyCount - enemies.Count,
            PlayerDamageDealt = damageDealt,
            PlayerDamageTaken = damageTaken,
            PlayerHpAtEnd = player.Health,
            PlayerHpMin = hpMin,
            SkillUses = ctx.SkillCastsThisRun,
            KillsByEnemyId = killsByEnemyId,
        };
    }

    // Apply the class's StartingSlotSkills to each slot. Same loop as
    // GameScreen.WireSlotSkills — the two stay in sync because both read
    // from the same WalkerClassDefinition.StartingSlotSkills source of truth.
    private static void WireDefaultSkills(GameLoopController loop, Tarpg.Classes.WalkerClassDefinition classDef)
    {
        for (var i = 0; i < classDef.StartingSlotSkills.Count; i++)
        {
            var skillId = classDef.StartingSlotSkills[i];
            if (skillId is null) continue;
            loop.SetSlotSkill(i, Registries.Skills.Get(skillId));
        }
    }

    private static EnemyDefinition PickEnemyForZone(ZoneDefinition zone, Random rng)
    {
        var totalWeight = 0;
        foreach (var def in Registries.Enemies.All)
        {
            if (!def.ZoneIds.Contains(zone.Id)) continue;
            if (def.RarityWeight <= 0) continue;
            totalWeight += def.RarityWeight;
        }
        if (totalWeight == 0)
            throw new InvalidOperationException(
                $"No spawnable enemies registered for zone '{zone.Id}'.");

        var pick = rng.Next(totalWeight);
        foreach (var def in Registries.Enemies.All)
        {
            if (!def.ZoneIds.Contains(zone.Id)) continue;
            if (def.RarityWeight <= 0) continue;
            pick -= def.RarityWeight;
            if (pick < 0) return def;
        }
        throw new InvalidOperationException("Weighted enemy roll fell through.");
    }

    private static void SpawnPack(
        EnemyDefinition def, Position center, List<Enemy> enemies,
        Map map, Position playerTile, int floor)
    {
        var occupied = new HashSet<Position> { playerTile };
        foreach (var enemy in enemies)
            occupied.Add(enemy.Position);

        var packSize = Math.Max(1, def.PackSize);
        var placed = 0;

        if (TryPlace(def, center, enemies, map, occupied, floor))
        {
            occupied.Add(center);
            placed++;
        }

        for (var radius = 1; placed < packSize && radius <= PackSpreadRadiusMax; radius++)
        {
            for (var dy = -radius; dy <= radius && placed < packSize; dy++)
            for (var dx = -radius; dx <= radius && placed < packSize; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                var p = new Position(center.X + dx, center.Y + dy);
                if (!TryPlace(def, p, enemies, map, occupied, floor)) continue;
                occupied.Add(p);
                placed++;
            }
        }
    }

    private static bool TryPlace(
        EnemyDefinition def, Position p, List<Enemy> enemies,
        Map map, HashSet<Position> occupied, int floor)
    {
        if (!map.IsWalkable(p)) return false;
        if (occupied.Contains(p)) return false;
        var enemy = Enemy.Create(def, p);
        ApplyFloorScaling(enemy, floor);
        enemies.Add(enemy);
        return true;
    }

    // Timeout diagnostic — emit player + threshold + alive-enemy positions
    // so we can see where the pilot got stuck. Cheap to leave on always: a
    // run that completes (cleared / died) takes this code path zero times.
    private static void DumpTimeoutDiagnostic(
        SimConfig cfg, Player player, List<Enemy> enemies, Position threshold,
        List<(int Tick, Position Tile, int CombatTargetHp)> trace)
    {
        Console.WriteLine($"  # timeout-diag F{cfg.Floor} seed={cfg.Seed}: " +
                          $"player=({player.Position.X},{player.Position.Y}) " +
                          $"threshold=({threshold.X},{threshold.Y}) " +
                          $"player-to-threshold-chebyshev={player.Position.ChebyshevTo(threshold)}");
        Console.WriteLine($"  # alive enemies ({enemies.Count}):");
        foreach (var e in enemies)
        {
            Console.WriteLine($"  #   {e.Definition.Id} at ({e.Position.X},{e.Position.Y}) " +
                              $"hp={e.Health}/{e.MaxHealth} cheby-to-player={e.Position.ChebyshevTo(player.Position)}");
        }
        Console.WriteLine($"  # position trace ({trace.Count} samples):");
        foreach (var t in trace)
        {
            Console.WriteLine($"  #   tick={t.Tick} player=({t.Tile.X},{t.Tile.Y}) target_hp={t.CombatTargetHp}");
        }
    }

    // Print a 21x21 ASCII view of the map around `center` so a stuck-tile
    // diagnostic can show what corridors / walls boxed the player in.
    private static void DumpNeighborhood(Map map, Position center)
    {
        const int Half = 10;
        Console.WriteLine($"  # {2*Half+1}x{2*Half+1} around ({center.X},{center.Y}) [@ = player tile, > = threshold]:");
        for (var dy = -Half; dy <= Half; dy++)
        {
            var row = "  #   ";
            for (var dx = -Half; dx <= Half; dx++)
            {
                var p = new Position(center.X + dx, center.Y + dy);
                if (!map.InBounds(p)) { row += "?"; continue; }
                if (dx == 0 && dy == 0) { row += "@"; continue; }
                if (map[p].Type == TileTypes.Threshold) { row += ">"; continue; }
                row += map.IsWalkable(p) ? "." : "#";
            }
            Console.WriteLine(row);
        }
    }

    private static void ApplyFloorScaling(Enemy enemy, int floor)
    {
        if (floor <= 1) return;
        var depth = floor - 1;
        var hpScale = 1f + HpScalePerFloor * depth;
        var dmgScale = 1f + DmgScalePerFloor * depth;
        enemy.MaxHealth = Math.Max(1, (int)MathF.Round(enemy.Definition.BaseHealth * hpScale));
        enemy.Health = enemy.MaxHealth;
        enemy.Damage = Math.Max(1, (int)MathF.Round(enemy.Definition.BaseDamage * dmgScale));
    }
}
