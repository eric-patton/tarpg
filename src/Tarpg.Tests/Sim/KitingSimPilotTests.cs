using System.Numerics;
using Tarpg.Classes;
using Tarpg.Combat;
using Tarpg.Core;
using Tarpg.Enemies;
using Tarpg.Entities;
using Tarpg.Movement;
using Tarpg.Sim;
using Tarpg.Skills;
using Tarpg.Tests.Helpers;
using Tarpg.World;

namespace Tarpg.Tests.Sim;

// Behavioral coverage for the Hunter-flavored kiter. Wires the Hunter
// kit directly via SetSlotSkill so the registry / ContentInitializer
// doesn't need to run for these tests.
public class KitingSimPilotTests
{
    private static SimContext NewContext(out KitingSimPilot pilot,
                                          out Player player,
                                          out List<Enemy> enemies,
                                          out CombatController combat,
                                          out MovementController movement,
                                          out GameLoopController loop,
                                          Position playerStart,
                                          Position threshold,
                                          Map? map = null)
    {
        map ??= TestMaps.OpenFloor(40, 40);
        player = Player.Create(Hunter.Definition, playerStart);
        // Top up Focus so skill-cost gates don't gate-keep the test scenarios.
        // (Live play earns Resource via auto-attack hits, but the kiter never
        // auto-attacks and tests want Focus pre-seeded.)
        player.Resource = player.MaxResource;
        enemies = new List<Enemy>();
        movement = new MovementController();
        combat = new CombatController();
        loop = new GameLoopController(player, enemies, map, movement, combat);
        loop.SetSlotSkill(GameLoopController.SlotIndexM2, QuickShot.Definition);
        loop.SetSlotSkill(GameLoopController.SlotIndexQ, Volley.Definition);
        loop.SetSlotSkill(GameLoopController.SlotIndexW, Roll.Definition);
        loop.SetSlotSkill(GameLoopController.SlotIndexE, Bandage.Definition);
        loop.SetSlotSkill(GameLoopController.SlotIndexR, RainOfArrows.Definition);

        pilot = new KitingSimPilot();
        return new SimContext
        {
            Loop = loop,
            FloorThreshold = threshold,
            Rng = new Random(42),
        };
    }

    [Fact]
    public void Tick_NoEnemies_RoutesToThreshold()
    {
        var ctx = NewContext(out var pilot, out _, out _, out _, out var movement, out _,
            playerStart: new Position(10, 10), threshold: new Position(30, 30));

        pilot.Tick(ctx);

        Assert.True(movement.HasGoal);
    }

    [Fact]
    public void Tick_EnemyInKiteBandWithLos_HoldsPositionAndShoots()
    {
        // Enemy 4 tiles east of player → in kite band (3..6) with clear LOS.
        // Pilot should call Movement.Stop (no movement goal) and fire QuickShot.
        var ctx = NewContext(out var pilot, out var player, out var enemies, out _, out var movement, out _,
            playerStart: new Position(10, 10), threshold: new Position(30, 30));
        var enemy = Enemy.Create(Wolf.Definition, new Position(14, 10));
        enemies.Add(enemy);

        var initialHp = enemy.Health;
        pilot.Tick(ctx);

        Assert.False(movement.HasGoal);
        Assert.True(enemy.Health < initialHp);
    }

    [Fact]
    public void Tick_EnemyInKiteBand_NeverSetsCombatTarget()
    {
        // The kiter relies on skills, not auto-attack — Combat.Target must
        // never get set, otherwise GameLoopController would override our
        // movement by chasing the target.
        var ctx = NewContext(out var pilot, out _, out var enemies, out var combat, out _, out _,
            playerStart: new Position(10, 10), threshold: new Position(30, 30));
        enemies.Add(Enemy.Create(Wolf.Definition, new Position(14, 10)));

        for (var i = 0; i < 10; i++) pilot.Tick(ctx);

        Assert.Null(combat.Target);
    }

    [Fact]
    public void Tick_EnemyInDangerZone_RollsOrFlees()
    {
        // Enemy chebyshev=1 to the player → danger zone. Pilot tries Roll
        // first; with Focus topped up + cooldown clear it should land us
        // multiple tiles west (away from the eastern threat).
        var ctx = NewContext(out var pilot, out var player, out var enemies, out _, out _, out _,
            playerStart: new Position(15, 15), threshold: new Position(35, 35));
        enemies.Add(Enemy.Create(Wolf.Definition, new Position(16, 15)));

        var startTile = player.Position;
        pilot.Tick(ctx);

        // Roll moves up to 4 tiles away; the new tile should be further west.
        Assert.True(player.Position.X < startTile.X);
    }

    [Fact]
    public void Tick_EnemyInDangerZone_RollOnCooldown_FleesOnFoot()
    {
        // Disable Roll (no slot) to force the foot-flee branch — pilot must
        // still issue a movement goal away from the threat instead of holding.
        var ctx = NewContext(out var pilot, out var player, out var enemies, out _, out var movement, out var loop,
            playerStart: new Position(15, 15), threshold: new Position(35, 35));
        loop.SetSlotSkill(GameLoopController.SlotIndexW, null);
        enemies.Add(Enemy.Create(Wolf.Definition, new Position(16, 15)));

        pilot.Tick(ctx);

        Assert.True(movement.HasGoal);
    }

    [Fact]
    public void Tick_EnemyOutOfRange_ClosesIn()
    {
        // Enemy at chebyshev=15, well beyond max kite range → pilot routes
        // toward the enemy.
        var ctx = NewContext(out var pilot, out var player, out var enemies, out _, out var movement, out _,
            playerStart: new Position(5, 10), threshold: new Position(30, 30));
        enemies.Add(Enemy.Create(Wolf.Definition, new Position(20, 10)));

        pilot.Tick(ctx);

        Assert.True(movement.HasGoal);
    }

    [Fact]
    public void Tick_LowHp_CastsBandage()
    {
        var ctx = NewContext(out var pilot, out var player, out var enemies, out _, out _, out _,
            playerStart: new Position(10, 10), threshold: new Position(30, 30));
        // Drop player below the 50% bandage trigger.
        player.Health = 10;
        // Put an enemy in kite band so the pilot reaches its main loop
        // (no enemies → routes to threshold and never tries E).
        enemies.Add(Enemy.Create(Wolf.Definition, new Position(14, 10)));

        pilot.Tick(ctx);

        // Bandage heals 25 — exact amount shouldn't matter, just that the
        // skill fired (cooldown burned, HP bumped).
        Assert.True(player.Health > 10);
    }

    [Fact]
    public void Tick_StandingStillInKiteBand_DoesNotBailToThreshold()
    {
        // Regression: the stuck-detector counts ticks-without-tile-change.
        // When the kiter is *intentionally* standing still in the kite band
        // to fire QuickShot, that must not falsely tick toward the bail
        // threshold — otherwise every successful kite would degrade after
        // a sim-second to "drop combat, walk to threshold."
        var ctx = NewContext(out var pilot, out _, out var enemies, out var combat, out var movement, out _,
            playerStart: new Position(10, 10), threshold: new Position(30, 30));
        enemies.Add(Enemy.Create(Wolf.Definition, new Position(14, 10)));

        // 100 ticks of standing-and-firing.
        for (var i = 0; i < 100; i++) pilot.Tick(ctx);

        // Pilot should still be in normal mode: no combat target (we never
        // set one) AND no movement goal toward the threshold (i.e. the bail
        // branch hasn't engaged — when bailing the pilot writes a fresh
        // RetargetTo every tick, which keeps HasGoal=true).
        Assert.False(movement.HasGoal);
        Assert.Null(combat.Target);
    }

    [Fact]
    public void Tick_StuckOnUnreachableTarget_BailsToThresholdAfterStuckThreshold()
    {
        // Mirror of GreedySimPilot's stuck-bail test. Enemy is placed
        // outside max kite range so the pilot is in "close in" mode on
        // every tick — it issues RetargetTo at the enemy each tick. We
        // skip loop.Tick (no actual movement happens), so player.Position
        // stays put and the stuck counter ticks toward bail.
        //
        // Important: the enemy must NOT be in kite band, otherwise the
        // pilot deliberately stands still and resets the stuck counter.
        var ctx = NewContext(out var pilot, out var player, out var enemies, out _, out var movement, out _,
            playerStart: new Position(5, 10), threshold: new Position(30, 30));
        enemies.Add(Enemy.Create(Wolf.Definition, new Position(20, 10))); // chebyshev=15, beyond kite band

        for (var i = 0; i < 70; i++) // > StuckThresholdTicks (60)
            pilot.Tick(ctx);

        // After bailing, pilot writes a fresh RetargetTo at the threshold
        // every tick — HasGoal stays true.
        Assert.True(movement.HasGoal);
    }

    [Fact]
    public void Tick_PinnedByWallNoLos_CommitsToFightAfterThreshold()
    {
        // The 1018-F2 regression: a wolfshade in an alcove sits at cheby=2
        // with a wall blocking LOS. Pilot oscillates between "retreat"
        // and "close in" forever, never firing a shot. After enough ticks
        // (CommitFightThresholdTicks=180, ~3 sim-sec) the pilot must
        // commit to a melee engagement so the run progresses.
        //
        // Setup mimics the original geometry: a single wall tile between
        // the player and a wolfshade-equivalent target, both close enough
        // that the pin condition holds.
        var map = TestMaps.OpenFloorWithWalls(40, 40, new Position(11, 10));
        var ctx = NewContext(out var pilot, out _, out var enemies, out var combat, out _, out _,
            playerStart: new Position(10, 10), threshold: new Position(30, 30), map: map);
        // Enemy at (12, 10): cheby=2 to player, wall at (11,10) blocks LOS.
        enemies.Add(Enemy.Create(Wolf.Definition, new Position(12, 10)));

        for (var i = 0; i < 200; i++) pilot.Tick(ctx);

        // Past the commit-fight threshold: combat target now points at
        // the pinning enemy so the loop's chase + auto-attack path drives
        // the engagement instead of the kiter's retreat oscillator.
        Assert.NotNull(combat.Target);
    }

    [Fact]
    public void Tick_CommitFight_ResetsWhenLosClears()
    {
        // Once committed to fight, regaining LOS to the threat means we
        // can kite again — the kiter must drop the combat target to stop
        // the loop's chase behavior and resume normal behavior.
        var map = TestMaps.OpenFloorWithWalls(40, 40, new Position(11, 10));
        var ctx = NewContext(out var pilot, out _, out var enemies, out var combat, out _, out _,
            playerStart: new Position(10, 10), threshold: new Position(30, 30), map: map);
        enemies.Add(Enemy.Create(Wolf.Definition, new Position(12, 10)));

        for (var i = 0; i < 200; i++) pilot.Tick(ctx);
        Assert.NotNull(combat.Target);

        // Knock the wall down — LOS now clear from player to enemy.
        map.SetTile(new Position(11, 10), TileTypes.Floor);
        pilot.Tick(ctx);

        Assert.Null(combat.Target);
    }
    [Fact]
    public void Tick_Cluster_FiresAoeOnDensestTile()
    {
        // Three enemies clumped at (15,10), (16,10), (15,11) → cluster of 3
        // within chebyshev-1 of the center. Player at (10,10) is within
        // both Volley range (6) and Rain of Arrows range (8). Both should
        // fire on the first tick (independent slots / cooldowns).
        var ctx = NewContext(out var pilot, out _, out var enemies, out _, out _, out _,
            playerStart: new Position(10, 10), threshold: new Position(30, 30));
        var a = Enemy.Create(Wolf.Definition, new Position(15, 10));
        var b = Enemy.Create(Wolf.Definition, new Position(16, 10));
        var c = Enemy.Create(Wolf.Definition, new Position(15, 11));
        enemies.Add(a); enemies.Add(b); enemies.Add(c);

        var totalHp = a.Health + b.Health + c.Health;
        pilot.Tick(ctx);

        // Cluster should have eaten damage from at least one AOE skill.
        // (Volley = 8 each within radius-1; RainOfArrows = 18 each within
        // radius-2. Even if only one fires the total drops.)
        Assert.True(a.Health + b.Health + c.Health < totalHp);
    }
}
