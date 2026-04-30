using SadRogue.Primitives;
using Tarpg.Classes;
using Tarpg.Core;
using Tarpg.Enemies;
using Tarpg.Entities;
using Tarpg.Skills;
using Tarpg.Tests.Helpers;
using Tarpg.World;

namespace Tarpg.Tests.Skills;

public class QuickShotTests
{
    [Fact]
    public void Execute_TargetInRangeAndLos_DealsDamage()
    {
        var map = TestMaps.OpenFloor(20, 20);
        var player = Player.Create(Reaver.Definition, new Position(10, 10));
        var enemy = Enemy.Create(Wolf.Definition, new Position(13, 10));
        var ctx = new SkillContext
        {
            Caster = player,
            Target = enemy.Position,
            Map = map,
            Hostiles = new List<Entity> { enemy },
        };

        var initialHp = enemy.Health;
        QuickShot.Definition.Behavior.Execute(ctx);

        Assert.True(enemy.Health < initialHp);
    }

    [Fact]
    public void Execute_TargetOutOfRange_NoDamage()
    {
        var map = TestMaps.OpenFloor(40, 20);
        var player = Player.Create(Reaver.Definition, new Position(5, 10));
        var enemy = Enemy.Create(Wolf.Definition, new Position(20, 10)); // 15 tiles, > 6
        var ctx = new SkillContext
        {
            Caster = player,
            Target = enemy.Position,
            Map = map,
            Hostiles = new List<Entity> { enemy },
        };

        var initialHp = enemy.Health;
        QuickShot.Definition.Behavior.Execute(ctx);

        Assert.Equal(initialHp, enemy.Health);
    }

    [Fact]
    public void Execute_WallBlocksLos_NoDamage()
    {
        // Wall column between caster (x=5) and enemy (x=8). LOS from (5.5,10.5)
        // to (8.5,10.5) passes through x=7 (wall) → blocked.
        var map = TestMaps.OpenFloorWithWalls(20, 20, new Position(7, 10));
        var player = Player.Create(Reaver.Definition, new Position(5, 10));
        var enemy = Enemy.Create(Wolf.Definition, new Position(8, 10));
        var ctx = new SkillContext
        {
            Caster = player,
            Target = enemy.Position,
            Map = map,
            Hostiles = new List<Entity> { enemy },
        };

        var initialHp = enemy.Health;
        QuickShot.Definition.Behavior.Execute(ctx);

        Assert.Equal(initialHp, enemy.Health);
    }

    [Fact]
    public void Execute_NearMissClick_PicksClosestEnemyInRadius()
    {
        var map = TestMaps.OpenFloor(20, 20);
        var player = Player.Create(Reaver.Definition, new Position(10, 10));
        var nearby = Enemy.Create(Wolf.Definition, new Position(13, 10));
        // Click one tile off the wolf — should still hit it via TargetRadius=1.
        var clickedAt = new Position(13, 11);
        var ctx = new SkillContext
        {
            Caster = player,
            Target = clickedAt,
            Map = map,
            Hostiles = new List<Entity> { nearby },
        };

        var initialHp = nearby.Health;
        QuickShot.Definition.Behavior.Execute(ctx);

        Assert.True(nearby.Health < initialHp);
    }

    [Fact]
    public void Definition_HasExpectedShape()
    {
        Assert.Equal("quick_shot", QuickShot.Definition.Id);
        Assert.Equal(ResourceType.Focus, QuickShot.Definition.Resource);
        Assert.Equal(0, QuickShot.Definition.Cost);
    }
}
