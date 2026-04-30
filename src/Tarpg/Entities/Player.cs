using Tarpg.Classes;
using Tarpg.Core;

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
