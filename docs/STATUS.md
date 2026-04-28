# TARPG — Master Status

> **Updated**: 2026-04-28
> **Where we are**: continuous-movement Diablo-style ARPG with FOV / fog of war, procgen Wolfwood floor 1 (BSP), active enemy AI (chase + attack), and v0 hit feedback (flash, damage numbers, kill burst, hit-stop); one enemy type (wolf); one playable class (Reaver); two-sided auto-attack melee combat; mouse modes; zoom; bundled square-cell font; on-death floor regen.

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
- **Procgen Wolfwood floor**: BSP-generated floor with ~4–7 rectangular rooms, L-shaped corridors connecting siblings, walls everywhere else. Different layout each run; seed is logged to stdout. A Threshold (`>`) tile sits in the room farthest from the entry as the future-boss anchor.
- **FOV / fog of war**: a circular 10-tile radius around the player renders in full color; explored-but-not-currently-visible tiles render dim (RGB × 0.5); never-explored tiles render black. Walls block sight. Enemy sprites are hidden when their tile is outside the player's FOV.
- **Player** `@` (Reaver, red glyph) spawns at the floor's entry tile, glides smoothly toward cursor at 8 tiles/sec.
- **Up to 8 `w` wolves** scatter across non-entry, non-boss rooms (1–2 per room), at least 6 chebyshev tiles from the entry. They aggro when they enter the player's FOV (mutual LOS), keep chasing for 3 sec after losing sight, A* through walls, swing every 0.8 sec when adjacent for `BaseDamage` (4) per hit. Player can die — when HP hits 0, the floor regenerates with full HP.
- **Hit feedback (juice v0)**: every successful hit flashes the target's glyph red for 120ms, spawns a drifting damage number (red on player, yellow on enemy), and freezes all movement / AI for 80ms hit-stop. Enemy deaths spray a 7-particle radial burst of `*` `'` `,` `.` `` ` `` glyphs that drift outward over 400ms.
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
| `RenderSettings.UnseenDimFactor` | `0.5f` | same |
| `GameScreen.FovRadius` | `10` (will move to `ModifierContext.FieldOfViewRadius` once context is plumbed in) | `UI/GameScreen.cs` |
| `ModifierContext.FieldOfViewRadius` | `10.0f` (canonical baseline; matches `GameScreen.FovRadius`) | `Modifiers/ModifierContext.cs` |
| `BspGenerator.MinLeafWidth` / `MinLeafHeight` | `12` / `8` | `World/Generation/BspGenerator.cs` |
| `BspGenerator.MaxDepth` | `4` | same |
| `BspGenerator.MinRoomWidth` / `MinRoomHeight` | `4` / `3` | same |
| `BspGenerator.RoomEdgeMargin` | `1` | same |
| `BspGenerator.MinSpawnDistanceFromEntry` | `6` (chebyshev) | same |
| `BspGenerator.MaxEnemiesPerFloor` | `8` | same |
| `BspGenerator.SplitAspectRatio` | `1.25f` | same |
| `MeleeChargerAi.AggroMemorySec` | `3.0f` (chase persists this long after losing FOV) | `Enemies/Ai/MeleeChargerAi.cs` |
| `MeleeChargerAi.AttackCooldownSec` | `0.8f` | same |
| `MeleeChargerAi.MeleeRange` | `1.4f` (matches `CombatController.MeleeRange`) | same |
| `HitFeedback.FlashSec` | `0.12f` | `UI/Effects/HitFeedback.cs` |
| `HitFeedback.FlashColor` | `(255, 80, 80)` red | same |
| `HitFeedback.DamageNumberLifeSec` | `0.6f` | same |
| `HitFeedback.DamageNumberDriftTilesPerSec` | `1.5f` | same |
| `HitFeedback.KillBurstLifeSec` | `0.4f` | same |
| `HitFeedback.KillBurstSpeedTilesPerSec` | `4.0f` | same |
| `HitFeedback.KillBurstParticles` | `7` | same |
| `HitFeedback.HitStopSec` | `0.08f` | same |

---

## Recently completed (newest first)

### Hit feedback (juice v0)
- **`Entities/Entity.cs`** — added `event Action<Entity, int>? Damaged` and `event Action<Entity>? Died` that fire from `TakeDamage` after the health adjustment. Damaged passes the actual amount applied (clamped); Died fires once on the zero-crossing transition.
- **`UI/Effects/HitFeedback.cs`** (new) — single class managing all four effect types over the existing `SadEntityManager`. Per-entity flash timers (`Dictionary<Entity, float>`), damage numbers (per-digit `SadEntity` instances that drift upward over 0.6s with a small horizontal jitter), kill bursts (7 radial particles with random glyph from `* ' , . `` ` and ±0.4 rad jitter, 0.4s lifetime), and a global `HitStopRemaining` timer set to 0.08s on every successful hit. `Tick(deltaSec, fontSize)` advances all of it; `Clear()` tears everything down for floor regen.
- **`UI/GameScreen.cs`**:
  - Owns a `HitFeedback _hitFeedback` constructed from the same `_entityManager` it uses for the player + wolf visuals.
  - Hooks `OnEntityDamaged` / `OnEntityDied` to the player and to each enemy at spawn time. Damaged routes to `HitFeedback.OnDamaged` with red number color for player-took / yellow for enemy-took. Died routes to `HitFeedback.OnDied` (kill burst), but only for enemies — player death falls through to the regen flow.
  - `Update` reads `_hitFeedback.HitStopRemaining > 0` into a local `frozen` flag that gates both player movement / combat AND enemy AI. The hit-stop timer itself counts down via `_hitFeedback.Tick(deltaSec, FontSize)` — also called every frame so damage numbers and particles keep animating during the freeze.
  - `SyncVisual` overrides the entity's foreground to `HitFeedback.FlashTint` while `IsFlashing` is true, restoring the entity's base color the next frame.
  - `RegenerateFloor` calls `_hitFeedback.Clear()` first so stale damage numbers from the previous floor don't survive across the swap.

### Enemy AI v0
- **`Enemies/Ai/IEnemyAi.cs`** — strategy interface: `Tick(self, player, map, deltaSec, cellAspect)`. One instance per enemy so per-actor state (aggro timer, last-seen position, attack cooldown, MovementController) lives on the AI.
- **`Enemies/Ai/MeleeChargerAi.cs`** — Wolf's brain. While the wolf's tile is in the player's FOV (FOV is symmetric, so this means mutual LOS), aggro is set to `AggroMemorySec = 3.0` and `_lastSeenPlayerPos` updates to the player's continuous position. Out of FOV the timer decays. While aggro is alive: if distance to last-seen > `MeleeRange (1.4)`, call `MovementController.RetargetTo + Tick` to chase (continuous, A* fallback through walls); if in melee range, `_movement.Stop()` and swing every `AttackCooldownSec (0.8)` — but only if currently in FOV (no whiffing into stale last-seen tile).
- **`Entities/Enemy.cs`** — added `required IEnemyAi Ai`. `Enemy.Create` resolves `def.AiTag` ("melee_charger") via a switch-on-string `ResolveAi` factory; throws if the tag isn't registered. Each enemy gets a fresh AI instance so per-actor state is isolated.
- **`UI/GameScreen.cs`**:
  - `_map` is no longer `readonly` — `RegenerateFloor` swaps in a fresh map on player death.
  - `Update` now: player movement/combat → `ReapDead` → FOV recompute (on tile-cross) → tick every live enemy's AI → check `_player.IsDead` → `RegenerateFloor` if dead → DrawMap / sync visuals / HUD.
  - New `RegenerateFloor()` clears enemies + visuals, regens via the Wolfwood generator with a fresh seed, repositions player at floor.Entry, restores HP to MaxHealth, re-seeds FOV, respawns wolves. Real corpse-run / XP-loss death deferred.

### BSP procgen — Wolfwood floor 1
- **Zone abstraction**: new `World/ZoneDefinition.cs` (`IRegistryEntry` with `Id`, `Name`, `IZoneGenerator Generator`). Mirrors the existing `XDefinition` + behavior-strategy pattern (`SkillDefinition`+`ISkillBehavior`, etc.).
- **`World/Generation/IZoneGenerator.cs`** — `Generate(int width, int height, int seed) → GeneratedFloor`. Strategy reference held by `ZoneDefinition.Generator`.
- **`World/Generation/GeneratedFloor.cs`** — value object carrying `Map`, `Entry`, `BossAnchor`, `IReadOnlyList<Position> EnemySpawnPoints`. `required init` to match the existing definition idiom.
- **`World/Generation/BspGenerator.cs`** — recursive BSP split with aspect-ratio-forced direction, `MinLeafWidth=12 / MinLeafHeight=8` gates, `MaxDepth=4`. Carve rectangular rooms in each leaf (1-tile margin, min `4×3`). Connect sibling leaves with random-orientation L-shaped corridors at midpoint of leaf bounding boxes. Pick entry from `rooms[0]`; pick boss anchor from the room farthest by chebyshev (place a `Threshold` tile there as future-boss marker). Scatter 1–2 enemy spawn points per non-entry, non-boss room, ≥6 chebyshev from entry, no duplicates, capped at 8 total. Defensive throw if `rooms.Count < 2` with seed in the message. No CA pass and no doors — both deferred per design call (doors over-pop FOV in a 5-room procgen, CA needs a connectivity-repair flood fill).
- **`World/Zones/Wolfwood.cs`** — `static class Wolfwood { public static readonly ZoneDefinition Definition = ... }`. `Id="wolfwood"` matches the strings already on `Wolf.ZoneIds` and `WolfMother.ZoneId`.
- **`Core/Registries.cs`** + **`Core/ContentInitializer.cs`** — added `Zones` registry and `ZoneDefinition` route in `BuildRouteMap`. Reflection auto-discovery picks up `Wolfwood.Definition` with no further wiring.
- **`UI/GameScreen.cs`** — deleted `BuildScaffoldMap`. Ctor now does `Registries.Zones.Get("wolfwood").Generator.Generate(width, height, Environment.TickCount)`, logs the seed via `System.Console.WriteLine`, spawns the player at `floor.Entry` and a Wolf at each of `floor.EnemySpawnPoints`.

### FOV / fog of war
- **`World/Map.cs`** — added `ComputeFovFor(viewer, radius)`, `IsInFov(p)`, `IsExploredAt(p)`. Visibility / explored state is owned by `Map` (parallel `bool[,]` arrays), not RogueSharp. RogueSharp's `ComputeFov` is called with a Manhattan radius of `⌈radius·√2⌉` (so its diamond fully contains our desired Euclidean circle), then we walk the bounding box of the circle and only mark a tile visible if both RogueSharp says it has LOS *and* its Euclidean distance from the viewer is within radius. Result: properly circular reveal shape, correct shadowcast LOS through walls, and an explored layer we own (not dependent on RogueSharp's stickiness behavior).
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

### 1. Camera follow + larger maps
**Goal**: maps bigger than the 80×30 viewport, with the world scrolling to keep the player centered. Unlocks the Option-B zoom-with-camera approach we sidelined earlier and lets Wolfwood floors actually feel like zones to descend through.

**Approach**: SadConsole's `ScreenSurface.View` lets us render a window over a larger backing surface; tick that view origin from the player's position each frame. `BspGenerator` already accepts width/height parameters so larger floors are a single call-site change.

**Files to touch**: `UI/GameScreen.cs` (the orchestrator); possibly tweak `RenderSettings` or introduce a `WorldSize` constant.

**Estimated effort**: 1–2 sessions.

### 2. Multi-floor descent
**Goal**: walking onto the Threshold tile triggers a new floor at +1 floor depth. The Threshold finally does something.

**Approach**: detect when the player's tile becomes Threshold; trigger a new generation with `floor + 1`. `IZoneGenerator.Generate` gains a `floor: int` parameter (planned-for one-line change).  Hook for difficulty scaling: enemy roll table, count, stat multipliers.

**Files to touch**: `World/Generation/IZoneGenerator.cs`, `BspGenerator.cs`, `UI/GameScreen.cs`, status display in HUD for current floor.

**Estimated effort**: 1 session.

---

## Roadmap (ordered, but not strict)

### Soon — next 3–5 sessions
- [ ] Camera follow + larger maps (above)
- [ ] Multi-floor descent (above)
- [ ] **Real corpse-run death**: replace "regen on death" with corpse drop, XP loss, return-to-town. Town doesn't exist yet so this is multi-step.
- [ ] **Weighted enemy rolls**: replace hardcoded `Wolf.Definition` in spawn with a weighted draw from `Registries.Enemies.All.Where(e => e.ZoneIds.Contains(zone.Id))` using `EnemyDefinition.RarityWeight`.
- [ ] **Cellular automata roughening for Wolfwood floor edges** + flood-fill connectivity repair.
- [ ] **More AI behaviors**: ranged attackers (`ranged_caster`?), pack-mob coordination, fleeing-when-low-HP. Each gets a new `IEnemyAi` impl + tag mapping in `Enemy.ResolveAi`.

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
- **No save/load yet** — character persists only within the running session.
- **No corpses / loot on death** — enemies just disappear when killed.
- **Click-to-target ignores FOV** — Shift+click on enemy still works at any distance regardless of visibility; click-to-walk targets and `FindLiveEnemyAt` consult position only. Now that the player can take damage, this lets you stand-attack unseen wolves through walls — worth tightening soon.
- **Enemy↔enemy collision doesn't exist** — multiple wolves chasing the player will overlap on the same tile when they reach melee range. Visual stacking only; doesn't break gameplay.
- **Death → free regen with full HP** — placeholder until corpse-run / XP-loss death lands.
- **Enemy spawns hardcoded to Wolf** — should roll from `Registries.Enemies.All.Where(ZoneIds contains zone.Id)` weighted by `RarityWeight`. Small follow-up.
- **Boss anchor uses Threshold tile as a placeholder** — when descent lands, Threshold becomes the next-floor stair, so we'll need a distinct tile (or a marker layer) for boss-spawn points. Today the player can walk onto the Threshold and nothing happens.
- **Cellular automata pass for forest feel deferred** — listed under Polish / juice. Needs flood-fill connectivity repair before BSP+CA is safe.
- **Doors at room↔corridor junctions deferred** — placed deliberately later (boss-arena gates, town buildings). Wolfwood gets none.

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
    Map.cs                            tile grid + RogueSharp pathfinding wrap + FOV state
    Tile.cs                           per-cell type reference (visibility lives on Map)
    TileTypeDefinition.cs             walkable / transparent definition
    TileTypes.cs                      Floor / Wall / Door / Threshold
    ZoneDefinition.cs                 IRegistryEntry — id, name, generator
    Generation/
      IZoneGenerator.cs               strategy interface — Generate(w, h, seed) → GeneratedFloor
      GeneratedFloor.cs               value: Map, Entry, BossAnchor, EnemySpawnPoints
      BspGenerator.cs                 BSP split → carve rooms → L-corridors → pick anchors
    Zones/
      Wolfwood.cs                     ZoneDefinition for the first mythic zone
  Movement/
    MovementController.cs             continuous Diablo-style + A* fallback + aspect correction
    TileLineOfSight.cs                grid raycast LOS
  Combat/
    CombatController.cs               auto-attack target + cooldown + ForceStand flag
  UI/
    GameScreen.cs                     orchestrator — input, render, tick
    Effects/
      HitFeedback.cs                  flash + damage numbers + kill burst + hit-stop
  Enemies/
    EnemyDefinition.cs / Wolf.cs      registry-discovered enemy data
    Ai/
      IEnemyAi.cs                     strategy interface — Tick(self, player, map, dt, aspect)
      MeleeChargerAi.cs               Wolf brain: FOV-aggro w/ 3s memory, A* chase, 0.8s swing
  Entities/
    Entity.cs                         base — ContinuousPosition, Health, IsDead, TakeDamage
    Player.cs                         walker class, level, resource
    Enemy.cs                          wraps EnemyDefinition
  Items/                              Definition, Tier, Slot, Affix, ILegendaryEffect, Wolfbreaker
  Skills/                             Definition, ISkillBehavior, Cleave
  Classes/                            WalkerClassDefinition, Reaver/Hunter/Cipher/Speaker
  Bosses/                             BossDefinition, WolfMother stub
  Modifiers/                          ModifierDefinition, IModifierBehavior, BurningFloor

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
