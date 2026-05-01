using Tarpg.Items;

namespace Tarpg.Inventory;

// First-cut consumables-only inventory. Tracks per-potion counts; full
// 32-slot bag + equipment slots (GDD §6) lands later when equipment
// drops do. The Add / TryConsume API is intentionally narrow so the
// drop / pickup / drink pipeline can be wired without committing to
// the eventual bag shape.
public sealed class Inventory
{
    public int HealthPotionCount { get; private set; }
    public int ResourcePotionCount { get; private set; }

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
