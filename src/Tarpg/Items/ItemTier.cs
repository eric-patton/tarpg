using SadRogue.Primitives;

namespace Tarpg.Items;

public enum ItemTier
{
    Normal,
    Magic,
    Rare,
    Legendary,
    Set,
}

public static class ItemTierExtensions
{
    public static Color DisplayColor(this ItemTier tier) => tier switch
    {
        ItemTier.Normal    => Color.White,
        ItemTier.Magic     => new Color(80, 130, 255),
        ItemTier.Rare      => new Color(220, 200, 60),
        ItemTier.Legendary => new Color(220, 130, 40),
        ItemTier.Set       => new Color(70, 200, 90),
        _                  => Color.White,
    };

    // Magic items show stats on drop; Rare+ items must be read.
    public static bool RequiresIdentification(this ItemTier tier) => tier >= ItemTier.Rare;
}
