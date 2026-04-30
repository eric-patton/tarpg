using Tarpg.Core;
using Tarpg.Enemies;
using Tarpg.Entities;
using Tarpg.Items;

namespace Tarpg.Tests.Items;

public class LootDropperTests
{
    [Fact]
    public void RollDrop_DropChanceZero_ReturnsNull()
    {
        var enemy = Enemy.Create(Wolf.Definition, new Position(5, 5));
        var rng = new Random(42);

        var item = LootDropper.RollDrop(enemy, rng, dropChance: 0f);

        Assert.Null(item);
    }

    [Fact]
    public void RollDrop_ForcedDrop_ReturnsItemAtEnemyPosition()
    {
        var enemy = Enemy.Create(Wolf.Definition, new Position(7, 12));
        var rng = new Random(42);

        var item = LootDropper.RollDrop(enemy, rng, dropChance: 1f);

        Assert.NotNull(item);
        Assert.Equal(enemy.Position, item!.Position);
        // The item is one of the two known consumables.
        Assert.True(
            item.Item.Id == Potions.HealthPotion.Id ||
            item.Item.Id == Potions.ResourcePotion.Id);
    }

    [Fact]
    public void RollDrop_OverManyRolls_ProducesBothPotionTypes()
    {
        // RNG branching is stable so we just need enough rolls to be very
        // unlikely to fail the assertion if both code paths are wired.
        var rng = new Random(7);
        var sawHealth = false;
        var sawResource = false;

        for (var i = 0; i < 100 && !(sawHealth && sawResource); i++)
        {
            var enemy = Enemy.Create(Wolf.Definition, new Position(5, 5));
            var item = LootDropper.RollDrop(enemy, rng, dropChance: 1f);
            Assert.NotNull(item);
            if (item!.Item.Id == Potions.HealthPotion.Id) sawHealth = true;
            if (item.Item.Id == Potions.ResourcePotion.Id) sawResource = true;
        }

        Assert.True(sawHealth, "Never rolled a health potion across 100 rolls.");
        Assert.True(sawResource, "Never rolled a resource potion across 100 rolls.");
    }
}
