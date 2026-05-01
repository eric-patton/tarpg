namespace Tarpg.Items;

// Magic-tier mid-floor weapon. Drops from regular enemies at a lower
// rate than RustyKnife (see LootDropper.RollEquipment) — the rarity
// gradient matches the tier system, so the natural progression is
// bare-handed -> RustyKnife -> IronBlade -> Wolfbreaker as the player
// pushes deeper into Wolfwood.
public static class IronBlade
{
    public static readonly ItemDefinition Definition = new()
    {
        Id = "iron_blade",
        Name = "Iron Blade",
        Tier = ItemTier.Magic,
        Slot = ItemSlot.Weapon,
        Glyph = ')',
        WeaponDamageBonus = 6,
        FlavorText = "the maker stamped a crow on the pommel",
    };
}
