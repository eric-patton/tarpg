using Tarpg.Classes;
using Tarpg.Combat;
using Tarpg.Core;
using Tarpg.Enemies;
using Tarpg.Entities;
using Tarpg.Items;
using Tarpg.Movement;
using Tarpg.Tests.Helpers;

namespace Tarpg.Tests.Items;

// Pickup logic lives in GameLoopController.Tick — these tests drive Tick
// directly, no GameScreen / SadConsole required.
public class PotionPickupTests
{
    private static GameLoopController NewLoop(out Player player, out List<FloorItem> floorItems)
    {
        var map = TestMaps.OpenFloor(20, 20);
        player = Player.Create(Reaver.Definition, new Position(5, 5));
        var enemies = new List<Enemy>();
        floorItems = new List<FloorItem>();
        var movement = new MovementController();
        var combat = new CombatController();
        return new GameLoopController(player, enemies, map, movement, combat, floorItems);
    }

    [Fact]
    public void Tick_PlayerOnFloorItem_PicksItUp()
    {
        var loop = NewLoop(out var player, out var floorItems);
        var item = FloorItem.Create(Potions.HealthPotion, player.Position, Potions.HealthGlyphColor);
        floorItems.Add(item);

        loop.Tick(deltaSec: 1f / 60f, cellAspect: 1.0f, frozen: false, lastPlayerTile: player.Position);

        Assert.Empty(floorItems);
        Assert.Equal(1, player.Inventory.HealthPotionCount);
    }

    [Fact]
    public void Tick_PlayerOnDifferentTile_LeavesItem()
    {
        var loop = NewLoop(out var player, out var floorItems);
        var item = FloorItem.Create(Potions.HealthPotion, new Position(10, 10), Potions.HealthGlyphColor);
        floorItems.Add(item);

        loop.Tick(1f / 60f, 1.0f, frozen: false, lastPlayerTile: player.Position);

        Assert.Single(floorItems);
        Assert.Equal(0, player.Inventory.HealthPotionCount);
    }

    [Fact]
    public void Tick_MultipleItemsOnSameTile_PicksAllUp()
    {
        var loop = NewLoop(out var player, out var floorItems);
        floorItems.Add(FloorItem.Create(Potions.HealthPotion, player.Position, Potions.HealthGlyphColor));
        floorItems.Add(FloorItem.Create(Potions.ResourcePotion, player.Position, Potions.ResourceGlyphColor));

        loop.Tick(1f / 60f, 1.0f, frozen: false, lastPlayerTile: player.Position);

        Assert.Empty(floorItems);
        Assert.Equal(1, player.Inventory.HealthPotionCount);
        Assert.Equal(1, player.Inventory.ResourcePotionCount);
    }
}
