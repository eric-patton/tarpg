using SadRogue.Primitives;
using Tarpg.Core;

namespace Tarpg.Classes;

public static class Speaker
{
    public static readonly WalkerClassDefinition Definition = new()
    {
        Id = "speaker",
        Name = "the Speaker",
        Tagline = "The dead remember everything. They will tell me what I need.",
        Description = "Echomancer / summoner. Pets, debuffs, ritual buffs. Echo gained per kill.",
        GlyphColor = new Color(80, 180, 220),
        Resource = ResourceType.Echo,
        StartingSkillIds = Array.Empty<string>(),
        BaseHealth = 45,
        BaseResource = 80,
    };
}
