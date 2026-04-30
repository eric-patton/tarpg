using Tarpg.Classes;
using Tarpg.Core;
using Tarpg.Entities;
using Tarpg.Skills;
using Tarpg.Tests.Helpers;

namespace Tarpg.Tests.Skills;

public class BandageTests
{
    [Fact]
    public void Execute_BelowMaxHp_RestoresHp()
    {
        var map = TestMaps.OpenFloor(20, 20);
        var player = Player.Create(Reaver.Definition, new Position(10, 10));
        player.Health = player.MaxHealth - 30;
        var beforeHp = player.Health;

        var ctx = new SkillContext
        {
            Caster = player,
            Target = player.Position,
            Map = map,
            Hostiles = new List<Entity>(),
        };
        Bandage.Definition.Behavior.Execute(ctx);

        Assert.True(player.Health > beforeHp);
        Assert.True(player.Health <= player.MaxHealth);
    }

    [Fact]
    public void Execute_AtMaxHp_DoesNotOverheal()
    {
        var map = TestMaps.OpenFloor(20, 20);
        var player = Player.Create(Reaver.Definition, new Position(10, 10));
        player.Health = player.MaxHealth;

        var ctx = new SkillContext
        {
            Caster = player,
            Target = player.Position,
            Map = map,
            Hostiles = new List<Entity>(),
        };
        Bandage.Definition.Behavior.Execute(ctx);

        Assert.Equal(player.MaxHealth, player.Health);
    }

    [Fact]
    public void Definition_HasExpectedShape()
    {
        Assert.Equal("bandage", Bandage.Definition.Id);
        Assert.Equal(ResourceType.Focus, Bandage.Definition.Resource);
        Assert.Equal(25, Bandage.Definition.Cost);
    }
}
