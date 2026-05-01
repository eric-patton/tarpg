using Tarpg.Classes;
using Tarpg.Combat;
using Tarpg.Core;
using Tarpg.Enemies;
using Tarpg.Entities;
using Tarpg.Movement;
using Tarpg.Tests.Helpers;
using Tarpg.World;

namespace Tarpg.Tests.Core;

// The boss-death-reveals-threshold contract: when a flagged-IsBoss enemy
// dies, GameLoopController converts every BossAnchor tile on the map to
// Threshold so the player can step on it for descent. Tested headlessly
// since the conversion lives entirely in the loop (no UI / VFX coupling).
public class BossDeathTileTests
{
    private static GameLoopController NewLoop(out Player player, out List<Enemy> enemies, out Map map)
    {
        map = TestMaps.OpenFloor(20, 20);
        // Mark a single tile as the boss anchor.
        map.SetTile(new Position(10, 10), TileTypes.BossAnchor);
        player = Player.Create(Reaver.Definition, new Position(5, 5));
        enemies = new List<Enemy>();
        var movement = new MovementController();
        var combat = new CombatController();
        return new GameLoopController(player, enemies, map, movement, combat);
    }

    [Fact]
    public void Tick_BossAlive_BossAnchorTileUnchanged()
    {
        var loop = NewLoop(out var player, out var enemies, out var map);
        // Wolf-Mother is the only IsBoss=true enemy in the registry today.
        enemies.Add(Enemy.Create(WolfMother.Definition, new Position(10, 10)));

        loop.Tick(1f / 60f, 1.0f, frozen: false, lastPlayerTile: player.Position);

        Assert.Equal(TileTypes.BossAnchor.Id, map[new Position(10, 10)].Type.Id);
    }

    [Fact]
    public void Tick_BossDies_BossAnchorTileBecomesThreshold()
    {
        var loop = NewLoop(out var player, out var enemies, out var map);
        var boss = Enemy.Create(WolfMother.Definition, new Position(10, 10));
        enemies.Add(boss);

        // Kill the boss directly; loop should detect the dead boss and
        // convert the BossAnchor tile on the next tick.
        boss.TakeDamage(boss.MaxHealth);
        loop.Tick(1f / 60f, 1.0f, frozen: false, lastPlayerTile: player.Position);

        Assert.Equal(TileTypes.Threshold.Id, map[new Position(10, 10)].Type.Id);
    }

    [Fact]
    public void Tick_NonBossEnemyDies_BossAnchorTileUnchanged()
    {
        // Regular wolf death must not convert the boss arena — only
        // IsBoss-flagged enemies trigger the unlock. Otherwise a single
        // wolf kill on a boss floor would reveal the descent before the
        // arena is actually cleared.
        var loop = NewLoop(out var player, out var enemies, out var map);
        var wolf = Enemy.Create(Wolf.Definition, new Position(10, 10));
        enemies.Add(wolf);

        wolf.TakeDamage(wolf.MaxHealth);
        loop.Tick(1f / 60f, 1.0f, frozen: false, lastPlayerTile: player.Position);

        Assert.Equal(TileTypes.BossAnchor.Id, map[new Position(10, 10)].Type.Id);
    }
}
