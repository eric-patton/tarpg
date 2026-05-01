using System.Numerics;
using Tarpg.Classes;
using Tarpg.Combat;
using Tarpg.Core;
using Tarpg.Enemies;
using Tarpg.Entities;
using Tarpg.Movement;
using Tarpg.Sim;
using Tarpg.Tests.Helpers;

namespace Tarpg.Tests.Sim;

public class GreedySimPilotTests
{
    private static SimContext NewContext(out GreedySimPilot pilot,
                                          out Player player,
                                          out List<Enemy> enemies,
                                          out CombatController combat,
                                          out MovementController movement,
                                          Position playerStart,
                                          Position threshold)
    {
        var map = TestMaps.OpenFloor(40, 40);
        player = Player.Create(Reaver.Definition, playerStart);
        enemies = new List<Enemy>();
        movement = new MovementController();
        combat = new CombatController();
        var loop = new GameLoopController(player, enemies, map, movement, combat);
        pilot = new GreedySimPilot();
        return new SimContext
        {
            Loop = loop,
            FloorThreshold = threshold,
            Rng = new Random(42),
        };
    }

    [Fact]
    public void Tick_NearestEnemy_SetsCombatTarget()
    {
        var ctx = NewContext(out var pilot, out var player, out var enemies, out var combat, out _,
            playerStart: new Position(10, 10), threshold: new Position(30, 30));
        var enemy = Enemy.Create(Wolf.Definition, new Position(15, 10));
        enemies.Add(enemy);

        pilot.Tick(ctx);

        Assert.Same(enemy, combat.Target);
    }

    [Fact]
    public void Tick_NoEnemies_RoutesToThreshold()
    {
        var ctx = NewContext(out var pilot, out var player, out _, out _, out var movement,
            playerStart: new Position(10, 10), threshold: new Position(30, 30));

        pilot.Tick(ctx);

        Assert.True(movement.HasGoal);
    }

    [Fact]
    public void Tick_TwoEnemiesAtNearEqualDistance_DoesNotSwapTargetEveryTick()
    {
        // Regression for the F3 seed=1009 timeout: two enemies at near-equal
        // Euclidean distance caused the greedy pilot to swap targets every
        // tick as the player jittered by tenths of a tile, which made
        // RetargetTo build conflicting paths (one east, one south) on
        // alternating frames — net player motion zero. The 25% hysteresis
        // margin in NearestLiveEnemy stops the thrash.
        var ctx = NewContext(out var pilot, out var player, out var enemies, out var combat, out _,
            playerStart: new Position(20, 20), threshold: new Position(35, 35));
        var enemyA = Enemy.Create(Wolf.Definition, new Position(25, 20));
        var enemyB = Enemy.Create(Wolf.Definition, new Position(20, 25));
        enemies.Add(enemyA);
        enemies.Add(enemyB);

        pilot.Tick(ctx);
        var first = combat.Target;
        Assert.NotNull(first);

        // Jitter the player position by less than a tile in a way that
        // would flip nearest under a strict argmin. With sticky targeting,
        // the pilot keeps `first` as the target.
        for (var i = 0; i < 5; i++)
        {
            player.ContinuousPosition = new Vector2(20.5f + 0.01f * i, 20.5f - 0.01f * i);
            pilot.Tick(ctx);
        }

        Assert.Same(first, combat.Target);
    }

    [Fact]
    public void Tick_MeaningfullyCloserEnemy_OverridesStickyTarget()
    {
        // Sticky targeting still allows switches when the new candidate is
        // significantly closer (>25% closer in dist²). Without this the
        // pilot would commit to a far enemy and ignore one that walked up.
        var ctx = NewContext(out var pilot, out var player, out var enemies, out var combat, out _,
            playerStart: new Position(20, 20), threshold: new Position(35, 35));
        var farEnemy = Enemy.Create(Wolf.Definition, new Position(30, 20));
        enemies.Add(farEnemy);

        pilot.Tick(ctx);
        Assert.Same(farEnemy, combat.Target);

        // Spawn a much closer enemy. The hysteresis gate should let it
        // become the new target since it's well under 75% of the current
        // distance squared.
        var closeEnemy = Enemy.Create(Wolf.Definition, new Position(22, 20));
        enemies.Add(closeEnemy);
        pilot.Tick(ctx);

        Assert.Same(closeEnemy, combat.Target);
    }

    [Fact]
    public void Tick_StuckOnUnreachableTarget_BailsToThresholdAfterStuckThreshold()
    {
        var ctx = NewContext(out var pilot, out var player, out var enemies, out var combat, out var movement,
            playerStart: new Position(10, 10), threshold: new Position(30, 30));
        var enemy = Enemy.Create(Wolf.Definition, new Position(15, 10));
        enemies.Add(enemy);

        // First tick — pilot picks the enemy as combat target.
        pilot.Tick(ctx);
        Assert.NotNull(combat.Target);

        // Simulate "stuck" — keep player position constant across many pilot
        // ticks (production loop would normally also call loop.Tick which
        // moves the player; we skip that to mimic the wall-slide deadlock).
        for (var i = 0; i < 70; i++) // > StuckThresholdTicks (60)
            pilot.Tick(ctx);

        // After the stuck threshold the pilot abandons combat and switches
        // to "walk to threshold" mode. Combat target is cleared; movement
        // has a fresh goal aimed at the threshold.
        Assert.Null(combat.Target);
        Assert.True(movement.HasGoal);
    }

    [Fact]
    public void Tick_StuckThenMoves_ResumesNormalEngagement()
    {
        var ctx = NewContext(out var pilot, out var player, out var enemies, out var combat, out var movement,
            playerStart: new Position(10, 10), threshold: new Position(30, 30));
        var enemy = Enemy.Create(Wolf.Definition, new Position(15, 10));
        enemies.Add(enemy);

        // Get into stuck state.
        for (var i = 0; i < 70; i++)
            pilot.Tick(ctx);
        Assert.Null(combat.Target);

        // Now simulate the player breaking free (e.g., enemy moved within
        // FOV / wall-slide resolved). Pilot detects movement, returns to
        // normal engagement on the next tick.
        player.SetTile(new Position(11, 10));
        pilot.Tick(ctx);

        Assert.Same(enemy, combat.Target);
    }

    [Fact]
    public void Tick_LowHp_FiresWarCryFromSlotE()
    {
        // Defensive pilot upgrade: when HP <= 50% and Rage is available,
        // greedy should cast War Cry (E) to heal. Without this the kit's
        // proactive heal sat unused at depth, contributing to the F8+
        // death wave that the balance-pass investigation surfaced.
        var ctx = NewContext(out var pilot, out var player, out var enemies, out _, out _,
            playerStart: new Position(10, 10), threshold: new Position(30, 30));
        // Need an enemy or pilot routes to threshold.
        enemies.Add(Enemy.Create(Wolf.Definition, new Position(15, 10)));
        // Wire War Cry into the E slot + give Reaver enough Rage.
        ctx.Loop.SetSlotSkill(GameLoopController.SlotIndexE, Tarpg.Skills.WarCry.Definition);
        player.Resource = player.MaxResource;
        player.Health = (int)(player.MaxHealth * 0.4f); // < 50%

        var hpBefore = player.Health;
        pilot.Tick(ctx);

        Assert.True(player.Health > hpBefore);
    }

    [Fact]
    public void Tick_CriticalHpAndPotionInBag_DrinksPotion()
    {
        // Below the panic threshold (30% HP) the pilot should reach for
        // the HP potion as a last-ditch heal — it's the only non-cooldown
        // option left after War Cry has been spent.
        var ctx = NewContext(out var pilot, out var player, out var enemies, out _, out _,
            playerStart: new Position(10, 10), threshold: new Position(30, 30));
        enemies.Add(Enemy.Create(Wolf.Definition, new Position(15, 10)));
        player.Inventory.Add(Tarpg.Items.Potions.HealthPotion);
        player.Health = (int)(player.MaxHealth * 0.2f); // < 30%

        var hpBefore = player.Health;
        pilot.Tick(ctx);

        // Potion was consumed AND HP increased. Two assertions because
        // either alone could be hit by the War Cry path (which we avoid
        // here by NOT wiring the E slot).
        Assert.Equal(0, player.Inventory.HealthPotionCount);
        Assert.True(player.Health > hpBefore);
    }
}
