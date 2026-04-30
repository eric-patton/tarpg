using SadRogue.Primitives;
using Tarpg.Core;

namespace Tarpg.Classes;

public static class Hunter
{
    public static readonly WalkerClassDefinition Definition = new()
    {
        Id = "hunter",
        Name = "the Hunter",
        Tagline = "Pin the myth before it can speak.",
        Description = "Ranged tracker. Bows, traps, mobility, regenerating Focus.",
        GlyphColor = new Color(80, 200, 110),
        Resource = ResourceType.Focus,
        StartingSkillIds = new[] { "quick_shot" },
        // M2 / Q / W / E / R — kept in lockstep with GameLoopController slot indices.
        StartingSlotSkills = new string?[]
        {
            "quick_shot",
            "volley",
            "roll",
            "bandage",
            "rain_of_arrows",
        },
        BaseHealth = 50,
        BaseResource = 100,
    };
}
