using Tarpg.Classes;
using Tarpg.Combat;
using Tarpg.Core;
using Tarpg.Enemies;
using Tarpg.Entities;
using Tarpg.Movement;
using Tarpg.Tests.Helpers;

namespace Tarpg.Tests.Items;

// Mirrors PotionPickupTests for the corpse-on-tile case. The death-loop
// flow lives in GameScreen (UI-coupled), but the *pickup* lives on
// GameLoopController.TryPickupCorpses, which we can drive headlessly.
public class CorpsePickupTests
{
    private static GameLoopController NewLoop(out Player player, out List<Corpse> corpses)
    {
        var map = TestMaps.OpenFloor(20, 20);
        player = Player.Create(Reaver.Definition, new Position(5, 5));
        var enemies = new List<Enemy>();
        var floorItems = new List<Tarpg.Entities.FloorItem>();
        corpses = new List<Corpse>();
        var movement = new MovementController();
        var combat = new CombatController();
        return new GameLoopController(player, enemies, map, movement, combat, floorItems, corpses);
    }

    [Fact]
    public void Tick_PlayerOnCorpse_RestoresPotionsAndRemovesCorpse()
    {
        var loop = NewLoop(out var player, out var corpses);
        corpses.Add(Corpse.CreateAt(player.Position, hpPotionCount: 3, resourcePotionCount: 2));

        loop.Tick(deltaSec: 1f / 60f, cellAspect: 1.0f, frozen: false, lastPlayerTile: player.Position);

        Assert.Empty(corpses);
        Assert.Equal(3, player.Inventory.HealthPotionCount);
        Assert.Equal(2, player.Inventory.ResourcePotionCount);
    }

    [Fact]
    public void Tick_PlayerOnDifferentTile_LeavesCorpse()
    {
        var loop = NewLoop(out var player, out var corpses);
        corpses.Add(Corpse.CreateAt(new Position(10, 10), hpPotionCount: 1, resourcePotionCount: 1));

        loop.Tick(1f / 60f, 1.0f, frozen: false, lastPlayerTile: player.Position);

        Assert.Single(corpses);
        Assert.Equal(0, player.Inventory.HealthPotionCount);
        Assert.Equal(0, player.Inventory.ResourcePotionCount);
    }

    [Fact]
    public void Tick_CorpsePickup_AddsToExistingInventory()
    {
        // Player grabbed a fresh potion post-respawn before reaching the
        // corpse — the corpse drain must add, not overwrite.
        var loop = NewLoop(out var player, out var corpses);
        player.Inventory.Add(Tarpg.Items.Potions.HealthPotion);
        corpses.Add(Corpse.CreateAt(player.Position, hpPotionCount: 2, resourcePotionCount: 0));

        loop.Tick(1f / 60f, 1.0f, frozen: false, lastPlayerTile: player.Position);

        Assert.Equal(3, player.Inventory.HealthPotionCount);
    }
}
