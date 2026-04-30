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
}
