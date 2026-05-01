using Tarpg.Items;

namespace Tarpg.Inventory;

// Player-side carry container. Two storage shapes:
//   1. Per-potion counters for the bottom-bar quick-drink consumables
//      (HP / Resource potions). These bypass the bag entirely so the
//      common "drink in a fight" path stays one keystroke (1 / 2).
//   2. A capped bag for equipment items the player picks up but hasn't
//      decided to equip yet. Auto-equip is intentionally NOT done —
//      ARPG item choice is build-dependent (a +5 weapon with the wrong
//      affixes can be worse than a +3 one with the right ones), so the
//      player explicitly equips via the inventory screen.
public sealed class Inventory
{
    // Hard cap on the equipment bag. GDD §6 spec is 32; chosen to feel
    // generous enough that mid-floor pickups don't force triage but
    // tight enough that the player eventually has to drop / sell things.
    public const int MaxBagSlots = 32;

    public int HealthPotionCount { get; private set; }
    public int ResourcePotionCount { get; private set; }

    // Backing list for the bag. Insertion order = display order; new
    // items go to the end (or fill an empty mid-list slot if one
    // exists from a manual remove). Caller iterates BagItems to render
    // the inventory grid.
    private readonly List<ItemDefinition?> _bag = new(MaxBagSlots);

    public IReadOnlyList<ItemDefinition?> BagItems => _bag;
    public int BagCount => _bag.Count(i => i is not null);
    public bool IsBagFull => BagCount >= MaxBagSlots;

    // Append item to the first empty slot (or grow the list if all
    // slots before the cap are full). Returns false if the bag is at
    // capacity — the caller (Player.PickUp) leaves the FloorItem on
    // the ground in that case.
    public bool TryAddToBag(ItemDefinition item)
    {
        if (item is null) return false;
        for (var i = 0; i < _bag.Count; i++)
        {
            if (_bag[i] is null) { _bag[i] = item; return true; }
        }
        if (_bag.Count >= MaxBagSlots) return false;
        _bag.Add(item);
        return true;
    }

    // Returns the item that was at `index` and clears the slot. Null if
    // index is out of range or already empty. Used by EquipFromBag and
    // future drop / sell flows.
    public ItemDefinition? RemoveFromBag(int index)
    {
        if (index < 0 || index >= _bag.Count) return null;
        var item = _bag[index];
        _bag[index] = null;
        return item;
    }

    // Direct slot write — used by Player.EquipFromBag's atomic swap to
    // place the previously-equipped item into the now-empty bag slot
    // without changing other items' positions. Caller guarantees the
    // index is within range and holds null at the time of call.
    public void BagSet(int index, ItemDefinition item)
    {
        if (index < 0 || index >= _bag.Count) return;
        _bag[index] = item;
    }

    public void Add(ItemDefinition item)
    {
        if (item.Id == Potions.HealthPotion.Id) HealthPotionCount++;
        else if (item.Id == Potions.ResourcePotion.Id) ResourcePotionCount++;
        // Non-consumable items silently ignored for now — equipment lands later.
    }

    public bool TryConsume(ItemDefinition item)
    {
        if (item.Id == Potions.HealthPotion.Id)
        {
            if (HealthPotionCount <= 0) return false;
            HealthPotionCount--;
            return true;
        }
        if (item.Id == Potions.ResourcePotion.Id)
        {
            if (ResourcePotionCount <= 0) return false;
            ResourcePotionCount--;
            return true;
        }
        return false;
    }

    // Used by the corpse-pickup pipeline: deposit potion counts back into
    // the inventory. Additive, so picking up a corpse stack on top of
    // potions you've grabbed since respawn doesn't overwrite them.
    public void Restore(int hpPotionCount, int resourcePotionCount)
    {
        HealthPotionCount += hpPotionCount;
        ResourcePotionCount += resourcePotionCount;
    }

    // Snapshot + zero — the death pipeline calls this to capture what
    // the player was carrying before re-spawning with an empty bag.
    public (int HpPotionCount, int ResourcePotionCount) DrainAll()
    {
        var snapshot = (HealthPotionCount, ResourcePotionCount);
        HealthPotionCount = 0;
        ResourcePotionCount = 0;
        return snapshot;
    }
}
