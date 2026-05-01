using SadRogue.Primitives;
using Tarpg.Core;

namespace Tarpg.Entities;

// The player's dead self, left on the floor where they re-spawned (today
// always F1 entry-adjacent — see GameScreen.RegenerateAfterDeath). Holds
// a snapshot of inventory contents at time of death; walking onto the
// corpse tile restores them via GameLoopController.TryPickupCorpses.
//
// Separate from FloorItem because the semantics differ: a FloorItem is
// "a single ItemDefinition you Add() to inventory," whereas a corpse is
// a multi-payload bundle that gets DRAINED into inventory. Future
// equipment drops will become additional fields on this class without
// touching the FloorItem pipeline.
//
// RenderLayer 25 sits between FloorItems (20) and creatures (50) so the
// corpse glyph draws on top of any potions that landed on the same tile,
// but live actors still draw on top of corpses (so the player walking
// over the corpse hides it visually until they step off).
public sealed class Corpse : Entity
{
    public int HpPotionCount { get; init; }
    public int ResourcePotionCount { get; init; }

    public override int RenderLayer => 25;

    public static Corpse CreateAt(Position tile, int hpPotionCount, int resourcePotionCount)
    {
        var c = new Corpse
        {
            Glyph = '%',
            Color = new Color(180, 50, 50),
            Name = "Corpse",
            HpPotionCount = hpPotionCount,
            ResourcePotionCount = resourcePotionCount,
            MaxHealth = 1,
            Health = 1,
        };
        c.SetTile(tile);
        return c;
    }
}
