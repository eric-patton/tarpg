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
        var movement = new MovementController();
        var combat = new CombatController();
        var loop = new GameLoopController(player, enemies, generated.Map, movement, combat);

        WireDefaultSkills(loop, classDef);

        // Spawn enemies. Mirrors GameScreen's SpawnPack + ApplyFloorScaling
        // logic so per-floor difficulty matches live play 1:1.
        foreach (var spawn in generated.EnemySpawnPoints)
        {
            var def = PickEnemyForZone(zone, rng);
            SpawnPack(def, spawn, enemies, generated.Map, player.Position, cfg.Floor);
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
            enemy.Damaged += (e, d) => damageDealt += d;
            enemy.Died += e =>
                killsByEnemyId[id] = killsByEnemyId.GetValueOrDefault(id) + 1;
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

            lastPlayerTile = player.Position;
            ticks++;
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

    // Default skill kit for each class. Mirrors GameScreen's slot wiring.
    // Lifted here so sim doesn't depend on UI / GameScreen directly. When
    // class definitions grow a "default skills" field this collapses to
    // a registry read.
    private static void WireDefaultSkills(GameLoopController loop, Tarpg.Classes.WalkerClassDefinition classDef)
    {
        if (classDef.Id == "reaver")
        {
            loop.SetSlotSkill(GameLoopController.SlotIndexM2, Registries.Skills.Get("heavy_strike"));
            loop.SetSlotSkill(GameLoopController.SlotIndexQ,  Registries.Skills.Get("cleave"));
            loop.SetSlotSkill(GameLoopController.SlotIndexW,  Registries.Skills.Get("charge"));
            loop.SetSlotSkill(GameLoopController.SlotIndexE,  Registries.Skills.Get("war_cry"));
            loop.SetSlotSkill(GameLoopController.SlotIndexR,  Registries.Skills.Get("whirlwind"));
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
