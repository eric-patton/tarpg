using Tarpg.Classes;
using Tarpg.Core;
using Tarpg.Entities;
using Tarpg.Skills;
using Tarpg.Tests.Helpers;

namespace Tarpg.Tests.Skills;

public class RollTests
{
    [Fact]
    public void Execute_OnOpenFloor_MovesAwayFromCursorByMaxDistance()
    {
        var map = TestMaps.OpenFloor(20, 20);
        var player = Player.Create(Reaver.Definition, new Position(10, 10));
        var ctx = new SkillContext
        {
            Caster = player,
            Target = new Position(8, 10), // cursor west — roll east
            Map = map,
            Hostiles = new List<Entity>(),
        };

        Roll.Definition.Behavior.Execute(ctx);

        // Roll's MaxDistanceTiles = 4 — east-direction roll lands at (14, 10).
        Assert.Equal(new Position(14, 10), player.Position);
    }

    [Fact]
    public void Execute_WallStopsRoll_LandsBeforeWall()
    {
        // Wall at x=12 — east-direction roll from x=10 should park at x=11.
        var map = TestMaps.OpenFloorWithWalls(20, 20, new Position(12, 10));
        var player = Player.Create(Reaver.Definition, new Position(10, 10));
        var ctx = new SkillContext
        {
            Caster = player,
            Target = new Position(8, 10),
            Map = map,
            Hostiles = new List<Entity>(),
        };

        Roll.Definition.Behavior.Execute(ctx);

        Assert.Equal(11, player.Position.X);
        Assert.Equal(10, player.Position.Y);
    }

    [Fact]
    public void Execute_CursorOnCaster_StillRollsArbitraryDirection()
    {
        var map = TestMaps.OpenFloor(20, 20);
        var player = Player.Create(Reaver.Definition, new Position(10, 10));
        var ctx = new SkillContext
        {
            Caster = player,
            Target = player.Position,
            Map = map,
            Hostiles = new List<Entity>(),
        };

        Roll.Definition.Behavior.Execute(ctx);

        // Falls back to east. Should land at (14, 10).
        Assert.NotEqual(new Position(10, 10), player.Position);
    }

    [Fact]
    public void Execute_DiagonalCursor_RollsDiagonallyAway()
    {
        var map = TestMaps.OpenFloor(20, 20);
        var player = Player.Create(Reaver.Definition, new Position(10, 10));
        var ctx = new SkillContext
        {
            Caster = player,
            Target = new Position(8, 8), // cursor NW — roll SE
            Map = map,
            Hostiles = new List<Entity>(),
        };

        Roll.Definition.Behavior.Execute(ctx);

        Assert.Equal(new Position(14, 14), player.Position);
    }
}
