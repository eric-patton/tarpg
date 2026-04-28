using SadRogue.Primitives;
using Tarpg.Core;

namespace Tarpg.Classes;

public static class Reaver
{
    public static readonly WalkerClassDefinition Definition = new()
    {
        Id = "reaver",
        Name = "the Reaver",
        Tagline = "If the Echoes can bleed, they can be killed.",
        Description = "Aggressive melee striker. Dual-wields, builds Rage from hits.",
        GlyphColor = new Color(220, 60, 60),
        Resource = ResourceType.Rage,
        StartingSkillIds = new[] { "cleave" },
        BaseHealth = 65,
        BaseResource = 100,
    };
}
