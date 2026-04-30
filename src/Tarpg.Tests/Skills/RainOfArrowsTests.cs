using Tarpg.Classes;
using Tarpg.Core;
using Tarpg.Enemies;
using Tarpg.Entities;
using Tarpg.Skills;
using Tarpg.Tests.Helpers;

namespace Tarpg.Tests.Skills;

public class RainOfArrowsTests
{
    [Fact]
    public void Execute_WideFootprint_HitsEveryoneInRadius2()
    {
        var map = TestMaps.OpenFloor(30, 30);
        var player = Player.Create(Reaver.Definition, new Position(10, 10));
        var center = new Position(15, 15);
        var inEdge = Enemy.Create(Wolf.Definition, new Position(17, 13)); // chebyshev 2
        var inCenter = Enemy.Create(Wolf.Definition, center);
        var outside = Enemy.Create(Wolf.Definition, new Position(18, 18)); // chebyshev 3

        var ctx = new SkillContext
        {
            Caster = player,
            Target = center,
            Map = map,
            Hostiles = new List<Entity> { inEdge, inCenter, outside },
        };

        var hpEdge = inEdge.Health;
        var hpCenter = inCenter.Health;
        var hpOut = outside.Health;
        RainOfArrows.Definition.Behavior.Execute(ctx);

        Assert.True(inEdge.Health < hpEdge);
        Assert.True(inCenter.Health < hpCenter);
        Assert.Equal(hpOut, outside.Health);
    }

    [Fact]
    public void Execute_IgnoresLineOfSight_HitsBehindWall()
    {
        // Wall between caster and target zone — RainOfArrows arcs over.
        var map = TestMaps.OpenFloorWithWalls(20, 20, new Position(7, 10));
        var player = Player.Create(Reaver.Definition, new Position(5, 10));
        var target = new Position(10, 10);
        var enemy = Enemy.Create(Wolf.Definition, target);

        var ctx = new SkillContext
        {
            Caster = player,
            Target = target,
            Map = map,
            Hostiles = new List<Entity> { enemy },
        };

        var initialHp = enemy.Health;
        RainOfArrows.Definition.Behavior.Execute(ctx);

        Assert.True(enemy.Health < initialHp);
    }

    [Fact]
    public void Execute_OutOfRange_NoDamage()
    {
        var map = TestMaps.OpenFloor(40, 20);
        var player = Player.Create(Reaver.Definition, new Position(5, 10));
        var farTarget = new Position(20, 10); // 15 tiles, > 8 max range
        var enemy = Enemy.Create(Wolf.Definition, farTarget);
        var ctx = new SkillContext
        {
            Caster = player,
            Target = farTarget,
            Map = map,
            Hostiles = new List<Entity> { enemy },
        };

        var initialHp = enemy.Health;
        RainOfArrows.Definition.Behavior.Execute(ctx);

        Assert.Equal(initialHp, enemy.Health);
    }
}
