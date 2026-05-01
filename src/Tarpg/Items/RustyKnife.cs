namespace Tarpg.Items;

// Normal-tier starter weapon. Auto-discovered by ContentInitializer
// reflection; LootDropper rolls for it on enemy kill so the player can
// move from bare-handed (10 dmg auto-attack) to lightly-armed (13 dmg)
// within the first floor or two of regular kills. Doesn't carry any
// affixes — Magic / Rare bases get those when the affix system wires up.
public static class RustyKnife
{
    public static readonly ItemDefinition Definition = new()
    {
        Id = "rusty_knife",
        Name = "Rusty Knife",
        Tier = ItemTier.Normal,
        Slot = ItemSlot.Weapon,
        Glyph = ')',
        WeaponDamageBonus = 3,
        FlavorText = "spotted with old red, edge gone soft",
    };
}
