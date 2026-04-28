using System.Numerics;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;
using Tarpg.Classes;
using Tarpg.Combat;
using Tarpg.Core;
using Tarpg.Enemies;
using Tarpg.Entities;
using Tarpg.Movement;
using Tarpg.UI.Effects;
using Tarpg.World;
using SadEntity = SadConsole.Entities.Entity;
using SadEntityManager = SadConsole.Entities.EntityManager;

namespace Tarpg.UI;

// Game screen orchestrator. On construction, asks the configured zone
// (currently "wolfwood") for a freshly generated floor, spawns the player
// at its entry tile and wolves at its enemy spawn points, then drives the
// per-tick movement / combat / FOV / render loop. Tiles redraw each tick
// with FOV-aware visibility (full / dim / hidden). Player and enemies are
// SadConsole Entities with pixel positioning, driven by MovementController
// (continuous) and CombatController (auto-attack melee).
public sealed class GameScreen : SadConsole.Console
{
    // Mode toggle lives in Tarpg.Core.RenderSettings so Program.cs (font
    // loader) and this file stay in sync. See RenderSettings for what each
    // mode does.
    private static readonly Point NativeFontSize = new(8, 16);
    private static readonly Point SquareFontSize = new(12, 12);
    private static Point BaseFontSize =>
        RenderSettings.UseSquareCells ? SquareFontSize : NativeFontSize;

    // Zoom multipliers applied against BaseFontSize. Half-steps are listed
    // for granularity but only integer multiples render perfectly crisp.
    private static readonly float[] ZoomLevels =
    {
        0.5f, 1.0f, 1.5f, 2.0f, 2.5f, 3.0f,
    };
    private const int DefaultZoomIndex = 1; // 1.0x

    // Player FOV radius in tiles. Will be replaced by ModifierContext.FieldOfViewRadius
    // (currently 10.0f) once modifier-context plumbing reaches the renderer.
    private const int FovRadius = 10;

    // Not readonly — RegenerateFloor swaps in a fresh Map on player death.
    private Map _map;
    private readonly Player _player;
    private readonly MovementController _movement = new();
    private readonly CombatController _combat = new();
    private readonly SadEntityManager _entityManager;
    private readonly SadEntity _playerEntity;
    private readonly HitFeedback _hitFeedback;

    private readonly List<Enemy> _enemies = new();
    private readonly Dictionary<Enemy, SadEntity> _enemyVisuals = new();

    private bool _shiftHeld;
    private int _zoomIndex = DefaultZoomIndex;
    private int _lastScrollWheelValue;

    // Sentinel that doesn't match any in-bounds tile, so the first Update tick
    // forces a recompute even if the player hasn't moved yet.
    private Position _lastPlayerTile = new(-1, -1);

    public GameScreen(int width, int height) : base(width, height)
    {
        UseMouse = true;
        UseKeyboard = true;
        IsFocused = true;
        FocusOnMouseClick = true;

        var zone = Registries.Zones.Get("wolfwood");
        var seed = Environment.TickCount;
        System.Console.WriteLine($"[Tarpg] Generating {zone.Name} (seed {seed})");
        var floor = zone.Generator.Generate(width, height, seed);

        _map = floor.Map;
        _player = Player.Create(Reaver.Definition, floor.Entry);

        // Seed FOV before the first paint so the initial frame already reflects
        // visibility instead of flashing as fully revealed.
        _map.ComputeFovFor(_player.Position, FovRadius);
        _lastPlayerTile = _player.Position;

        DrawMap();

        _entityManager = new SadEntityManager();
        SadComponents.Add(_entityManager);

        _hitFeedback = new HitFeedback(_entityManager);

        // Player events are hooked once for the lifetime of this GameScreen
        // (RegenerateFloor reuses the same Player instance, so subscriptions
        // survive death + respawn).
        _player.Damaged += OnEntityDamaged;
        _player.Died += OnEntityDied;

        _playerEntity = new SadEntity(_player.Color, Color.Black, _player.Glyph, zIndex: 100)
        {
            UsePixelPositioning = true,
        };
        _entityManager.Add(_playerEntity);

        // Hardcoded to Wolf for v0; later: roll over Registries.Enemies.All
        // filtered by ZoneIds.Contains(zone.Id) with RarityWeight.
        foreach (var spawn in floor.EnemySpawnPoints)
            SpawnEnemy(Wolf.Definition, spawn);

        // Apply the configured base font size (and resize window once to match).
        // No-op if UseSquareCells is false and we're already at native 8x16.
        FontSize = BaseFontSize;
        Game.Instance.ResizeWindow(Surface.Width, Surface.Height, BaseFontSize, true);

        SyncPlayerVisual();
        SyncEnemyVisuals();
        DrawHud();
    }

    private void SpawnEnemy(EnemyDefinition def, Position spawnTile)
    {
        var enemy = Enemy.Create(def, spawnTile);
        enemy.Damaged += OnEntityDamaged;
        enemy.Died += OnEntityDied;
        var visual = new SadEntity(enemy.Color, Color.Black, enemy.Glyph, zIndex: enemy.RenderLayer)
        {
            UsePixelPositioning = true,
        };
        _entityManager.Add(visual);
        _enemies.Add(enemy);
        _enemyVisuals[enemy] = visual;
    }

    // Damage / death event handlers shared by both player and enemy entities.
    // Routes to HitFeedback for the visual + hit-stop response.
    private void OnEntityDamaged(Entity entity, int amount)
    {
        // Red on player so the "you got hit" cue is unmistakable; warm yellow
        // on enemies for "you landed a swing" feedback.
        var color = entity is Player
            ? new Color(255, 90, 90)
            : new Color(255, 230, 100);
        _hitFeedback.OnDamaged(entity, amount, color);
    }

    private void OnEntityDied(Entity entity)
    {
        // Player death is handled by the regen flow — skip the kill burst
        // since the floor swaps before the spray would render.
        if (entity is Enemy)
            _hitFeedback.OnDied(entity);
    }

    // Tear down current enemies, generate a fresh Wolfwood floor, reposition
    // the player at full HP. Used on player death until real corpse-run /
    // XP-loss death lands.
    private void RegenerateFloor()
    {
        _hitFeedback.Clear();
        foreach (var visual in _enemyVisuals.Values)
            _entityManager.Remove(visual);
        _enemies.Clear();
        _enemyVisuals.Clear();
        _combat.Clear();
        _movement.Stop();

        var zone = Registries.Zones.Get("wolfwood");
        var seed = Environment.TickCount;
        System.Console.WriteLine($"[Tarpg] Player died. Regenerating {zone.Name} (seed {seed})");
        var floor = zone.Generator.Generate(_map.Width, _map.Height, seed);

        _map = floor.Map;
        _player.SetTile(floor.Entry);
        _player.Health = _player.MaxHealth;

        _map.ComputeFovFor(_player.Position, FovRadius);
        _lastPlayerTile = _player.Position;

        foreach (var spawn in floor.EnemySpawnPoints)
            SpawnEnemy(Wolf.Definition, spawn);
    }

    public override void Update(TimeSpan delta)
    {
        var deltaSec = (float)delta.TotalSeconds;
        // Aspect ratio derived from the *rendered* cell size (FontSize),
        // not the underlying font asset, so this stays correct under both
        // the native (8x16, aspect=2) and square (16x16, aspect=1) modes.
        var cellAspect = (float)FontSize.Y / FontSize.X;

        // Hit-stop: when a swing connects, both player and enemy movement +
        // attack ticks freeze for ~80ms so the impact reads. Cooldowns hold
        // their value across the freeze; they resume from where they were.
        var frozen = _hitFeedback.HitStopRemaining > 0f;

        if (!frozen)
        {
            if (_combat.IsTargetAlive)
            {
                var target = _combat.Target!;
                var distance = Vector2.Distance(_player.ContinuousPosition, target.ContinuousPosition);

                if (!_combat.ForceStand && distance > CombatController.MeleeRange)
                {
                    // Approach the target. Re-target every frame so the player
                    // tracks the enemy if it moves.
                    _movement.RetargetTo(target.ContinuousPosition, _player.ContinuousPosition, _map);
                    _movement.Tick(_player, _map, deltaSec, cellAspect);
                }
                else
                {
                    // Either in range, or shift+click force-stand. Stop moving and try to swing.
                    _movement.Stop();
                    _combat.TryAttack(_player, deltaSec);
                }
            }
            else
            {
                // No combat target — pure movement input.
                _movement.Tick(_player, _map, deltaSec, cellAspect);
            }
        }

        ReapDead();

        // FOV only needs to recompute when the player crosses a tile boundary.
        // Position is the floor of ContinuousPosition, so equality checks here
        // catch every tile transition without per-frame overhead. We do this
        // BEFORE AI tick so each enemy queries the freshest FOV state.
        if (_player.Position != _lastPlayerTile)
        {
            _map.ComputeFovFor(_player.Position, FovRadius);
            _lastPlayerTile = _player.Position;
        }

        // Each live enemy's AI decides chase / attack / idle for this frame.
        // Skipped during hit-stop so attacks and chase pause on impact.
        if (!frozen)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.IsDead) continue;
                enemy.Ai.Tick(enemy, _player, _map, deltaSec, cellAspect);
            }
        }

        // If the AIs collectively killed the player this frame, regenerate
        // the floor and respawn at full HP. Real corpse-run / XP-loss death
        // is deferred per docs/STATUS.md.
        if (_player.IsDead)
            RegenerateFloor();

        // Effects always tick (so the hit-stop timer counts down even while
        // we're "frozen", and damage numbers / kill-burst particles keep
        // animating during the freeze for visual punch).
        _hitFeedback.Tick(deltaSec, FontSize);

        DrawMap();
        SyncPlayerVisual();
        SyncEnemyVisuals();
        DrawHud();

        base.Update(delta);
    }

    public override bool ProcessKeyboard(SadConsole.Input.Keyboard keyboard)
    {
        _shiftHeld = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

        // Zoom keys: + / - (also numpad add/subtract). Pressed event so each
        // tap moves one step rather than auto-repeating.
        if (keyboard.IsKeyPressed(Keys.OemPlus) || keyboard.IsKeyPressed(Keys.Add))
            ChangeZoom(+1);
        if (keyboard.IsKeyPressed(Keys.OemMinus) || keyboard.IsKeyPressed(Keys.Subtract))
            ChangeZoom(-1);

        return base.ProcessKeyboard(keyboard);
    }

    public override bool ProcessMouse(MouseScreenObjectState state)
    {
        if (!state.IsOnScreenObject) return base.ProcessMouse(state);

        // Mouse wheel zoom.
        var scrollDelta = state.Mouse.ScrollWheelValue - _lastScrollWheelValue;
        _lastScrollWheelValue = state.Mouse.ScrollWheelValue;
        if (scrollDelta != 0)
            ChangeZoom(Math.Sign(scrollDelta));

        // Right-button-held = force move (drift toward cursor, ignore enemies).
        if (state.Mouse.RightButtonDown)
        {
            _combat.Clear();
            var cell = state.CellPosition;
            var target = new Vector2(cell.X + 0.5f, cell.Y + 0.5f);
            _movement.RetargetTo(target, _player.ContinuousPosition, _map);
            return true;
        }

        if (state.Mouse.LeftButtonDown)
        {
            var cell = new Position(state.CellPosition.X, state.CellPosition.Y);

            var clickedEnemy = FindLiveEnemyAt(cell);
            if (clickedEnemy is not null)
            {
                // Shift held = force-stand-attack. Otherwise normal attack-move.
                _combat.SetTarget(clickedEnemy, forceStand: _shiftHeld);
                return true;
            }

            // Empty floor click. If shift is held, ignore (force-stand has no
            // floor target). Otherwise normal walk.
            if (_shiftHeld) return true;

            _combat.Clear();
            var target = new Vector2(cell.X + 0.5f, cell.Y + 0.5f);
            _movement.RetargetTo(target, _player.ContinuousPosition, _map);
            return true;
        }

        return base.ProcessMouse(state);
    }

    private void ChangeZoom(int direction)
    {
        var newIndex = Math.Clamp(_zoomIndex + direction, 0, ZoomLevels.Length - 1);
        if (newIndex == _zoomIndex) return;

        _zoomIndex = newIndex;
        var multiplier = ZoomLevels[_zoomIndex];
        var newSize = new Point(
            (int)MathF.Round(BaseFontSize.X * multiplier),
            (int)MathF.Round(BaseFontSize.Y * multiplier));
        FontSize = newSize;

        // Resize the OS window to match the new pixel dimensions of the
        // surface. Without this, larger cells overflow / smaller cells
        // letterbox the existing window.
        Game.Instance.ResizeWindow(Surface.Width, Surface.Height, newSize, true);
    }

    private Enemy? FindLiveEnemyAt(Position cell)
    {
        foreach (var enemy in _enemies)
        {
            if (enemy.IsDead) continue;
            if (enemy.Position == cell) return enemy;
        }
        return null;
    }

    private void ReapDead()
    {
        for (var i = _enemies.Count - 1; i >= 0; i--)
        {
            var enemy = _enemies[i];
            if (!enemy.IsDead) continue;
            if (_enemyVisuals.Remove(enemy, out var visual))
                _entityManager.Remove(visual);
            _enemies.RemoveAt(i);
        }
    }

    private void SyncPlayerVisual() => SyncVisual(_player, _playerEntity);

    private void SyncEnemyVisuals()
    {
        foreach (var (enemy, visual) in _enemyVisuals)
            SyncVisual(enemy, visual);
    }

    // ContinuousPosition is in tile-space, where (cx + 0.5, cy + 0.5) means the
    // visual center of cell (cx, cy). Convert to top-left pixel coords for the
    // entity (which expects the glyph's top-left corner) using the *current*
    // FontSize so visuals follow the zoom level.
    private void SyncVisual(Entity entity, SadEntity visual)
    {
        var pos = entity.ContinuousPosition;
        var pxX = (pos.X - 0.5f) * FontSize.X;
        var pxY = (pos.Y - 0.5f) * FontSize.Y;
        visual.Position = new Point((int)MathF.Round(pxX), (int)MathF.Round(pxY));

        // While flashing from a recent hit, override the glyph foreground.
        // Reset to the entity's base color the moment the flash expires —
        // which is just "every other frame", since flash state is checked
        // per-tick.
        var fg = _hitFeedback.IsFlashing(entity) ? _hitFeedback.FlashTint : entity.Color;
        if (visual.AppearanceSingle is { } appearance && appearance.Appearance.Foreground != fg)
        {
            appearance.Appearance.Foreground = fg;
            visual.IsDirty = true;
        }

        // Hide entity sprites whose tile isn't currently visible. Player tile
        // is always in FOV by construction, so this collapses to true for the
        // player visual and only affects enemies.
        visual.IsVisible = !RenderSettings.EnableFov || _map.IsInFov(entity.Position);
    }

    private void DrawHud()
    {
        var width = Surface.Width;
        for (var x = 0; x < width; x++)
            Surface.SetGlyph(x, 0, ' ', Color.White, Color.Black);

        var line = $"  {_player.Name}  HP {_player.Health}/{_player.MaxHealth}";
        if (_combat.Target is not null)
            line += $"   |   target: {_combat.Target.Name}  HP {_combat.Target.Health}/{_combat.Target.MaxHealth}";
        line += $"   |   zoom {ZoomLevels[_zoomIndex]:0.#}x";

        Surface.Print(0, 0, line, Color.White, Color.Black);
        Surface.IsDirty = true;
    }

    private void DrawMap()
    {
        var fovOn = RenderSettings.EnableFov;
        var dim = RenderSettings.UnseenDimFactor;

        for (var y = 0; y < _map.Height; y++)
        for (var x = 0; x < _map.Width; x++)
        {
            var tile = _map[x, y];
            var p = new Position(x, y);

            if (!fovOn || _map.IsInFov(p))
                Surface.SetGlyph(x, y, tile.Type.Glyph, tile.Type.Foreground, tile.Type.Background);
            else if (_map.IsExploredAt(p))
                Surface.SetGlyph(x, y, tile.Type.Glyph,
                    Dim(tile.Type.Foreground, dim),
                    Dim(tile.Type.Background, dim));
            else
                Surface.SetGlyph(x, y, ' ', Color.Black, Color.Black);
        }
        Surface.IsDirty = true;
    }

    private static Color Dim(Color c, float factor) =>
        new Color((byte)(c.R * factor), (byte)(c.G * factor), (byte)(c.B * factor), c.A);
}
