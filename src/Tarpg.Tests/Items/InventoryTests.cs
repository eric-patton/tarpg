using Tarpg.Core;
using Tarpg.Items;

namespace Tarpg.Tests.Items;

public class InventoryTests
{
    [Fact]
    public void Add_HealthPotion_IncrementsCount()
    {
        var inv = new Tarpg.Inventory.Inventory();
        Assert.Equal(0, inv.HealthPotionCount);

        inv.Add(Potions.HealthPotion);
        inv.Add(Potions.HealthPotion);

        Assert.Equal(2, inv.HealthPotionCount);
        Assert.Equal(0, inv.ResourcePotionCount);
    }

    [Fact]
    public void Add_ResourcePotion_IncrementsCount()
    {
        var inv = new Tarpg.Inventory.Inventory();
        inv.Add(Potions.ResourcePotion);

        Assert.Equal(1, inv.ResourcePotionCount);
        Assert.Equal(0, inv.HealthPotionCount);
    }

    [Fact]
    public void TryConsume_WithCount_DecrementsAndReturnsTrue()
    {
        var inv = new Tarpg.Inventory.Inventory();
        inv.Add(Potions.HealthPotion);

        var ok = inv.TryConsume(Potions.HealthPotion);

        Assert.True(ok);
        Assert.Equal(0, inv.HealthPotionCount);
    }

    [Fact]
    public void TryConsume_EmptyStack_ReturnsFalseAndDoesNotMutate()
    {
        var inv = new Tarpg.Inventory.Inventory();

        var ok = inv.TryConsume(Potions.HealthPotion);

        Assert.False(ok);
        Assert.Equal(0, inv.HealthPotionCount);
    }

    [Fact]
    public void DrainAll_ZeroesCountsAndReturnsSnapshot()
    {
        var inv = new Tarpg.Inventory.Inventory();
        inv.Add(Potions.HealthPotion);
        inv.Add(Potions.HealthPotion);
        inv.Add(Potions.ResourcePotion);

        var (hp, res) = inv.DrainAll();

        Assert.Equal(2, hp);
        Assert.Equal(1, res);
        Assert.Equal(0, inv.HealthPotionCount);
        Assert.Equal(0, inv.ResourcePotionCount);
    }

    [Fact]
    public void Restore_IsAdditive_DoesNotOverwriteExistingPotions()
    {
        // Regression for the corpse-pickup scenario: player respawns,
        // grabs a fresh HP potion off the floor before reaching their
        // corpse. Restore must add to (not replace) the current counts
        // so the new pickup isn't clobbered when the corpse drains.
        var inv = new Tarpg.Inventory.Inventory();
        inv.Add(Potions.HealthPotion); // picked up post-respawn
        inv.Restore(hpPotionCount: 2, resourcePotionCount: 1);

        Assert.Equal(3, inv.HealthPotionCount);
        Assert.Equal(1, inv.ResourcePotionCount);
    }
}

// Coverage for the GameLoopController potion-drink API. Ported here
// instead of in a Core/ test file because they exercise inventory +
// loop wiring together, and the existing Items/ tests already share a
// fixture pattern for player + loop construction.
public class PotionDrinkTests
{
    private static GameLoopController NewLoop(out Tarpg.Entities.Player player)
    {
        var map = Tarpg.Tests.Helpers.TestMaps.OpenFloor(20, 20);
        player = Tarpg.Entities.Player.Create(Tarpg.Classes.Reaver.Definition, new Position(5, 5));
        var enemies = new List<Tarpg.Entities.Enemy>();
        var movement = new Tarpg.Movement.MovementController();
        var combat = new Tarpg.Combat.CombatController();
        return new GameLoopController(player, enemies, map, movement, combat);
    }

    [Fact]
    public void TryDrinkHealthPotion_WithStockAndDamage_HealsAndConsumes()
    {
        var loop = NewLoop(out var player);
        player.Inventory.Add(Potions.HealthPotion);
        player.Health = 10;

        var ok = loop.TryDrinkHealthPotion();

        Assert.True(ok);
        Assert.Equal(0, player.Inventory.HealthPotionCount);
        Assert.Equal(10 + Potions.HealthPotionHealAmount, player.Health);
    }

    [Fact]
    public void TryDrinkHealthPotion_NoStock_ReturnsFalse()
    {
        var loop = NewLoop(out var player);
        var ok = loop.TryDrinkHealthPotion();
        Assert.False(ok);
    }

    [Fact]
    public void TryDrinkHealthPotion_OnCooldown_ReturnsFalse()
    {
        var loop = NewLoop(out var player);
        player.Inventory.Add(Potions.HealthPotion);
        player.Inventory.Add(Potions.HealthPotion);
        player.Health = 10;

        Assert.True(loop.TryDrinkHealthPotion());
        // Second drink in the same tick window should fail the gate.
        Assert.False(loop.TryDrinkHealthPotion());
        Assert.Equal(1, player.Inventory.HealthPotionCount);
    }

    [Fact]
    public void Tick_DecaysDrinkCooldown_AllowsNextDrink()
    {
        var loop = NewLoop(out var player);
        player.Inventory.Add(Potions.HealthPotion);
        player.Inventory.Add(Potions.HealthPotion);
        player.Health = 1;

        loop.TryDrinkHealthPotion();
        // Tick past the drink cooldown.
        var ticksToClear = (int)MathF.Ceiling(Potions.DrinkCooldownSec * 60f) + 2;
        for (var i = 0; i < ticksToClear; i++)
            loop.Tick(1f / 60f, 1.0f, frozen: false, lastPlayerTile: player.Position);

        Assert.True(loop.TryDrinkHealthPotion());
    }
}
