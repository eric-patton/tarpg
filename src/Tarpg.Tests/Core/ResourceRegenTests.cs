using Tarpg.Classes;
using Tarpg.Combat;
using Tarpg.Core;
using Tarpg.Enemies;
using Tarpg.Entities;
using Tarpg.Movement;
using Tarpg.Tests.Helpers;

namespace Tarpg.Tests.Core;

// Coverage for WalkerClassDefinition.PassiveResourceRegenPerSec — the
// property added to give Hunter a working Focus economy. Drives Tick
// directly; passive regen is independent of the out-of-combat HP gate
// so we don't need an "out of combat" setup either.
public class ResourceRegenTests
{
    private static GameLoopController NewLoop(WalkerClassDefinition cls, out Player player)
    {
        var map = TestMaps.OpenFloor(20, 20);
        player = Player.Create(cls, new Position(5, 5));
        var enemies = new List<Enemy>();
        var movement = new MovementController();
        var combat = new CombatController();
        return new GameLoopController(player, enemies, map, movement, combat);
    }

    [Fact]
    public void Tick_HunterPlayer_AccumulatesFocusOverTime()
    {
        var loop = NewLoop(Hunter.Definition, out var player);
        Assert.Equal(0, player.Resource);

        // 600 ticks at 1/60s = 10 sim-seconds. Hunter regens 3 Focus/sec
        // → expect ~30 Focus. Floor of accumulator is integer so allow a
        // 1-Focus tolerance band.
        for (var i = 0; i < 600; i++)
            loop.Tick(1f / 60f, 1.0f, frozen: false, lastPlayerTile: player.Position);

        Assert.InRange(player.Resource, 29, 31);
    }

    [Fact]
    public void Tick_ReaverPlayer_DoesNotPassivelyRegenRage()
    {
        // Reaver's PassiveResourceRegenPerSec is 0 — Rage is hit-driven
        // (see GrantResourceOnHit). Without combat Reaver should gain 0.
        var loop = NewLoop(Reaver.Definition, out var player);
        Assert.Equal(0, player.Resource);

        for (var i = 0; i < 600; i++)
            loop.Tick(1f / 60f, 1.0f, frozen: false, lastPlayerTile: player.Position);

        Assert.Equal(0, player.Resource);
    }

    [Fact]
    public void Tick_HunterAtMaxFocus_DoesNotOverflow()
    {
        var loop = NewLoop(Hunter.Definition, out var player);
        player.Resource = player.MaxResource;

        for (var i = 0; i < 600; i++)
            loop.Tick(1f / 60f, 1.0f, frozen: false, lastPlayerTile: player.Position);

        Assert.Equal(player.MaxResource, player.Resource);
    }
}
