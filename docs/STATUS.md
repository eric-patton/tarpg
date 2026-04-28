# TARPG — Master Status

> **Updated**: 2026-04-28
> **Where we are**: continuous-movement Diablo-style ARPG scaffold; one scaffold map with FOV / fog of war; one enemy type (wolf); one playable class (Reaver); auto-attack melee combat working end-to-end; mouse modes; zoom; bundled square-cell font.

This is the running roadmap. Every meaningful change updates the **Recently completed** and **Up next** sections. Before starting work, read this top-to-bottom.

---

## Orientation for a fresh session

If you're picking this up cold, read in this order:

1. **`docs/design/game-design.md`** — the master GDD (15 sections, ~480 lines). Read sections 1–2 (Vision, Setting), section 4 (Classes), and sections 13–15 (Scope, Deferred, Implementation order) at minimum.
2. **`docs/research/arpg-design-research.md`** — foundational research on ARPG design (332 lines). Skim if needed.
3. **This file** — what's been built, what's queued, key decisions.
4. **`src/Tarpg/Program.cs`** — entry point, very short.
5. **`src/Tarpg/UI/GameScreen.cs`** — the orchestrator. Most current work touches this.

**Project root**: `C:\repos\personal\tarpg`

---

## Run / build

| Command | What it does |
|---|---|
| `run` | Launches the game window (alias for `dotnet run --project src/Tarpg`) |
| `test` | Runs xUnit tests (no tests written yet — `dotnet test`) |
| `dotnet build tarpg.sln` | Verifies clean compile |

Both `.bat` files include `cd /d "%~dp0"` so they work from any subdirectory.

**Stack**: .NET 8 LTS (pinned via `global.json`), SadConsole 10.9.0, MonoGame.Framework.DesktopGL 3.8.4.1, RogueSharp 4.2.0, xUnit (test project only).

---

## Architectural ground rules

1. **Registry pattern with reflection auto-discovery.** Any `public static readonly XDefinition Foo` field anywhere in the assembly gets registered via `ContentInitializer.Initialize()`. **Adding a class/item/skill/enemy/boss/modifier = drop one file, no central edits.**
2. **Behavior via interfaces.** `ISkillBehavior`, `ILegendaryEffect`, `IModifierBehavior`. Definitions hold data + a strategy reference.
3. **Code-defined content for v1.** No JSON loader yet — we'll add one if non-coders ever need to author. (Currently solo dev, so unnecessary.)
4. **Continuous position is the source of truth.** `Entity.ContinuousPosition` (Vector2 in tile-space) drives everything; integer `Position` is derived. Movement is per-frame velocity-based.
5. **Don't run unit tests** (per user CLAUDE.md). Tell user when ready.
6. **Use raw string literals for multi-line C# strings** (per user CLAUDE.md).
7. **No destructive git** (per user CLAUDE.md). No `reset --hard`, `push --force`, `branch -D`, `rebase`, `clean`, `stash drop/clear`, etc.

---

## Current state of the build

When you `run`, you get:
- **Window**: 80×30 cells. Square-cells mode active (12×12 Milazzo CP437 font), so window is 960×360 px at 1× zoom.
- **HUD** (top row): walker name, HP, current target HP, current zoom level.
- **Scaffold map**: 80×30 bordered room, interior wall with a door (`+`), threshold tile (`>`).
- **FOV / fog of war**: only a 10-tile radius around the player renders in full color; explored-but-not-currently-visible tiles render dim (RGB × 0.3); never-explored tiles render black. Walls and doors block sight. Enemy sprites are hidden when their tile is outside the player's FOV.
- **Player** `@` (Reaver, red glyph) spawns center, glides smoothly toward cursor at 8 tiles/sec.
- **Two `w` wolves** spawn (one upper-left, one lower-right of player). They have HP and die. They don't move or fight back yet.
- **A\* pathfinding** around walls, axis-separated wall-slide collision, line-of-sight optimization.
- **Combat** kicks in on click — walks into melee range, auto-attacks every 0.8s for 10 damage.

### Controls

| Input | Behavior |
|---|---|
| **Left-click on enemy** | Attack-target: walk into range, auto-attack |
| **Left-click on floor** | Walk there |
| **Hold Right-click** | Force-move toward cursor (ignores enemies) |
| **Shift + Left-click on enemy** | Force-stand-attack — no approach, only swings if adjacent |
| **`+` / `-` / mouse wheel** | Zoom: 0.5×, 1×, 1.5×, 2×, 2.5×, 3× |

### Tunable constants (single source of truth)

| Constant | Value | Where |
|---|---|---|
| `MovementController.TilesPerSecond` | `8f` | `Movement/MovementController.cs` |
| `MovementController.WaypointArriveDistance` | `0.15f` | same |
| `MovementController.TargetArriveDistance` | `0.05f` | same |
| `CombatController.MeleeRange` | `1.4f` (covers diagonals) | `Combat/CombatController.cs` |
| `CombatController.AutoAttackCooldownSec` | `0.8f` | same |
| `CombatController.BaseDamage` | `10` | same |
| `RenderSettings.UseSquareCells` | `true` (Option B) | `Core/RenderSettings.cs` |
| `RenderSettings.SquareFontPath` | `"Content/font_12x12.font"` | same |
| `RenderSettings.EnableFov` | `true` (debug-bisection toggle) | same |
| `RenderSettings.UnseenDimFactor` | `0.3f` | same |
| `GameScreen.FovRadius` | `10` (will move to `ModifierContext.FieldOfViewRadius` once context is plumbed in) | `UI/GameScreen.cs` |
| `ModifierContext.FieldOfViewRadius` | `10.0f` (canonical baseline; matches `GameScreen.FovRadius`) | `Modifiers/ModifierContext.cs` |

---

## Recently completed (newest first)

### FOV / fog of war
- **`World/Map.cs`** — added `ComputeFovFor(viewer, radius)`, `IsInFov(p)`, `IsExploredAt(p)` thin pass-throughs. RogueSharp's `IMap` already tracks `IsInFov` / `IsExplored` per cell internally; we just call into it. `lightWalls: true` so opaque tiles at the FOV boundary remain visible.
- **`World/Tile.cs`** — deleted unused `IsVisible` / `IsExplored` properties (single source of truth lives on the wrapped RogueSharp map).
- **`UI/GameScreen.cs`** — added `FovRadius = 10` const + `_lastPlayerTile` sentinel field. Ctor seeds FOV after player spawn so the first paint is already FOV-aware (no reveal flash). `Update` recomputes FOV only when the player crosses a tile boundary. `DrawMap` now runs every tick with three branches: in-FOV → full color; explored → dim (RGB × 0.3 via a `Dim(Color, float)` helper); unseen → blank black. Enemy visuals get `IsVisible` set from `_map.IsInFov(entity.Position)` in `SyncVisual`.
- **`Modifiers/ModifierContext.cs`** — bumped `FieldOfViewRadius` default 8.0f → 10.0f to match `GameScreen.FovRadius` so future modifier code stacks against the same canonical baseline.
- **`Core/RenderSettings.cs`** — added `EnableFov` (debug bisection toggle) + `UnseenDimFactor` (`0.3f`) static-readonly fields.

### Square-cell font + zoom polish
- Bundled **Adam Milazzo 12×12 CP437 font** as a project Content asset.
- Preprocessed PNG via PowerShell + System.Drawing: magenta → transparent (28k px), glyphs → opaque white (8.6k px).
- Wrote `Content/font_12x12.font` JSON metadata (16 cols, 12×12, solid glyph 219).
- `Tarpg.csproj` got a `<Content Include="Content\**\*">` block to copy assets to `bin/.../Content/` at build.
- `Core/RenderSettings.cs` — `UseSquareCells` (static readonly so the toggle doesn't generate dead-code warnings) + `SquareFontPath`.
- `Program.cs` resolves the font path against `AppContext.BaseDirectory` (so it works regardless of cwd) and chooses the font via the toggle.
- `GameScreen.SquareFontSize` updated to `(12, 12)`.
- **Half-step zoom levels** (0.5/1/1.5/2/2.5/3). Note: only integer multiples are pixel-crisp; half-steps are fuzzy at non-integer scales — accepted trade-off for accessibility.

### Mouse modes
- **Right-click hold** = force-move (drift toward cursor, ignore enemies).
- **Shift + Left-click on enemy** = force-stand-attack. New `CombatController.ForceStand` flag; GameScreen skips the approach phase when set.
- `_shiftHeld` tracked via `ProcessKeyboard` since `MouseScreenObjectState` doesn't expose modifier keys.

### Zoom system
- `+` / `-` keys (and numpad add/subtract) and mouse wheel.
- 6 levels (0.5, 1, 1.5, 2, 2.5, 3); HUD shows the current multiplier.
- Calls `Game.Instance.ResizeWindow(W, H, FontSize, true)` to grow/shrink the OS window so all cells stay visible.
- Discovered SadConsole's `EntityManager` (not `Renderer` as I initially guessed) and `IFont.GetFontSize` API along the way.

### Aspect-ratio correction (Option A for vertical-movement question)
- `MovementController.Tick(...)` takes a `cellAspect` parameter.
- Pixel-corrected distance for direction normalization so visual pixel-speed is uniform across X/Y at non-square cells.
- GameScreen passes `(float)FontSize.Y / FontSize.X`. Naturally becomes 1.0 in square mode (no-op), 2.0 in native IBM mode.

### First enemy + auto-attack melee combat
- `Entity` base now carries `MaxHealth`, `Health`, `IsDead`, `TakeDamage(int)`.
- `Player` pulls stats from `WalkerClassDefinition.BaseHealth` / `BaseResource`.
- `Enemy : Entity` wraps `EnemyDefinition`; factory `Create(def, tile)`.
- `CombatController` — target tracking, cooldown, melee range, damage application. Parallel to `MovementController`, no coupling between them.
- `GameScreen` orchestrates: spawns wolves, click-to-target, "approach if out of range, else attack", reaper for dead enemies, HUD.

### Continuous Diablo-style movement (Tier 2 from the movement-options brainstorm)
- `Entity.ContinuousPosition` (System.Numerics.Vector2 in tile-space) is the source of truth; `Position` is derived via floor.
- `MovementController` — A* fallback when LOS is blocked; axis-separated wall-slide collision; LOS revalidation each tick to drop waypoints when they're no longer needed.
- `TileLineOfSight.HasLineOfSight(map, from, to)` — grid raycast.
- Player rendered via `SadConsole.Entities.Entity` with `UsePixelPositioning = true` instead of being painted to the surface.

### Initial scaffold
- Solution + .NET 8 console project + NuGet packages (SadConsole, MonoGame, RogueSharp).
- Folder structure for every content category: Core / World / Entities / Items / Skills / Classes / Enemies / Bosses / Modifiers / UI / Movement / Combat.
- One example per category to demonstrate the registry pattern (Wolfbreaker legendary, Cleave skill, all 4 walker classes, Wolf enemy, WolfMother boss stub, BurningFloor modifier).
- xUnit test project + `run.bat` / `test.bat`.
- `.gitignore`, `global.json` pinning .NET 8.

### Design + research docs
- **`docs/design/game-design.md`** — master GDD, 15 sections covering vision, setting, classes, descent, bosses, town, economy, difficulty, etc.
- **`docs/research/arpg-design-research.md`** — foundational research, 13 design pillars + 19 sources.

---

## Up next (immediate work queue)

### 1. BSP procgen — Wolfwood floor 1
**Goal**: replace `BuildScaffoldMap` with a real procgen Wolfwood floor.

**Approach**: BSP for room/corridor layout (rectangular rooms), maybe a cellular automata pass for "natural" rough edges since Wolfwood is forest. Entry tile, anchor for boss room (Wolf-Mother), 3–6 rooms per floor.

**Files to touch**: new `World/Generation/BspGenerator.cs`, `World/Generation/Zone.cs`; refactor the consumer in `GameScreen`.

**Estimated effort**: 1–2 sessions.

---

## Roadmap (ordered, but not strict)

### Soon — next 3–5 sessions
- [ ] BSP procgen — Wolfwood floor 1 (above)
- [ ] **Hit feedback (juice v0)**: enemy red flash on hit, kill burst (radial spray glyphs `*` `'` `,`), floating damage numbers, hit-stop ~80ms on heavy hits
- [ ] **Camera follow**: once procgen makes maps bigger than viewport, scroll the world to keep player centered. Unlocks zoom-with-camera (Option B from the zoom discussion).
- [ ] **Enemy AI v0**: wolves chase player when in FOV, attack when in range. Tag-driven: `EnemyDefinition.AiTag` (already exists) maps to a behavior class.
- [ ] **Multi-floor descent**: stair tile, floor counter, regen new map on descend.

### Mid-term — skills and loot
- [ ] **All four classes' starter skills** (~10 per class = ~40 skills) — wire into right-click skill slot for v0.
- [ ] **Loot drop on enemy kill**: dropped items render as glyphs on tile, walk over to pick up.
- [ ] **Inventory UI**: 32-slot bag + 8 equipment slots, drag to equip, right-click to use consumable.
- [ ] **Item tier system end-to-end**: drop with rarity color, unidentified Rare+ shroud, Reading Stone in town for ID.
- [ ] **Wolf-Mother boss**: first iconic encounter, signature mechanic (pack summon? leap?), Wolfbreaker drop.

### Mid-term — world / town
- [ ] **Walker's Hold town map**: 8 named NPCs (Eldest, Reader, Steward, Smith, Apothecary, Innkeeper, Marshal, Sigil-Maker) with dialogue stubs.
- [ ] **Town↔dungeon transition**: Threshold tile in town leads to dungeon; death returns to town.
- [ ] **Stash + persistence**: Steward's stash survives across delves and characters.
- [ ] **Death + corpse run**: lose XP, drop one item, reclaim from corpse on next delve.

### Endgame layer
- [ ] **Echo-pact mechanic**: post-boss-kill choice between loot and binding.
- [ ] **Floor modifiers**: 0–3 per floor from `ModifierDefinition` registry, HUD indicator.
- [ ] **Zone loops**: floor 35 → loop 2 (Wolfwood II), scaling difficulty + drops.
- [ ] **All 5 zones**: Wolfwood, Drowned Hall, Hollow Court, Forgotten Fair, Last Room.
- [ ] **All 5 named bosses**.

### Polish / juice (parallel track)
- [ ] **Audio palette**: per-zone ambient, per-glyph SFX, hit-type audio, silence between waves.
- [ ] **Color flashes / screen shake / particles** beyond v0 hit feedback.
- [ ] **Threshold-ritual fast-travel UI**: pick zone + floor to enter.

---

## Deferred (explicitly not doing yet)

These are in GDD section 14 — known unknowns we'll figure out by prototyping, not pre-spec'ing:

- Specific Legendary effects (one example exists: Wolfbreaker)
- Specific monster stats per zone
- Concrete XP curve / damage scaling numbers
- NPC dialogue
- Quest content (Eldest's charges, Echo lore-quests)
- UI layout details
- Visual movement easing (acceleration / deceleration on stop)

---

## Open questions / known issues

- **Half-step zoom (1.5×, 2.5×) is fuzzy** with the 12×12 Milazzo font. Integer multiples (1×, 2×, 3×) are crisp. Acceptable trade-off; can document or remove half-steps if it bothers user.
- **Mouse hover doesn't preview attack target** — would help readability ("am I going to engage this wolf if I click here?")
- **Enemy AI is stationary** — wolves are punching bags right now.
- **No save/load yet** — character persists only within the running session.
- **Player can't be hit by enemies** — combat is one-sided (no enemy→player attack, no player damage taken).
- **No corpses / loot on death** — enemies just disappear when killed.
- **Click-to-target ignores FOV** — Shift+click on enemy still works at any distance regardless of visibility; click-to-walk targets and `FindLiveEnemyAt` consult position only. Revisit when enemy AI lands so unseen enemies aren't clickable.

---

## File map

```
docs/
  research/arpg-design-research.md    Foundational ARPG research (332 lines)
  design/game-design.md               The master GDD (~480 lines, 15 sections)
  STATUS.md                           ← THIS FILE — living roadmap

src/Tarpg/
  Program.cs                          SadConsole bootstrap; picks font from RenderSettings
  Content/
    font_12x12.png                    Milazzo square CP437 (preprocessed; magenta → transparent)
    font_12x12.font                   SadConsole font metadata
  Core/
    Position.cs                       int X/Y record struct (tile coords)
    IRegistryEntry.cs                 marker interface (string Id)
    Registry.cs                       generic Registry<T>
    Registries.cs                     static typed registry instances
    ContentInitializer.cs             reflection-based auto-discovery
    ResourceType.cs                   Rage / Focus / Insight / Echo
    RenderSettings.cs                 UseSquareCells toggle + font path
  World/
    Map.cs                            tile grid + RogueSharp pathfinding wrap
    Tile.cs                           IsVisible / IsExplored
    TileType.cs                       walkable / transparent definition
    TileTypes.cs                      Floor / Wall / Door / Threshold
  Movement/
    MovementController.cs             continuous Diablo-style + A* fallback + aspect correction
    TileLineOfSight.cs                grid raycast LOS
  Combat/
    CombatController.cs               auto-attack target + cooldown + ForceStand flag
  Entities/
    Entity.cs                         base — ContinuousPosition, Health, IsDead, TakeDamage
    Player.cs                         walker class, level, resource
    Enemy.cs                          wraps EnemyDefinition
  Items/                              Definition, Tier, Slot, Affix, ILegendaryEffect, Wolfbreaker
  Skills/                             Definition, ISkillBehavior, Cleave
  Classes/                            WalkerClassDefinition, Reaver/Hunter/Cipher/Speaker
  Enemies/                            EnemyDefinition, Wolf
  Bosses/                             BossDefinition, WolfMother stub
  Modifiers/                          ModifierDefinition, IModifierBehavior, BurningFloor
  UI/
    GameScreen.cs                     orchestrator — input, render, tick

src/Tarpg.Tests/                      xUnit project (no tests written yet)

tarpg.sln, global.json, .gitignore, run.bat, test.bat
```

---

## How to update this doc

After every meaningful work session:

1. Move the completed item from **Up next** or **Roadmap** into **Recently completed** (newest first).
2. Update **Current state of the build** if behavior changed.
3. Add new entries to **Open questions / known issues** if you discovered something.
4. Update the **Updated** date at the top.
5. If a tunable constant changed, update the **Tunable constants** table.

Keep entries concise — link to git history / files for the gory details. The point of this doc is fast orientation, not full reproducibility.
