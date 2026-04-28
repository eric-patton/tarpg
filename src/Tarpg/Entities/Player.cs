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
