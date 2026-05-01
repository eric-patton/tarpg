using Tarpg.Classes;
using Tarpg.Combat;
using Tarpg.Core;
using Tarpg.Enemies;
using Tarpg.Entities;
using Tarpg.Items;
using Tarpg.Movement;
using Tarpg.Tests.Helpers;

namespace Tarpg.Tests.Items;

// Coverage for the v0 equipment loop: PickUp routing (consumable vs
// weapon), auto-equip-if-better, weapon damage flowing into the
// auto-attack via Player.WeaponDamage. The full inventory UI / bag /
// skill-damage scaling lands in later iterations; these pin the
// current minimal contract.
public class EquipmentTests
{
    private static Player NewPlayer() =>
        Player.Create(Reaver.Definition, new Position(5, 5));

    [Fact]
    public void PickUp_Consumable_RoutesToInventory()
    {
        var player = NewPlayer();
        player.PickUp(Potions.HealthPotion);

        Assert.Equal(1, player.Inventory.HealthPotionCount);
        Assert.Null(player.EquippedWeapon);
    }

    [Fact]
    public void WeaponDamage_BareHanded_EqualsBaseDamage()
    {
        var player = NewPlayer();
        Assert.Equal(CombatController.BaseDamage, player.WeaponDamage);
    }

    [Fact]
    public void WeaponDamage_WithWeapon_AddsBonus()
    {
        var player = NewPlayer();
        player.PickUp(IronBlade.Definition); // +6, lands in bag
        player.EquipFromBag(0);

        Assert.Equal(CombatController.BaseDamage + 6, player.WeaponDamage);
    }

    [Fact]
    public void PickUp_Weapon_GoesToBagNotAutoEquip()
    {
        // Bag-first pickup contract: weapons must NOT auto-equip on
        // pickup any more (auto-equip is build-killing in ARPGs where
        // affixes / sockets matter beyond raw damage).
        var player = NewPlayer();

        var ok = player.PickUp(RustyKnife.Definition);

        Assert.True(ok);
        Assert.Null(player.EquippedWeapon);
        Assert.Equal(1, player.Inventory.BagCount);
        Assert.Same(RustyKnife.Definition, player.Inventory.BagItems[0]);
    }

    [Fact]
    public void PickUp_FullBag_ReturnsFalse()
    {
        // PickUp returns false when the bag can't fit the item — caller
        // (GameLoopController) leaves the FloorItem on the floor so the
        // player can come back to it after dropping something.
        var player = NewPlayer();
        for (var i = 0; i < Tarpg.Inventory.Inventory.MaxBagSlots; i++)
            player.PickUp(RustyKnife.Definition);
        Assert.True(player.Inventory.IsBagFull);

        var ok = player.PickUp(IronBlade.Definition);

        Assert.False(ok);
        Assert.Equal(Tarpg.Inventory.Inventory.MaxBagSlots, player.Inventory.BagCount);
    }

    [Fact]
    public void EquipFromBag_BareHanded_EquipsAndRemovesFromBag()
    {
        var player = NewPlayer();
        player.PickUp(IronBlade.Definition);
        Assert.Equal(1, player.Inventory.BagCount);

        var ok = player.EquipFromBag(0);

        Assert.True(ok);
        Assert.Same(IronBlade.Definition, player.EquippedWeapon);
        // The bag slot is now empty (atomic single-direction move when
        // there was nothing to swap back in).
        Assert.Equal(0, player.Inventory.BagCount);
    }

    [Fact]
    public void EquipFromBag_WithCurrentWeapon_AtomicallySwaps()
    {
        // Atomic-swap contract: bag count stays the same; the new
        // weapon equips, the old one falls into the now-empty bag slot.
        var player = NewPlayer();
        player.PickUp(RustyKnife.Definition);
        player.EquipFromBag(0); // RustyKnife equipped, bag empty.
        player.PickUp(IronBlade.Definition);
        Assert.Equal(1, player.Inventory.BagCount);

        var ok = player.EquipFromBag(0);

        Assert.True(ok);
        Assert.Same(IronBlade.Definition, player.EquippedWeapon);
        // Bag count is conserved; the previously-equipped RustyKnife
        // should be the item in the bag now.
        Assert.Equal(1, player.Inventory.BagCount);
        Assert.Same(RustyKnife.Definition, player.Inventory.BagItems[0]);
    }

    [Fact]
    public void UnequipToBag_FullBag_ReturnsFalse()
    {
        // Player has IronBlade equipped + a fully-stocked bag of 32
        // weapons. Unequip should fail because there's no room for
        // the IronBlade to land in.
        var player = NewPlayer();
        player.PickUp(IronBlade.Definition);
        player.EquipFromBag(0);
        for (var i = 0; i < Tarpg.Inventory.Inventory.MaxBagSlots; i++)
            player.PickUp(RustyKnife.Definition);
        Assert.True(player.Inventory.IsBagFull);

        var ok = player.UnequipToBag(ItemSlot.Weapon);

        Assert.False(ok);
        Assert.Same(IronBlade.Definition, player.EquippedWeapon);
    }

    [Fact]
    public void UnequipToBag_RoomInBag_MovesEquippedToBag()
    {
        var player = NewPlayer();
        player.PickUp(IronBlade.Definition);
        player.EquipFromBag(0);
        Assert.NotNull(player.EquippedWeapon);
        Assert.Equal(0, player.Inventory.BagCount);

        var ok = player.UnequipToBag(ItemSlot.Weapon);

        Assert.True(ok);
        Assert.Null(player.EquippedWeapon);
        Assert.Equal(1, player.Inventory.BagCount);
        Assert.Same(IronBlade.Definition, player.Inventory.BagItems[0]);
    }

    [Fact]
    public void TryAttack_WithEquippedWeapon_DealsBoostedDamage()
    {
        // End-to-end: the auto-attack pipeline must read Player.WeaponDamage
        // (= base + bonus) instead of the flat BaseDamage const. Wire up a
        // wolf, set the attack cooldown to 0 so the swing fires immediately,
        // confirm the wolf takes (10 + 12 = 22) dmg from a Wolfbreaker hit.
        // Pickup goes to the bag now (no auto-equip), so we explicitly
        // EquipFromBag before swinging.
        var player = NewPlayer();
        player.PickUp(Wolfbreaker.Definition); // +12, lands in bag
        player.EquipFromBag(0);
        var enemy = Enemy.Create(Wolf.Definition, new Position(5, 6)); // adjacent

        var combat = new CombatController();
        combat.SetTarget(enemy);
        var initialHp = enemy.Health;

        // First TryAttack swings (cooldown is 0 on first call).
        var hit = combat.TryAttack(player, deltaSec: 0f);

        Assert.True(hit);
        Assert.Equal(initialHp - (CombatController.BaseDamage + 12), enemy.Health);
    }
}
