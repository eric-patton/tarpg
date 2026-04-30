using System.Numerics;
using Tarpg.Classes;
using Tarpg.Core;
using Tarpg.Enemies;
using Tarpg.Entities;
using Tarpg.Tests.Helpers;

namespace Tarpg.Tests.Enemies.Ai;

public class RangedKiterAiTests
{
    [Fact]
    public void Tick_PlayerInsidePreferredBand_BackpedalsAway()
    {
        var map = TestMaps.OpenFloor(30, 20);
        var player = Player.Create(Reaver.Definition, new Position(10, 10));
        // Place the howler 2 tiles east of the player — inside PreferredDistanceMin (4).
        var enemy = Enemy.Create(Howler.Definition, new Position(12, 10));
        map.ComputeFovFor(player.Position, GameLoopController.FovRadius);

        var startX = enemy.ContinuousPosition.X;
        for (var i = 0; i < 60; i++) // 1 sim-second
            enemy.Ai.Tick(enemy, player, map, 1f / 60f, 1.0f);

        // Howler should retreat — its X grows farther from the player at x=10.5.
        Assert.True(enemy.ContinuousPosition.X > startX,
            $"Expected howler to backpedal; x went from {startX} to {enemy.ContinuousPosition.X}");
    }

    [Fact]
    public void Tick_PlayerInBandAndLos_FiresHitscanOnCooldown()
    {
        var map = TestMaps.OpenFloor(30, 20);
        var player = Player.Create(Reaver.Definition, new Position(10, 10));
        // 5 tiles east — inside the [4, 6] preferred band, in attack range.
        var enemy = Enemy.Create(Howler.Definition, new Position(15, 10));
        map.ComputeFovFor(player.Position, GameLoopController.FovRadius);

        var initialHp = player.Health;
        // First tick fires (cooldown starts at 0).
        enemy.Ai.Tick(enemy, player, map, 1f / 60f, 1.0f);

        Assert.Equal(initialHp - enemy.Damage, player.Health);
    }

    [Fact]
    public void Tick_PlayerTooFar_AdvancesToward()
    {
        var map = TestMaps.OpenFloor(30, 20);
        var player = Player.Create(Reaver.Definition, new Position(5, 10));
        // 10 tiles east — outside PreferredDistanceMax (6). Outside FOV
        // (radius 10 measured from player at x=5 → enemy at x=15 is on
        // the boundary), but the kiter has aggro-memory once it sees us
        // and can advance even on stale memory.
        var enemy = Enemy.Create(Howler.Definition, new Position(15, 10));
        map.ComputeFovFor(player.Position, GameLoopController.FovRadius);

        // Move player closer briefly to seed aggro, then back.
        var seedPlayerPos = player.ContinuousPosition;
        player.SetTile(new Position(10, 10));
        map.ComputeFovFor(player.Position, GameLoopController.FovRadius);
        enemy.Ai.Tick(enemy, player, map, 1f / 60f, 1.0f);

        // Restore player far away.
        player.ContinuousPosition = seedPlayerPos;
        map.ComputeFovFor(player.Position, GameLoopController.FovRadius);

        var startX = enemy.ContinuousPosition.X;
        for (var i = 0; i < 30; i++)
            enemy.Ai.Tick(enemy, player, map, 1f / 60f, 1.0f);

        // Howler should close toward last-seen position (advancing toward the player).
        Assert.True(enemy.ContinuousPosition.X < startX,
            $"Expected howler to advance toward last-seen player; x went from {startX} to {enemy.ContinuousPosition.X}");
    }
}
