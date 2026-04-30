using System.Linq;
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
using Tarpg.Skills;
using Tarpg.UI.Effects;
using Tarpg.World;
using SadEntity = SadConsole.Entities.Entity;
using SadEntityManager = SadConsole.Entities.EntityManager;

namespace Tarpg.UI;

// Game screen orchestrator. Three-layer rendering:
//   - GameScreen itself: viewport-sized, owns input + acts as the parent
//     surface (its own cells are unused — children cover it).
//   - _worldConsole: world-sized backing surface (WorldWidth × WorldHeight)
//     with pixel-positioning enabled. Each frame UpdateCamera adjusts
//     `_worldConsole.Surface.View` to viewport+1 cells of the world AND
//     `_worldConsole.Position` by the sub-cell pixel remainder, so the
//     camera pans smoothly between cells instead of snapping a full cell
//     at every tile boundary.
//   - _hudConsole: viewport-aligned single row, no pixel shift, added last
//     so it draws on top of the world. The HUD stays glued to screen y=0
//     while the world slides underneath.
//
// On construction asks the configured zone for a generated floor, spawns
// the player at its entry tile and wolves at its enemy spawn points, then
// drives the per-tick movement / combat / FOV / render / camera loop.
// Walking onto a Threshold tile triggers descent to the next floor; dying
// triggers a same-floor regen at full HP (placeholder until corpse-run).
public sealed class GameScreen : SadConsole.Console
{
    // World is bigger than the viewport so the camera has somewhere to scroll.
    // BSP min-leaf gates (12x8) leave room for plenty of subdivisions at 160x60.
    private const int WorldWidth = 160;
    private const int WorldHeight = 60;

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

    // Left-click finds the nearest live enemy within this radius (Euclidean
    // tiles) of the clicked cell. ARPG attacks read as "swing in a direction"
    // rather than precise single-target picks, so a generous radius makes
    // clicking into a clump still feel responsive without forcing pixel-aim.
    private const float ClickTargetRadius = 1.5f;

    // Per-floor stat scaling. Floor N applies multipliers to BaseHealth and
    // BaseDamage of every spawned enemy: HP × (1 + HpScalePerFloor·(N−1)),
    // Dmg × (1 + DmgScalePerFloor·(N−1)). HP scales faster than damage so
    // deeper floors feel like grindier fights instead of one-shot brutality.
    private const float HpScalePerFloor = 0.15f;
    private const float DmgScalePerFloor = 0.10f;

    // Pack spawn fans out in chebyshev rings around the BSP-chosen center.
    // Capped at this radius so a horde with PackSize=10 doesn't sprawl across
    // a whole room when only the immediate neighborhood is walkable.
    private const int PackSpreadRadiusMax = 3;

    // Viewport rows reserved by HUD layers — the world camera centers the
    // player in the visible region between them so the player isn't covered
    // by an overlay in normal play.
    private const int TopHudHeight = 1;
    private const int BottomHudHeight = StatusPanel.PanelHeight;

    // Skills that translate the caster (Charge today; future blink / leap)
    // get their snap converted into a quick lerp at this speed instead of a
    // single-frame teleport. Player normal walk is 8 t/s; dash is ~4× that
    // so it reads as fast-but-visible rather than instantaneous.
    private const float DashTilesPerSec = 30f;

    // Not readonly — LoadFloor swaps in a fresh Map on descent / death.
    private Map _map;
    private readonly Player _player;
    private readonly MovementController _movement = new();
    private readonly CombatController _combat = new();
    private readonly GameLoopController _loop;
    private readonly SadEntityManager _entityManager;
    private readonly SadEntity _playerEntity;
    private readonly HitFeedback _hitFeedback;
    private readonly ClickIndicator _clickIndicator;
    private readonly ZoneDefinition _zone;

    // Skill slot index aliases — values come from GameLoopController so the
    // bottom-bar HUD, input handling, and the loop's cooldown array all agree.
    private const int SlotCount = GameLoopController.SlotCount;
    private const int SlotIndexM2 = GameLoopController.SlotIndexM2;
    private const int SlotIndexQ = GameLoopController.SlotIndexQ;
    private const int SlotIndexW = GameLoopController.SlotIndexW;
    private const int SlotIndexE = GameLoopController.SlotIndexE;
    private const int SlotIndexR = GameLoopController.SlotIndexR;

    // World layer (gets pixel-shifted each frame for sub-cell camera panning)
    // and HUD overlay layers (never shifted; pinned to viewport top / bottom).
    // _flashOverlay sits between world and HUD so screen-flash effects tint
    // the playfield without dimming the bars.
    private readonly SadConsole.Console _worldConsole;
    private readonly SadConsole.Console _flashOverlay;
    private readonly SadConsole.Console _hudConsole;
    private readonly SadConsole.Console _bottomHudConsole;
    private readonly StatusPanel _statusPanel;
    private readonly SkillVfx _skillVfx;

    private readonly int _viewportCellsW;
    private readonly int _viewportCellsH;

    private readonly List<Enemy> _enemies = new();
    private readonly Dictionary<Enemy, SadEntity> _enemyVisuals = new();

    private bool _shiftHeld;
    private bool _wasLeftButtonDown;
    private bool _wasRightButtonDown;
    private int _zoomIndex = DefaultZoomIndex;
    private int _lastScrollWheelValue;
    private int _currentFloor = 1;

    // Sentinel that doesn't match any in-bounds tile, so the first Update tick
    // forces a recompute even if the player hasn't moved yet.
    private Position _lastPlayerTile = new(-1, -1);

    // Last cell the cursor hovered over (in world tile coords). Cursor-aimed
    // skills (Heavy Strike at the cursor cell, Charge dashing toward it) read
    // this as their SkillContext.Target. Updated in ProcessMouse on any mouse
    // event; falls back to the player's tile when nothing has moved yet.
    private Position _lastCursorCell;

    // Active dash animation. _dashRemainingSec > 0 means the player is being
    // tweened from _dashStart toward _dashEnd; gates input + AI ticks via
    // the same `frozen` flag the hit-stop pause uses.
    private Vector2 _dashStart;
    private Vector2 _dashEnd;
    private float _dashTotalSec;
    private float _dashRemainingSec;

    // Single shared RNG drives floor seeds, weighted enemy picks, and
    // (later) loot drops. Program.cs seeds from Environment.TickCount;
    // tests / sim runners can pass an explicit seed for reproducibility.
    private readonly Random _rng;

    public GameScreen(int viewportWidth, int viewportHeight, Random? rng = null) : base(viewportWidth, viewportHeight)
    {
        _rng = rng ?? new Random(Environment.TickCount);

        UseMouse = true;
        UseKeyboard = true;
        IsFocused = true;
        FocusOnMouseClick = true;

        _viewportCellsW = viewportWidth;
        _viewportCellsH = viewportHeight;

        // World layer: oversized surface, pixel-positioning so we can pan
        // by sub-cell pixel amounts. UseMouse=false so input bubbles up to
        // GameScreen for routing. View width is viewport+1 to keep a buffer
        // cell on the right/bottom edges — partial cells render there as
        // the camera pans between integer cell positions.
        _worldConsole = new SadConsole.Console(WorldWidth, WorldHeight)
        {
            UsePixelPositioning = true,
            UseMouse = false,
        };
        _worldConsole.Surface.View = new Rectangle(0, 0, viewportWidth + 1, viewportHeight + 1);
        Children.Add(_worldConsole);

        // Flash overlay: full-viewport child between world and HUD so the
        // screen-flash VFX tints the playfield without affecting the bars.
        // Cells start transparent; SkillVfx paints / clears them on demand.
        _flashOverlay = new SadConsole.Console(viewportWidth, viewportHeight)
        {
            UseMouse = false,
        };
        for (var y = 0; y < viewportHeight; y++)
        for (var x = 0; x < viewportWidth; x++)
            _flashOverlay.Surface.SetGlyph(x, y, ' ', Color.Transparent, Color.Transparent);
        Children.Add(_flashOverlay);

        // Top HUD layer: viewport-aligned single row, no shifts. Added AFTER
        // the world (and flash overlay) so it draws on top of viewport row 0.
        _hudConsole = new SadConsole.Console(viewportWidth, TopHudHeight)
        {
            UseMouse = false,
        };
        Children.Add(_hudConsole);

        // Bottom HUD layer: status panel pinned to the bottom of the viewport.
        // Cell-positioned at (0, viewportHeight - BottomHudHeight) so it
        // covers the bottom rows independently of the world's pixel shift.
        _bottomHudConsole = new SadConsole.Console(viewportWidth, BottomHudHeight)
        {
            UseMouse = false,
            Position = new Point(0, viewportHeight - BottomHudHeight),
        };
        Children.Add(_bottomHudConsole);
        _statusPanel = new StatusPanel(_bottomHudConsole);

        _zone = Registries.Zones.Get("wolfwood");

        var seed = _rng.Next();
        System.Console.WriteLine($"[Tarpg] Generating {_zone.Name} F{_currentFloor} (seed {seed})");
        var floor = _zone.Generator.Generate(WorldWidth, WorldHeight, seed, _currentFloor);

        _map = floor.Map;
        _player = Player.Create(Reaver.Definition, floor.Entry);

        _loop = new GameLoopController(_player, _enemies, _map, _movement, _combat);
        _loop.SetSlotSkill(SlotIndexM2, Registries.Skills.Get("heavy_strike"));
        _loop.SetSlotSkill(SlotIndexQ,  Registries.Skills.Get("cleave"));
        _loop.SetSlotSkill(SlotIndexW,  Registries.Skills.Get("charge"));
        _loop.SetSlotSkill(SlotIndexE,  Registries.Skills.Get("war_cry"));
        _loop.SetSlotSkill(SlotIndexR,  Registries.Skills.Get("whirlwind"));

        _map.ComputeFovFor(_player.Position, GameLoopController.FovRadius);
        _lastPlayerTile = _player.Position;
        _lastCursorCell = _player.Position;

        // Effect subsystems must be constructed before the first UpdateCamera
        // call below, since UpdateCamera reads SkillVfx.GetShakeOffsetPx().
        // Entities live on the world layer so they pan with it.
        _entityManager = new SadEntityManager();
        _worldConsole.SadComponents.Add(_entityManager);

        _hitFeedback = new HitFeedback(_entityManager);
        _clickIndicator = new ClickIndicator(_entityManager);
        _skillVfx = new SkillVfx(_worldConsole.Surface, _flashOverlay.Surface);

        UpdateCamera();
        DrawMap();

        // Player events are hooked once for the lifetime of this GameScreen
        // (LoadFloor reuses the same Player instance, so subscriptions
        // survive descent + death respawn).
        _player.Damaged += OnEntityDamaged;
        _player.Died += OnEntityDied;

        _playerEntity = new SadEntity(_player.Color, Color.Black, _player.Glyph, zIndex: 100)
        {
            UsePixelPositioning = true,
        };
        _entityManager.Add(_playerEntity);

        foreach (var spawn in floor.EnemySpawnPoints)
            SpawnPack(PickEnemyForZone(), spawn);

        // Apply the configured base font size to all consoles, then size
        // the OS window to the viewport (NOT the world surface).
        FontSize = BaseFontSize;
        _worldConsole.FontSize = BaseFontSize;
        _flashOverlay.FontSize = BaseFontSize;
        _hudConsole.FontSize = BaseFontSize;
        _bottomHudConsole.FontSize = BaseFontSize;
        Game.Instance.ResizeWindow(_viewportCellsW, _viewportCellsH, BaseFontSize, true);

        SyncPlayerVisual();
        SyncEnemyVisuals();
        DrawHud();
    }

    // Place PackSize copies of `def` around `center`. Filling order: center
    // first, then chebyshev ring 1, then ring 2, up to PackSpreadRadiusMax.
    // Skips non-walkable / already-occupied tiles. If the spread can't fit
    // PackSize copies (tight room, neighbors blocked), we just spawn fewer —
    // BSP rooms are big enough that it's a non-issue at PackSize ≤ ~6.
    private void SpawnPack(EnemyDefinition def, Position center)
    {
        var occupied = new HashSet<Position> { _player.Position };
        foreach (var enemy in _enemies)
            occupied.Add(enemy.Position);

        var packSize = Math.Max(1, def.PackSize);
        var placed = 0;

        if (TryPlaceAt(def, center, occupied))
        {
            occupied.Add(center);
            placed++;
        }

        for (var radius = 1; placed < packSize && radius <= PackSpreadRadiusMax; radius++)
        {
            for (var dy = -radius; dy <= radius && placed < packSize; dy++)
            for (var dx = -radius; dx <= radius && placed < packSize; dx++)
            {
                // Walk only the ring perimeter — interior cells were filled
                // by earlier (smaller) radius iterations.
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                var p = new Position(center.X + dx, center.Y + dy);
                if (!TryPlaceAt(def, p, occupied)) continue;
                occupied.Add(p);
                placed++;
            }
        }
    }

    private bool TryPlaceAt(EnemyDefinition def, Position p, HashSet<Position> occupied)
    {
        if (!_map.IsWalkable(p)) return false;
        if (occupied.Contains(p)) return false;
        SpawnEnemy(def, p);
        return true;
    }

    private void SpawnEnemy(EnemyDefinition def, Position spawnTile)
    {
        var enemy = Enemy.Create(def, spawnTile);
        ApplyFloorScaling(enemy);
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

    // Slot activation: delegates to the loop controller for the
    // gate-and-execute, then converts a teleport snap (Charge) into a brief
    // animated lerp from PreCastPosition to PostCastPosition. The lerp lives
    // on the UI side because it's purely visual — sim runs leave the player
    // at the snapped position immediately.
    private void TryActivateSlot(int index)
    {
        var result = _loop.TryCastSkill(index, _lastCursorCell, _skillVfx);
        if (!result.Success || !result.Teleported) return;

        _movement.Stop();
        _combat.Clear();

        _player.ContinuousPosition = result.PreCastPosition;
        _dashStart = result.PreCastPosition;
        _dashEnd = result.PostCastPosition;
        var distance = Vector2.Distance(_dashStart, _dashEnd);
        _dashTotalSec = distance / DashTilesPerSec;
        _dashRemainingSec = _dashTotalSec;
    }

    // Bump HP and Damage on a freshly-created enemy based on _currentFloor.
    // Floor 1 applies no scaling; later floors multiply by the linear curves
    // configured at the top of the file. Min 1 on both so floor multipliers
    // never round a tiny base stat to zero.
    private void ApplyFloorScaling(Enemy enemy)
    {
        if (_currentFloor <= 1) return;
        var depth = _currentFloor - 1;
        var hpScale = 1f + HpScalePerFloor * depth;
        var dmgScale = 1f + DmgScalePerFloor * depth;
        enemy.MaxHealth = Math.Max(1, (int)MathF.Round(enemy.Definition.BaseHealth * hpScale));
        enemy.Health = enemy.MaxHealth;
        enemy.Damage = Math.Max(1, (int)MathF.Round(enemy.Definition.BaseDamage * dmgScale));
    }

    // Damage / death event handlers shared by both player and enemy entities.
    // Routes to HitFeedback for the visual + hit-stop response. The loop
    // controller subscribes to _player.Damaged separately for its regen
    // timer, so this handler only owns the UI-side response.
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

    // Walking onto a Threshold tile triggers descent: increment floor depth,
    // load a fresh layout at the new depth. HP carries over (descent doesn't heal).
    private void Descend()
    {
        _currentFloor++;
        LoadFloor(restoreFullHealth: false, reason: "descent");
    }

    // Real corpse-run / XP-loss death is deferred — for now, dying just
    // regenerates the current floor at full HP.
    private void RegenerateAfterDeath()
    {
        LoadFloor(restoreFullHealth: true, reason: "death");
    }

    // Tear down current enemies + transient effects, generate a fresh floor
    // at _currentFloor depth, reposition the player at the new entry tile.
    private void LoadFloor(bool restoreFullHealth, string reason)
    {
        _hitFeedback.Clear();
        _clickIndicator.Clear();
        _skillVfx.Clear();
        foreach (var visual in _enemyVisuals.Values)
            _entityManager.Remove(visual);
        _enemies.Clear();
        _enemyVisuals.Clear();

        _dashRemainingSec = 0f;

        var seed = _rng.Next();
        System.Console.WriteLine(
            $"[Tarpg] {reason}: loading {_zone.Name} F{_currentFloor} (seed {seed})");
        var floor = _zone.Generator.Generate(WorldWidth, WorldHeight, seed, _currentFloor);

        _map = floor.Map;
        _loop.Map = _map;
        _player.SetTile(floor.Entry);
        if (restoreFullHealth) _player.Health = _player.MaxHealth;

        // Loop owns combat / movement / cooldown / regen reset.
        // Rage is wiped on death (fresh start); descent preserves the saved
        // pool so a built-up resource carries to the next fight.
        _loop.OnFloorLoaded(restoreFullHealth);

        _map.ComputeFovFor(_player.Position, GameLoopController.FovRadius);
        _lastPlayerTile = _player.Position;
        _lastCursorCell = _player.Position;

        foreach (var spawn in floor.EnemySpawnPoints)
            SpawnPack(PickEnemyForZone(), spawn);

        UpdateCamera();
    }

    // Standard weighted-pick over the zone-eligible subset of the enemy
    // registry. Rebuilt per spawn so future per-floor weight curves (or
    // runtime tweaks) apply with no caching to invalidate.
    private EnemyDefinition PickEnemyForZone()
    {
        var totalWeight = 0;
        foreach (var def in Registries.Enemies.All)
        {
            if (!def.ZoneIds.Contains(_zone.Id)) continue;
            if (def.RarityWeight <= 0) continue;
            totalWeight += def.RarityWeight;
        }
        if (totalWeight == 0)
            throw new InvalidOperationException(
                $"No spawnable enemies registered for zone '{_zone.Id}'.");

        var pick = _rng.Next(totalWeight);
        foreach (var def in Registries.Enemies.All)
        {
            if (!def.ZoneIds.Contains(_zone.Id)) continue;
            if (def.RarityWeight <= 0) continue;
            pick -= def.RarityWeight;
            if (pick < 0) return def;
        }

        // Unreachable given totalWeight > 0; satisfies the compiler.
        throw new InvalidOperationException("Weighted enemy roll fell through.");
    }

    public override void Update(TimeSpan delta)
    {
        var deltaSec = (float)delta.TotalSeconds;
        // Aspect ratio derived from the *rendered* cell size (FontSize),
        // not the underlying font asset, so this stays correct under both
        // the native (8x16, aspect=2) and square (16x16, aspect=1) modes.
        var cellAspect = (float)FontSize.Y / FontSize.X;

        // Active dash: tween player position toward _dashEnd, snap exactly
        // on landing. Done before the frozen check so the lerp continues
        // even though gameplay is paused during the dash window.
        if (_dashRemainingSec > 0f)
        {
            _dashRemainingSec = MathF.Max(0f, _dashRemainingSec - deltaSec);
            var t = _dashTotalSec > 0f
                ? 1f - (_dashRemainingSec / _dashTotalSec)
                : 1f;
            t = Math.Clamp(t, 0f, 1f);
            _player.ContinuousPosition = Vector2.Lerp(_dashStart, _dashEnd, t);
            if (_dashRemainingSec <= 0f)
                _player.ContinuousPosition = _dashEnd;
        }

        // Hit-stop and dash both freeze the gameplay clock — same pattern,
        // different reason. Hit-stop sells the impact of a successful swing;
        // dash gates input until the lerp lands so the player can't queue a
        // walk-target halfway through the animation.
        var frozen = _hitFeedback.HitStopRemaining > 0f || _dashRemainingSec > 0f;

        var lastTile = _lastPlayerTile;
        _loop.Tick(deltaSec, cellAspect, frozen, lastTile);

        ReapDead();

        // React to the controller's tile-transition signals. Descent fully
        // reloads the floor (which resets _lastPlayerTile inside LoadFloor).
        // Otherwise FOV was already recomputed inside Tick at the new tile,
        // so we just track it here.
        if (_loop.SteppedOnThreshold)
            Descend();
        else if (_player.Position != lastTile)
            _lastPlayerTile = _player.Position;

        // Death regen: real corpse-run / XP-loss death is deferred per
        // docs/STATUS.md; today we just regenerate the floor at full HP.
        if (_loop.PlayerDied)
            RegenerateAfterDeath();

        // Effects always tick (so the hit-stop timer counts down even while
        // we're "frozen", and damage numbers / kill-burst particles keep
        // animating during the freeze for visual punch).
        _hitFeedback.Tick(deltaSec, FontSize);
        _clickIndicator.Tick(deltaSec, FontSize);
        _skillVfx.Tick(deltaSec);

        UpdateCamera();
        DrawMap();
        // SkillVfx renders AFTER DrawMap so its area-highlight tints land
        // on top of the freshly painted cells (otherwise DrawMap would
        // overwrite them). It also paints / clears the flash overlay.
        _skillVfx.Render();
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

        // Mapped skill slots. Pressed-event so each tap = one activation
        // attempt; holding the key shouldn't auto-spam. M2 (right mouse) is
        // handled in ProcessMouse on the press edge.
        if (keyboard.IsKeyPressed(Keys.Q)) TryActivateSlot(SlotIndexQ);
        if (keyboard.IsKeyPressed(Keys.W)) TryActivateSlot(SlotIndexW);
        if (keyboard.IsKeyPressed(Keys.E)) TryActivateSlot(SlotIndexE);
        if (keyboard.IsKeyPressed(Keys.R)) TryActivateSlot(SlotIndexR);

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

        // state.CellPosition is in GameScreen-cell space (viewport coords,
        // 0..viewportW × 0..viewportH). Translate to a world tile via the
        // world console's View origin. The sub-cell pixel shift is below 1
        // cell so this is off-by-one at the leftmost/topmost viewport edge
        // when the camera is mid-pan; not worth pixel-precise routing for v0.
        var view = _worldConsole.Surface.View;
        var worldCol = state.CellPosition.X + view.X;
        var worldRow = state.CellPosition.Y + view.Y;
        _lastCursorCell = new Position(worldCol, worldRow);

        // Detect left-button release as a one-shot edge so we can drop a
        // "you clicked here" pulse at the cursor cell. Tracked manually since
        // SadConsole's MouseScreenObjectState exposes "currently held" but
        // not the press↔release transition.
        var leftDown = state.Mouse.LeftButtonDown;
        if (_wasLeftButtonDown && !leftDown)
            _clickIndicator.Spawn(new Vector2(worldCol + 0.5f, worldRow + 0.5f));
        _wasLeftButtonDown = leftDown;

        // Right-button press edge → activate the M2 skill slot. Force-move
        // (the previous RMB-held behavior) is gone since the click-target
        // radius + far-floor clicks already cover walking past enemies.
        var rightDown = state.Mouse.RightButtonDown;
        if (rightDown && !_wasRightButtonDown)
            TryActivateSlot(SlotIndexM2);
        _wasRightButtonDown = rightDown;

        if (leftDown)
        {
            var cell = new Position(worldCol, worldRow);

            var clickedEnemy = FindLiveEnemyAt(cell);
            if (clickedEnemy is not null)
            {
                // Shift held = force-stand-attack. Otherwise normal attack-move.
                _combat.SetTarget(clickedEnemy, forceStand: _shiftHeld);
                return true;
            }

            if (_shiftHeld)
            {
                // Shift + LMB held over empty floor = "stand here, no walk".
                // Lets the user toggle out of a walk-drag by pressing shift
                // mid-gesture without having to release the mouse. Combat
                // targets are preserved so shift+click on an enemy followed
                // by dragging the cursor across floor doesn't cancel the
                // attack-stand on that enemy.
                if (_combat.Target is null)
                    _movement.Stop();
                return true;
            }

            _combat.Clear();
            var target = new Vector2(worldCol + 0.5f, worldRow + 0.5f);
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
        _worldConsole.FontSize = newSize;
        _flashOverlay.FontSize = newSize;
        _hudConsole.FontSize = newSize;
        _bottomHudConsole.FontSize = newSize;

        // Resize the OS window to match the new pixel dimensions of the
        // *viewport*, not the world surface.
        Game.Instance.ResizeWindow(_viewportCellsW, _viewportCellsH, newSize, true);
    }

    // Compute the camera origin in surface-pixel coords, split into integer
    // cell View origin + sub-cell pixel remainder. Apply the View to the
    // world console's surface and the remainder to its Position. This is what
    // gives us smooth cell-to-cell panning instead of single-cell pop-jumps.
    //
    // Y axis centers the player in the *visible* region (rows TopHudHeight
    // .. viewportH - BottomHudHeight - 1) instead of the full viewport, so
    // overlays don't cover the player. The Y clamp also tightens enough to
    // let the camera follow the player to the very bottom of the world
    // without dropping them under the bottom panel.
    private void UpdateCamera()
    {
        var fontW = FontSize.X;
        var fontH = FontSize.Y;

        var visibleHeight = _viewportCellsH - TopHudHeight - BottomHudHeight;
        var visibleCenterRow = TopHudHeight + visibleHeight / 2f;

        var halfViewPxX = _viewportCellsW * fontW / 2f;
        var camPxX = _player.ContinuousPosition.X * fontW - halfViewPxX;
        var camPxY = _player.ContinuousPosition.Y * fontH - visibleCenterRow * fontH;

        var maxCamPxX = MathF.Max(0, (_map.Width  - _viewportCellsW) * fontW);
        var maxCamPxY = MathF.Max(0, (_map.Height - (TopHudHeight + visibleHeight)) * fontH);
        camPxX = Math.Clamp(camPxX, 0, maxCamPxX);
        camPxY = Math.Clamp(camPxY, 0, maxCamPxY);

        var viewX = (int)MathF.Floor(camPxX / fontW);
        var viewY = (int)MathF.Floor(camPxY / fontH);
        var subPxX = camPxX - viewX * fontW;
        var subPxY = camPxY - viewY * fontH;

        var viewW = Math.Min(_viewportCellsW + 1, _map.Width  - viewX);
        var viewH = Math.Min(_viewportCellsH + 1, _map.Height - viewY);

        var newView = new Rectangle(viewX, viewY, viewW, viewH);
        if (_worldConsole.Surface.View != newView)
            _worldConsole.Surface.View = newView;

        var shake = _skillVfx.GetShakeOffsetPx();
        _worldConsole.Position = new Point(
            -(int)MathF.Round(subPxX) + shake.X,
            -(int)MathF.Round(subPxY) + shake.Y);
    }

    // Picks the live enemy nearest the clicked cell (by Euclidean distance to
    // its continuous position) within ClickTargetRadius. Returns null if no
    // enemy is in range — caller falls through to walk-to-here. This is what
    // gives clicks the "swing in a direction" feel instead of demanding a
    // pixel-perfect tile match against a moving target.
    private Enemy? FindLiveEnemyAt(Position cell)
    {
        var clickPos = new Vector2(cell.X + 0.5f, cell.Y + 0.5f);
        Enemy? best = null;
        var bestDistSq = ClickTargetRadius * ClickTargetRadius;
        foreach (var enemy in _enemies)
        {
            if (enemy.IsDead) continue;
            var d = Vector2.DistanceSquared(enemy.ContinuousPosition, clickPos);
            if (d <= bestDistSq) { best = enemy; bestDistSq = d; }
        }
        return best;
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
    // FontSize so visuals follow the zoom level. Entity positions are in
    // _worldConsole's surface-pixel space, so they pan with the world layer.
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
        // Top HUD: zone + floor on the left, target info on the right when
        // engaged. HP / Rage / skill state all live on the bottom panel now.
        var surface = _hudConsole.Surface;
        for (var x = 0; x < _viewportCellsW; x++)
            surface.SetGlyph(x, 0, ' ', Color.White, Color.Black);

        var leftText = $" {_zone.Name} F{_currentFloor}";
        surface.Print(0, 0, leftText, Color.White, Color.Black);

        if (_combat.Target is not null)
        {
            var rightText = $"> {_combat.Target.Name} {_combat.Target.Health}/{_combat.Target.MaxHealth} ";
            var rightStart = Math.Max(0, _viewportCellsW - rightText.Length);
            surface.Print(rightStart, 0, rightText, Color.White, Color.Black);
        }
        surface.IsDirty = true;

        // Bottom panel: HP orb, HP potion, 5 skill slots, resource potion,
        // resource orb. Slots and consumables that aren't filled in yet
        // render as dim placeholders so the player learns the bar layout
        // before content lands in those positions.
        var loopSlots = _loop.Slots;
        var loopCooldowns = _loop.Cooldowns;
        var slots = new[]
        {
            new SkillSlot("M2", loopSlots[SlotIndexM2], loopCooldowns[SlotIndexM2]),
            new SkillSlot("Q",  loopSlots[SlotIndexQ],  loopCooldowns[SlotIndexQ]),
            new SkillSlot("W",  loopSlots[SlotIndexW],  loopCooldowns[SlotIndexW]),
            new SkillSlot("E",  loopSlots[SlotIndexE],  loopCooldowns[SlotIndexE]),
            new SkillSlot("R",  loopSlots[SlotIndexR],  loopCooldowns[SlotIndexR]),
        };
        var hpPotion = new ConsumableSlot("1", '!', Count: 0);
        var resourcePotion = new ConsumableSlot("2", '!', Count: 0);
        _statusPanel.Render(_player, slots, hpPotion, resourcePotion);
    }

    private void DrawMap()
    {
        var fovOn = RenderSettings.EnableFov;
        var dim = RenderSettings.UnseenDimFactor;
        var view = _worldConsole.Surface.View;
        var surface = _worldConsole.Surface;

        // Paint only cells inside the View. Off-screen cells retain whatever
        // they last had until they scroll back in, which is fine since FOV /
        // explored state changes only matter when a cell is visible.
        for (var y = view.Y; y < view.Y + view.Height; y++)
        for (var x = view.X; x < view.X + view.Width; x++)
        {
            var tile = _map[x, y];
            var p = new Position(x, y);

            if (!fovOn || _map.IsInFov(p))
                surface.SetGlyph(x, y, tile.Type.Glyph, tile.Type.Foreground, tile.Type.Background);
            else if (_map.IsExploredAt(p))
                surface.SetGlyph(x, y, tile.Type.Glyph,
                    Dim(tile.Type.Foreground, dim),
                    Dim(tile.Type.Background, dim));
            else
                surface.SetGlyph(x, y, ' ', Color.Black, Color.Black);
        }
        surface.IsDirty = true;
    }

    private static Color Dim(Color c, float factor) =>
        new Color((byte)(c.R * factor), (byte)(c.G * factor), (byte)(c.B * factor), c.A);
}
