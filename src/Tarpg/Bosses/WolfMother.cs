using SadRogue.Primitives;

namespace Tarpg.Bosses;

public static class WolfMother
{
    public static readonly BossDefinition Definition = new()
    {
        Id = "wolf_mother",
        Name = "the Wolf-Mother",
        ZoneId = "wolfwood",
        Glyph = 'W',
        Color = new Color(220, 180, 110),
        BaseHealth = 320,
        Tagline = "her teeth were the first prayer ever spoken",
        SignatureLootId = "wolfbreaker",
        EchoPactCompanionId = "wolf_mothers_hound",
        LoreSummary = "She was first the hunger, then the hunt, then the lullaby that follows the kill.",
    };
}
