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
    public void PickUp_WeaponWhenBareHanded_Equips()
    {
        var player = NewPlayer();
        Assert.Null(player.EquippedWeapon);

        player.PickUp(RustyKnife.Definition);

        Assert.Same(RustyKnife.Definition, player.EquippedWeapon);
    }

    [Fact]
    public void PickUp_BetterWeapon_Upgrades()
    {
        // Auto-equip rule: strictly higher WeaponDamageBonus wins. A
        // RustyKnife (+3) gets replaced by an IronBlade (+6).
        var player = NewPlayer();
        player.PickUp(RustyKnife.Definition);
        player.PickUp(IronBlade.Definition);

        Assert.Same(IronBlade.Definition, player.EquippedWeapon);
    }

    [Fact]
    public void PickUp_WorseWeapon_DoesNotDowngrade()
    {
        // The "discard the lesser pickup" branch — equipping IronBlade
        // (+6) and then walking over a RustyKnife (+3) keeps the better
        // weapon. Otherwise loot ordering would matter and the player
        // would feel cheated when crossing a downgrade tile.
        var player = NewPlayer();
        player.PickUp(IronBlade.Definition);
        player.PickUp(RustyKnife.Definition);

        Assert.Same(IronBlade.Definition, player.EquippedWeapon);
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
        player.PickUp(IronBlade.Definition); // +6

        Assert.Equal(CombatController.BaseDamage + 6, player.WeaponDamage);
    }

    [Fact]
    public void TryAttack_WithEquippedWeapon_DealsBoostedDamage()
    {
        // End-to-end: the auto-attack pipeline must read Player.WeaponDamage
        // (= base + bonus) instead of the flat BaseDamage const. Wire up a
        // wolf, set the attack cooldown to 0 so the swing fires immediately,
        // confirm the wolf takes (10 + 12 = 22) dmg from a Wolfbreaker hit.
        var player = NewPlayer();
        player.PickUp(Wolfbreaker.Definition); // +12
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
