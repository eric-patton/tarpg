using Tarpg.Classes;
using Tarpg.Core;
using Tarpg.Items;

namespace Tarpg.Entities;

public sealed class Player : Entity
{
    public required WalkerClassDefinition WalkerClass { get; init; }

    public int Level { get; set; } = 1;
    public int Experience { get; set; }
    public int Resource { get; set; }
    public int MaxResource { get; set; } = 100;

    // Consumables (potions today; full bag + equipment slots later). Owned
    // by the player so it travels with the character through descent /
    // death without GameScreen having to plumb it through LoadFloor.
    public Tarpg.Inventory.Inventory Inventory { get; } = new();

    // Equipped weapon — null = bare-handed (auto-attack uses the base
    // damage only). v0 has only the Weapon slot wired; armor / ring /
    // amulet land when the equipment system grows. The drop pipeline
    // currently always overwrites with strictly-better weapons (see
    // PickUp), so this can never be downgraded by accidental pickup.
    public ItemDefinition? EquippedWeapon { get; set; }

    // Derived auto-attack damage. Combines the unarmed baseline (the
    // CombatController.BaseDamage const, threaded down so we don't
    // double-count) with the equipped weapon's flat bonus. Skills don't
    // read this yet — they keep their own flat per-skill damage until
    // the skill-damage-from-weapon refactor lands.
    public int WeaponDamage =>
        Tarpg.Combat.CombatController.BaseDamage + (EquippedWeapon?.WeaponDamageBonus ?? 0);

    // Pickup funnel. Routes by item shape: consumables stack in
    // Inventory; weapons hit the auto-equip path; everything else is
    // silently discarded for now (no bag yet). GameLoopController calls
    // this from TryPickupFloorItems instead of going straight to
    // Inventory.Add so equipment doesn't get swallowed by the consumable
    // gate.
    public void PickUp(ItemDefinition item)
    {
        if (item.Slot == ItemSlot.None)
        {
            // Consumables (potions today). Inventory.Add silently no-ops
            // on items it doesn't recognize, which keeps unsupported
            // consumables from breaking the pickup pipeline.
            Inventory.Add(item);
            return;
        }
        if (item.Slot == ItemSlot.Weapon)
        {
            TryEquipWeapon(item);
            return;
        }
        // Non-weapon equipment (armor, ring, ...) lands in this branch
        // when its slots get wired. For now silently discarded — pickup
        // visual still fires (the FloorItem is removed from the floor
        // by the loop), but the player's loadout doesn't change.
    }

    // Auto-equip rule: take the new weapon if we're bare-handed OR if
    // its WeaponDamageBonus is strictly higher than the currently-
    // equipped piece. Ties keep the current weapon (defensive — the
    // player would manually swap once a UI exists). The replaced
    // weapon is silently discarded today (no bag).
    private void TryEquipWeapon(ItemDefinition weapon)
    {
        if (EquippedWeapon is null
            || weapon.WeaponDamageBonus > EquippedWeapon.WeaponDamageBonus)
        {
            EquippedWeapon = weapon;
        }
    }

    public override int RenderLayer => 100;

    public static Player Create(WalkerClassDefinition cls, Position startPos)
    {
        var player = new Player
        {
            Glyph = '@',
            Color = cls.GlyphColor,
            Name = cls.Name,
            WalkerClass = cls,
            MaxHealth = cls.BaseHealth,
            Health = cls.BaseHealth,
            MaxResource = cls.BaseResource,
        };
        player.SetTile(startPos);
        return player;
    }
}
