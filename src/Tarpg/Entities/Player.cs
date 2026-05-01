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

    // Pickup funnel. Routes by item shape: consumables stack in the
    // dedicated potion counters; equipment goes to the bag (NOT auto-
    // equipped — ARPG item choice is build-dependent and the player
    // explicitly equips via the inventory screen). Returns true if
    // the item was actually claimed; false leaves the FloorItem on
    // the ground for the player to come back to (typical case: bag
    // full and the equipment can't fit yet).
    //
    // GameLoopController calls this from TryPickupFloorItems and only
    // removes the FloorItem from the floor when it returns true, so
    // a failed pickup doesn't silently consume the loot.
    public bool PickUp(ItemDefinition item)
    {
        if (item.Slot == ItemSlot.None)
        {
            // Consumables (potions today). Inventory.Add silently no-ops
            // on items it doesn't recognize, which keeps unsupported
            // consumables from breaking the pickup pipeline.
            Inventory.Add(item);
            return true;
        }
        // Equipment of any slot (Weapon today; Helm/Chest/Ring/etc.
        // will populate as their gameplay effects land) goes to the
        // bag. The TryEquipFromBag path is what moves things onto
        // the loadout; pickup is just storage.
        return Inventory.TryAddToBag(item);
    }

    // Equip the item at the given bag index. Atomic swap: any item
    // currently equipped in the destination slot moves to the freed
    // bag slot, so the operation always succeeds (bag count is
    // unchanged). Returns false only if the bag index is empty / out
    // of range — meaning "you tried to equip nothing."
    public bool EquipFromBag(int bagIndex)
    {
        var item = Inventory.BagItems.ElementAtOrDefault(bagIndex);
        if (item is null) return false;
        if (item.Slot == ItemSlot.None) return false;

        // Pull the bag item out, then write the previously-equipped
        // piece (if any) into the now-empty bag slot. Net bag count
        // is conserved; equipped slot reflects the new item.
        Inventory.RemoveFromBag(bagIndex);
        var previouslyEquipped = GetEquipped(item.Slot);
        SetEquipped(item.Slot, item);
        if (previouslyEquipped is not null)
            Inventory.BagSet(bagIndex, previouslyEquipped);
        return true;
    }

    // Unequip the item in the given slot back into the bag. Fails if
    // the bag is full (no room for the unequipped piece) — the player
    // has to drop / sell something first.
    public bool UnequipToBag(ItemSlot slot)
    {
        var item = GetEquipped(slot);
        if (item is null) return false;
        if (Inventory.IsBagFull) return false;

        SetEquipped(slot, null);
        Inventory.TryAddToBag(item);
        return true;
    }

    // Read / write helpers so EquipFromBag / UnequipToBag don't have
    // a switch tower. Adding new equipment slots = extend this switch
    // and add the corresponding field; everything else flows through.
    public ItemDefinition? GetEquipped(ItemSlot slot) => slot switch
    {
        ItemSlot.Weapon => EquippedWeapon,
        _ => null, // Other slots (Helm/Chest/Ring/...) not wired yet.
    };

    private void SetEquipped(ItemSlot slot, ItemDefinition? item)
    {
        switch (slot)
        {
            case ItemSlot.Weapon: EquippedWeapon = item; break;
            // Other slots no-op until their fields exist; PickUp accepts
            // them into the bag, but EquipFromBag will silently fail to
            // route them to a loadout slot. Tracked in STATUS open issues.
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
