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
    public static FloorItem? RollDrop(Enemy enemy, Random rng, float dropChance)
    {
        if (dropChance <= 0f) return null;
        if (rng.NextSingle() >= dropChance) return null;

        // 50/50 between HP and Resource. Per-enemy / per-zone weighted
        // tables come once we have more item content to weight.
        var pickHealth = rng.Next(2) == 0;
        var def = pickHealth ? Potions.HealthPotion : Potions.ResourcePotion;
        var color = pickHealth ? Potions.HealthGlyphColor : Potions.ResourceGlyphColor;

        return FloorItem.Create(def, enemy.Position, color);
    }
}
