using Tarpg.Classes;
using Tarpg.Core;
using Tarpg.Enemies;
using Tarpg.Entities;
using Tarpg.Skills;
using Tarpg.Tests.Helpers;

namespace Tarpg.Tests.Skills;

public class VolleyTests
{
    [Fact]
    public void Execute_ChunkOfEnemies_HitsEveryoneInRadius()
    {
        var map = TestMaps.OpenFloor(20, 20);
        var player = Player.Create(Reaver.Definition, new Position(10, 10));
        var center = new Position(13, 10);
        var inA = Enemy.Create(Wolf.Definition, new Position(13, 10));
        var inB = Enemy.Create(Wolf.Definition, new Position(14, 11));
        var outside = Enemy.Create(Wolf.Definition, new Position(15, 12)); // chebyshev 2 from center

        var ctx = new SkillContext
        {
            Caster = player,
            Target = center,
            Map = map,
            Hostiles = new List<Entity> { inA, inB, outside },
        };

        var hpA = inA.Health; var hpB = inB.Health; var hpOut = outside.Health;
        Volley.Definition.Behavior.Execute(ctx);

        Assert.True(inA.Health < hpA);
        Assert.True(inB.Health < hpB);
        Assert.Equal(hpOut, outside.Health);
    }

    [Fact]
    public void Execute_OutOfRange_NoDamage()
    {
        var map = TestMaps.OpenFloor(40, 20);
        var player = Player.Create(Reaver.Definition, new Position(5, 10));
        var farTarget = new Position(20, 10); // 15 tiles, > 6 max range
        var enemy = Enemy.Create(Wolf.Definition, farTarget);
        var ctx = new SkillContext
        {
            Caster = player,
            Target = farTarget,
            Map = map,
            Hostiles = new List<Entity> { enemy },
        };

        var initialHp = enemy.Health;
        Volley.Definition.Behavior.Execute(ctx);

        Assert.Equal(initialHp, enemy.Health);
    }

    [Fact]
    public void Execute_WallBlocksCasterToCursorLos_NoDamage()
    {
        var map = TestMaps.OpenFloorWithWalls(20, 20, new Position(7, 10));
        var player = Player.Create(Reaver.Definition, new Position(5, 10));
        var target = new Position(9, 10);
        var enemy = Enemy.Create(Wolf.Definition, target);
        var ctx = new SkillContext
        {
            Caster = player,
            Target = target,
            Map = map,
            Hostiles = new List<Entity> { enemy },
        };

        var initialHp = enemy.Health;
        Volley.Definition.Behavior.Execute(ctx);

        Assert.Equal(initialHp, enemy.Health);
    }
}
