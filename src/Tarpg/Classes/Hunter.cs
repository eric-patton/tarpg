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
        StartingSkillIds = Array.Empty<string>(),
        BaseHealth = 50,
        BaseResource = 100,
    };
}
