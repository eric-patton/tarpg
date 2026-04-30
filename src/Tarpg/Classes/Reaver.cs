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
        // M2 / Q / W / E / R — kept in lockstep with GameLoopController slot indices.
        StartingSlotSkills = new string?[]
        {
            "heavy_strike",
            "cleave",
            "charge",
            "war_cry",
            "whirlwind",
        },
        BaseHealth = 65,
        BaseResource = 100,
    };
}
