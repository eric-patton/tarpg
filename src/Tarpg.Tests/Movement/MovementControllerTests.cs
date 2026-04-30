using System.Numerics;
using SadRogue.Primitives;
using Tarpg.Classes;
using Tarpg.Core;
using Tarpg.Entities;
using Tarpg.Movement;
using Tarpg.Tests.Helpers;

namespace Tarpg.Tests.Movement;

public class MovementControllerTests
{
    [Fact]
    public void Tick_DriftsTowardTarget_AtConfiguredSpeed()
    {
        var map = TestMaps.OpenFloor(20, 20);
        var player = Player.Create(Reaver.Definition, new Position(5, 5));
        var movement = new MovementController();
        var target = new Vector2(10.5f, 5.5f);

        movement.RetargetTo(target, player.ContinuousPosition, map);

        var ticks = 0;
        var maxTicks = 200; // safety; expected ~13 at 0.05s steps
        while (movement.HasGoal && ticks < maxTicks)
        {
            movement.Tick(player, map, deltaSec: 0.05f, cellAspect: 1.0f);
            ticks++;
        }

        Assert.False(movement.HasGoal);
        // 5 tiles at 8 t/s = 0.625s ≈ 13 ticks of 0.05s. Allow a generous
        // upper bound since the final tick may overshoot the threshold.
        Assert.InRange(ticks, 11, 15);
    }

    [Fact]
    public void Tick_StopsWithinTargetArriveDistance()
    {
        var map = TestMaps.OpenFloor(20, 20);
        var player = Player.Create(Reaver.Definition, new Position(5, 5));
        var movement = new MovementController();
        var target = new Vector2(10.5f, 5.5f);

        movement.RetargetTo(target, player.ContinuousPosition, map);

        var ticks = 0;
        while (movement.HasGoal && ticks < 200)
        {
            movement.Tick(player, map, 0.05f, 1.0f);
            ticks++;
        }

        var dist = Vector2.Distance(player.ContinuousPosition, target);
        Assert.True(dist <= 0.1f, $"Expected to arrive near target; final distance {dist}");
    }

    [Fact]
    public void Tick_NoGoal_DoesNothing()
    {
        var map = TestMaps.OpenFloor(20, 20);
        var player = Player.Create(Reaver.Definition, new Position(5, 5));
        var movement = new MovementController();

        var startPos = player.ContinuousPosition;
        movement.Tick(player, map, 1.0f, 1.0f);

        Assert.Equal(startPos, player.ContinuousPosition);
    }

    [Fact]
    public void Stop_ClearsGoal()
    {
        var map = TestMaps.OpenFloor(20, 20);
        var player = Player.Create(Reaver.Definition, new Position(5, 5));
        var movement = new MovementController();

        movement.RetargetTo(new Vector2(10.5f, 5.5f), player.ContinuousPosition, map);
        Assert.True(movement.HasGoal);

        movement.Stop();
        Assert.False(movement.HasGoal);
    }

    [Fact]
    public void Tick_WallSlides_DoesNotCrossSolidTile()
    {
        // Wall column at x=8 from y=1..18 with no gap. RetargetTo (10.5, 5.5)
        // — A* finds no path (column blocks every row), so RetargetTo leaves
        // _finalTarget set without waypoints and movement drifts straight at
        // the cursor. Wall-slide collision should clamp X just before the
        // wall regardless of how many ticks we run.
        var walls = new List<Position>();
        for (var y = 1; y < 19; y++)
            walls.Add(new Position(8, y));
        var map = TestMaps.OpenFloorWithWalls(20, 20, walls.ToArray());
        var player = Player.Create(Reaver.Definition, new Position(5, 5));
        var movement = new MovementController();

        movement.RetargetTo(new Vector2(10.5f, 5.5f), player.ContinuousPosition, map);

        for (var i = 0; i < 200; i++)
            movement.Tick(player, map, 0.05f, 1.0f);

        // X should never have entered the wall tile (8 ≤ x < 9).
        Assert.True(player.ContinuousPosition.X < 8f,
            $"Expected x < 8, got {player.ContinuousPosition.X}");
    }
}
