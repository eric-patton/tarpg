using Tarpg.Classes;
using Tarpg.Core;
using Tarpg.Enemies;
using Tarpg.Entities;
using Tarpg.Tests.Helpers;

namespace Tarpg.Tests.Enemies.Ai;

// FOV-symmetric LOS aggro: the AI considers itself "seen by player" iff the
// player's FOV mask covers the enemy's tile. The map's ComputeFovFor is the
// only way to populate that mask, so each test seeds the FOV from the
// player's position before calling Tick.
public class MeleeChargerAiTests
{
    [Fact]
    public void Tick_PlayerInFov_ChasesPlayer()
    {
        var map = TestMaps.OpenFloor(30, 20);
        var player = Player.Create(Reaver.Definition, new Position(5, 10));
        var enemy = Enemy.Create(Wolf.Definition, new Position(8, 10));
        map.ComputeFovFor(player.Position, GameLoopController.FovRadius);

        var startX = enemy.ContinuousPosition.X;
        for (var i = 0; i < 60; i++) // 1 sim-second at 60Hz
            enemy.Ai.Tick(enemy, player, map, deltaSec: 1f / 60f, cellAspect: 1.0f);

        // Enemy should have moved toward the player (decreasing X).
        Assert.True(enemy.ContinuousPosition.X < startX,
            $"Expected enemy to advance toward player; x went from {startX} to {enemy.ContinuousPosition.X}");
    }

    [Fact]
    public void Tick_PlayerInRange_DealsDamageOnAttackCooldown()
    {
        var map = TestMaps.OpenFloor(20, 20);
        var player = Player.Create(Reaver.Definition, new Position(10, 10));
        var enemy = Enemy.Create(Wolf.Definition, new Position(11, 10)); // adjacent
        map.ComputeFovFor(player.Position, GameLoopController.FovRadius);

        var initialHp = player.Health;
        // First Tick fires the attack (cooldown starts at 0). Subsequent
        // Ticks within the same cooldown window won't fire again.
        enemy.Ai.Tick(enemy, player, map, 1f / 60f, 1.0f);

        Assert.Equal(initialHp - enemy.Damage, player.Health);
    }

    [Fact]
    public void Tick_PlayerOutOfFov_StopsAfterAggroMemoryExpires()
    {
        var map = TestMaps.OpenFloor(30, 20);
        var player = Player.Create(Reaver.Definition, new Position(5, 10));
        var enemy = Enemy.Create(Wolf.Definition, new Position(8, 10));
        map.ComputeFovFor(player.Position, GameLoopController.FovRadius);

        // First seed aggro by ticking once with player visible.
        enemy.Ai.Tick(enemy, player, map, 1f / 60f, 1.0f);

        // Move player far away and recompute FOV so enemy falls outside it.
        player.SetTile(new Position(25, 10));
        map.ComputeFovFor(player.Position, GameLoopController.FovRadius);

        // Tick for longer than AggroMemorySec (3.0s).
        var enemyXBefore = enemy.ContinuousPosition.X;
        for (var i = 0; i < 60 * 4; i++) // 4 sim-seconds
            enemy.Ai.Tick(enemy, player, map, 1f / 60f, 1.0f);

        // The enemy can't perceive the player after the 3s memory window,
        // so it should have ended this run idle. We verify by ticking once
        // more and confirming no movement on that tick.
        var xBeforeFinal = enemy.ContinuousPosition.X;
        enemy.Ai.Tick(enemy, player, map, 1f / 60f, 1.0f);
        var xAfterFinal = enemy.ContinuousPosition.X;
        Assert.Equal(xBeforeFinal, xAfterFinal);
    }
}
