using Tarpg.Classes;
using Tarpg.Core;

namespace Tarpg.Tests.Classes;

// Class kits live on WalkerClassDefinition.StartingSlotSkills as an
// IReadOnlyList<string?> indexed by GameLoopController.Slot* indices. Both
// GameScreen and TickRunner read this for slot wiring, so any class whose
// list has the wrong length / dangling skill ids will explode at runtime.
// These tests pin the shape and resolvability so a typo in a class def is
// caught at test time instead of mid-sim.
public class StartingSlotSkillsTests
{
    public StartingSlotSkillsTests()
    {
        // Idempotent — registries are populated on first call. Required so
        // Registries.Skills.Get can resolve the ids the class lists name.
        ContentInitializer.Initialize();
    }

    [Theory]
    [InlineData("reaver")]
    [InlineData("hunter")]
    public void StartingSlotSkills_HasCorrectLength(string classId)
    {
        var classDef = Registries.Classes.Get(classId);
        Assert.Equal(GameLoopController.SlotCount, classDef.StartingSlotSkills.Count);
    }

    [Theory]
    [InlineData("reaver")]
    [InlineData("hunter")]
    public void StartingSlotSkills_AllNonNullIdsResolveToRegisteredSkills(string classId)
    {
        var classDef = Registries.Classes.Get(classId);
        for (var i = 0; i < classDef.StartingSlotSkills.Count; i++)
        {
            var skillId = classDef.StartingSlotSkills[i];
            if (skillId is null) continue;
            // Throws KeyNotFoundException if the id was typoed or never registered.
            var skill = Registries.Skills.Get(skillId);
            Assert.NotNull(skill);
            Assert.Equal(skillId, skill.Id);
        }
    }

    [Fact]
    public void Reaver_FillsAllFiveSlots()
    {
        var reaver = Registries.Classes.Get("reaver");
        // Reaver's kit is fully wired — no nulls. Hunter same.
        for (var i = 0; i < reaver.StartingSlotSkills.Count; i++)
            Assert.NotNull(reaver.StartingSlotSkills[i]);
    }

    [Fact]
    public void Hunter_FillsAllFiveSlots()
    {
        var hunter = Registries.Classes.Get("hunter");
        for (var i = 0; i < hunter.StartingSlotSkills.Count; i++)
            Assert.NotNull(hunter.StartingSlotSkills[i]);
    }

    [Fact]
    public void Hunter_KitUsesFocusResource()
    {
        var hunter = Registries.Classes.Get("hunter");
        Assert.Equal(ResourceType.Focus, hunter.Resource);
        for (var i = 0; i < hunter.StartingSlotSkills.Count; i++)
        {
            var skillId = hunter.StartingSlotSkills[i];
            if (skillId is null) continue;
            var skill = Registries.Skills.Get(skillId);
            Assert.Equal(ResourceType.Focus, skill.Resource);
        }
    }
}
