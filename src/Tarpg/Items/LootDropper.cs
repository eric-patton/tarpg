using SadRogue.Primitives;
using Tarpg.Entities;

namespace Tarpg.Items;

// Pure-logic loot roll. Returns the FloorItem that should drop (or null
// if the drop chance fails). Caller adds the result to the floor-items
// list and wires whatever visual / pickup plumbing it needs.
//
// Lifted out of GameScreen so the roll is unit-testable without spinning
// up SadConsole and so the simulation harness can call the same code
// path when we start testing loot drops in balance sweeps.
public static class LootDropper
{
    // Of the rolls that pass the outer dropChance gate, this fraction
    // resolves to a weapon (the rest stay potions). Tuned low for v0 —
    // potions are the common drop, weapons are the meaningful upgrade
    // events. With LootDropChance=0.08 and EquipmentShare=0.15,
    // ~1.2% of kills produce a weapon ≈ one weapon per 80 kills.
    private const float EquipmentShare = 0.15f;

    // Of the equipment rolls, this fraction picks the higher-tier
    // IronBlade; the rest get RustyKnife. Keeps Magic-tier weapons
    // rarer than Normal-tier as the tier system implies.
    private const float MagicWeaponShare = 0.25f;

    private static readonly Color WeaponColor = new(180, 180, 200);

    public static FloorItem? RollDrop(Enemy enemy, Random rng, float dropChance)
    {
        if (dropChance <= 0f) return null;
        if (rng.NextSingle() >= dropChance) return null;

        // Equipment vs consumable split. Weapons are the upgrade-event
        // drop; potions are the bread-and-butter sustain drop.
        if (rng.NextSingle() < EquipmentShare)
        {
            var pickMagic = rng.NextSingle() < MagicWeaponShare;
            var weapon = pickMagic ? IronBlade.Definition : RustyKnife.Definition;
            return FloorItem.Create(weapon, enemy.Position, WeaponColor);
        }

        // 50/50 between HP and Resource. Per-enemy / per-zone weighted
        // tables come once we have more item content to weight.
        var pickHealth = rng.Next(2) == 0;
        var def = pickHealth ? Potions.HealthPotion : Potions.ResourcePotion;
        var color = pickHealth ? Potions.HealthGlyphColor : Potions.ResourceGlyphColor;

        return FloorItem.Create(def, enemy.Position, color);
    }
}
