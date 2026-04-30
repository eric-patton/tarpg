using Tarpg.Core;
using Tarpg.Items;

namespace Tarpg.Entities;

// An item lying on the floor, waiting to be picked up. Subclass of Entity
// so it slots into the existing visual pipeline (zIndex render layer in
// GameScreen). Health is set to 1 and never decremented — FloorItems
// don't take damage.
//
// RenderLayer 20 sits between terrain (10) and creatures (50), so live
// enemies and the player draw on top of items but the items still cover
// the underlying floor glyph.
public sealed class FloorItem : Entity
{
    public required ItemDefinition Item { get; init; }

    public override int RenderLayer => 20;

    public static FloorItem Create(ItemDefinition item, Position tile, SadRogue.Primitives.Color color)
    {
        var fi = new FloorItem
        {
            Glyph = item.Glyph,
            Color = color,
            Name = item.Name,
            Item = item,
            MaxHealth = 1,
            Health = 1,
        };
        fi.SetTile(tile);
        return fi;
    }
}
