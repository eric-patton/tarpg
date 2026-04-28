using SadRogue.Primitives;
using Tarpg.Core;

namespace Tarpg.Classes;

public static class Cipher
{
    public static readonly WalkerClassDefinition Definition = new()
    {
        Id = "cipher",
        Name = "the Cipher",
        Tagline = "The world is a sentence. I edit.",
        Description = "Reality-magic caster. Glass-cannon AOE. Insight regenerates between casts.",
        GlyphColor = new Color(190, 130, 230),
        Resource = ResourceType.Insight,
        StartingSkillIds = Array.Empty<string>(),
        BaseHealth = 40,
        BaseResource = 120,
    };
}
