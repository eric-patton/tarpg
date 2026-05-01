using SadRogue.Primitives;

namespace Tarpg.Enemies;

// First boss as a live combat entity. Mirrors the Bosses/WolfMother.cs
// BossDefinition stub (lore + signature loot id) but as an Enemy the
// combat system can actually fight — until BossDefinition grows runtime
// teeth, the live encounter ships through the Enemy pipeline.
//
// v0 behavior is just a high-HP, high-damage MeleeChargerAi — the
// signature pup-summon mechanic the GDD calls out is deferred until
// IEnemyAi gains a "spawn into the world" capability (today AIs only
// drive the actor they own, no way to inject new enemies mid-tick).
//
// RarityWeight = 0 keeps her out of the random-spawn pool — Wolf-Mother
// only appears when GameScreen explicitly spawns her at the floor's
// BossAnchor on a boss floor (see BspGenerator.BossFloors).
public static class WolfMother
{
    public static readonly EnemyDefinition Definition = new()
    {
        Id = "wolf_mother",
        Name = "the Wolf-Mother",
        Glyph = 'M',
        Color = new Color(220, 180, 110),

        // Tuned against player kit at F5 with floor scaling (HP ×1.6,
        // Dmg ×1.4): effective fight is ~200 HP / 14 dmg per 1.4s hit.
        // Reaver at ~25 DPS clears in ~8 sim-sec; takes 3-4 boss hits =
        // ~50-65 dmg if no defense. War Cry + potion buffer covers the
        // shortfall. First sim-tuned pass landed at 70% Reaver clear,
        // 90% Hunter clear at F5 — challenging but not lethal.
        BaseHealth = 140,
        BaseDamage = 10,
        MoveSpeed = 5.5f,        // slightly slower than a wolf — heavier
        AttackCooldown = 1.4f,   // hits hard but not often
        PackSize = 1,
        AiTag = "melee_charger",
        ZoneIds = new[] { "wolfwood" },
        RarityWeight = 0,         // boss-only, not in the weighted pool
        IsBoss = true,
        FlavorText = "her teeth were the first prayer ever spoken",
    };
}
