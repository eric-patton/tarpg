# TARPG — Master Status

> **Updated**: 2026-04-30
> **Where we are**: continuous-movement Diablo-style ARPG with FOV / fog of war, procgen Wolfwood (BSP) on a 160×60 world surface with an 80×30 sub-cell-smoothed camera-follow viewport, multi-floor descent on Threshold step with per-floor stat + count scaling, three enemy AI archetypes (`melee_charger`, `melee_skirmisher`, `ranged_kiter`), five Wolfwood enemy types (wolf, wolf pup horde, dire wolf, wolfshade skirmisher, howler ranged), v0 hit feedback (flash, damage numbers, kill burst, hit-stop); two playable classes (Reaver melee + Hunter ranged), each with a full 5-slot kit (M2/Q/W/E/R) wired via `WalkerClassDefinition.StartingSlotSkills`; HP / Resource potions drop on enemy kill, render on the floor, pick up on tile-cross, drink with `1` / `2`; two-sided auto-attack combat; forgiving 1.5-tile click-target radius; click indicator pulse + drift-on-unreachable; mouse modes; zoom; bundled square-cell font; on-death floor regen. Headless game-loop core (`GameLoopController`) plus xUnit suite (skills, AIs, BSP, Items, Movement, Combat, slot-wiring) and a `tarpg-sim` CLI that sweeps (floor × seed × class) grids for balance tuning.

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
- **Window**: 80×30 cell viewport (Square-cells mode active, 12×12 Milazzo CP437 font, so window is 960×360 px at 1× zoom).
- **World**: 160×60 cells per generated floor, ~4× the area of the viewport. Camera follows the player and clamps to map edges.
- **HUD** (top row of viewport): walker name, HP, zone + floor depth, current target HP, current zoom level.
- **Procgen Wolfwood floor**: BSP-generated floor with ~4–10 rectangular rooms (more than before thanks to the larger map), L-shaped corridors connecting siblings, walls everywhere else. Different layout each run; seed is logged to stdout. A Threshold (`>`) tile sits in the room farthest from the entry — walking onto it descends to the next floor.
- **Multi-floor descent**: stepping onto the Threshold loads a fresh layout at floor depth +1. HP carries over. Per-floor difficulty scaling is deferred (every floor is identical density / stat-wise for now).
- **FOV / fog of war**: a circular 10-tile radius around the player renders in full color; explored-but-not-currently-visible tiles render dim (RGB × 0.5); never-explored tiles render black. Walls block sight. Enemy sprites are hidden when their tile is outside the player's FOV.
- **Player** `@` (Reaver, red glyph) spawns at the floor's entry tile, glides smoothly toward cursor at 8 tiles/sec.
- **Enemy roster**: each spawn slot is a weighted draw from `Registries.Enemies.All` filtered by zone, then expanded into a pack of `def.PackSize` copies fanned out in chebyshev rings around the BSP-chosen point. Wolfwood pool today:
  - `w` **wolf** — weight 5, HP 18, Dmg 4, speed 6, `melee_charger`. Standard mid-tier.
  - `p` **wolf pup** — weight 3, HP 5, Dmg 1, speed 7, `melee_charger`, **PackSize 3**. Horde tier — each slot spawns 3 fast-but-fragile pups, creating swarm pressure.
  - `W` **dire wolf** — weight 1, HP 50, Dmg 9, speed 4.5, `melee_charger`. Slow elite.
  - `s` **wolfshade** — weight 1, HP 22, Dmg 6, speed 6.5, `melee_skirmisher`. Bites then retreats ~4 tiles for ~600ms before re-engaging — back-and-forth pacing instead of grind.
  - `h` **howler** — weight 1, HP 14, Dmg 5, speed 5, `ranged_kiter`. Maintains 4–6 tile band from player and fires hitscan damage on a 1.4s cadence when LOS holds; backpedals when you close.
  All AIs share FOV-symmetric mutual-LOS aggro with 3s memory. Player can die — when HP hits 0, the current floor regenerates with full HP, same depth.
- **Per-floor scaling**: descent depth ramps both spawn slot count (`5 + floor`, capped at 12) and per-enemy stats. HP × `(1 + 0.15·(F−1))`, Dmg × `(1 + 0.10·(F−1))`. F1 spawns ~7 enemies; F8 spawns ~12 slots × ~30% pup-pack expansion ≈ 17 enemies, each with ~2× HP / ~1.7× damage of base.
- **Forgiving click-targeting**: left-click picks the nearest live enemy within 1.5 tiles of the clicked cell, not just one on the exact tile. Empty floor clicks within 1.5 tiles of an enemy still attack instead of walk; right-click bypasses this for "walk past enemies" cases.
- **Drift-on-unreachable**: clicks on a wall, deep inside an unreachable region, or off the map drift the player toward the cursor instead of doing nothing — collision sliding handles wall contact. Same fallback applies to enemy-chase pathfinding when A* fails.
- **Click indicator pulse**: releasing the left mouse drops a brief `+` glyph at the cursor cell that fades over 250ms. Confirms "the game saw your click" without leaving a permanent marker.
- **Shift-while-walking toggles to stand**: while LMB is held to walk, pressing shift stops the player in place without releasing the mouse first. Existing combat targets are preserved (shift+click on enemy → drag cursor across floor doesn't cancel the attack-stand).
- **Hit feedback (juice v0)**: every successful hit flashes the target's glyph red for 120ms, spawns a drifting damage number (red on player, yellow on enemy), and freezes all movement / AI for 80ms hit-stop. Enemy deaths spray a 7-particle radial burst of `*` `'` `,` `.` `` ` `` glyphs that drift outward over 400ms.
- **A\* pathfinding** around walls, axis-separated wall-slide collision, line-of-sight optimization.
- **Combat** kicks in on click — walks into melee range, auto-attacks every 0.8s for 10 damage.

### Controls

| Input | Behavior |
|---|---|
| **Left-click on enemy** | Attack-target: walk into range, auto-attack |
| **Left-click on floor** | Walk there |
| **Right-click** | M2 — Heavy Strike (cursor-cell, range-gated, 25 dmg, 0 cost, 1.5s cd) |
| **Q** | Cleave (chebyshev-adjacent AOE, 10 dmg, 10 Rage, 1.0s cd) |
| **W** | Charge (dash up to 6 tiles toward cursor, first enemy hit takes 15 dmg, 15 Rage, 5s cd) |
| **E** | War Cry (heal 25 HP, 25 Rage, 12s cd) |
| **R** | Whirlwind (chebyshev-2 AOE around caster, 15 dmg, 30 Rage, 6s cd) |
| **1** | Drink HP potion (`Potions.HealthPotionHealAmount` HP, 0.5s drink cd) |
| **2** | Drink Resource potion (`Potions.ResourcePotionRestoreAmount` resource, 0.5s drink cd) |
| **Shift + Left-click on enemy** | Force-stand-attack — no approach, only swings if adjacent |
| **Shift + Left-click on empty floor** | Stop walking in place (only when there's no active combat target) |
| **`+` / `-` / mouse wheel** | Zoom: 0.5×, 1×, 1.5×, 2×, 2.5×, 3× |

### Tunable constants (single source of truth)

| Constant | Value | Where |
|---|---|---|
| `MovementController.DefaultTilesPerSecond` | `8f` (player baseline; enemies override per `EnemyDefinition.MoveSpeed`) | `Movement/MovementController.cs` |
| `MovementController.WaypointArriveDistance` | `0.15f` | same |
| `MovementController.TargetArriveDistance` | `0.05f` | same |
| `CombatController.MeleeRange` | `1.4f` (covers diagonals) | `Combat/CombatController.cs` |
| `CombatController.AutoAttackCooldownSec` | `0.8f` | same |
| `CombatController.BaseDamage` | `10` | same |
| `RenderSettings.UseSquareCells` | `true` (Option B) | `Core/RenderSettings.cs` |
| `RenderSettings.SquareFontPath` | `"Content/font_12x12.font"` | same |
| `RenderSettings.EnableFov` | `true` (debug-bisection toggle) | same |
| `RenderSettings.UnseenDimFactor` | `0.5f` | same |
| `GameLoopController.FovRadius` | `10` (canonical now; `ModifierContext.FieldOfViewRadius` still mirrors but the loop owns it) | `Core/GameLoopController.cs` |
| `ModifierContext.FieldOfViewRadius` | `10.0f` (canonical baseline; matches `GameLoopController.FovRadius`) | `Modifiers/ModifierContext.cs` |
| `BspGenerator.MinLeafWidth` / `MinLeafHeight` | `12` / `8` | `World/Generation/BspGenerator.cs` |
| `BspGenerator.MaxDepth` | `4` | same |
| `BspGenerator.MinRoomWidth` / `MinRoomHeight` | `4` / `3` | same |
| `BspGenerator.RoomEdgeMargin` | `1` | same |
| `BspGenerator.MinSpawnDistanceFromEntry` | `4` (chebyshev, room-center vs entry) | same |
| `BspGenerator.MaxEnemySlotsBase` / `Cap` | `6` / `12` (slots, not enemies — pack expansion can multiply) | same |
| `BspGenerator.SplitAspectRatio` | `1.25f` | same |
| `GameScreen.WorldWidth` / `WorldHeight` | `160` / `60` | `UI/GameScreen.cs` |
| `Program.ScreenWidth` / `ScreenHeight` | `80` / `30` (viewport, in cells) | `Program.cs` |
| `MeleeChargerAi.AggroMemorySec` | `3.0f` (chase persists this long after losing FOV) | `Enemies/Ai/MeleeChargerAi.cs` |
| `MeleeChargerAi.MeleeRange` | `1.4f` (matches `CombatController.MeleeRange`) | same |
| `SkirmisherAi.RetreatDistanceTiles` / `RetreatDurationSec` | `4.0f` / `0.6f` | `Enemies/Ai/SkirmisherAi.cs` |
| `RangedKiterAi.AttackRangeTiles` | `6.0f` | `Enemies/Ai/RangedKiterAi.cs` |
| `RangedKiterAi.PreferredDistanceMin` / `Max` | `4.0f` / `6.0f` | same |
| `EnemyDefinition.MoveSpeed` | per-enemy (wolves 6, pups 7, dires 4.5, shades 6.5, howlers 5) | `Enemies/EnemyDefinition.cs` |
| `EnemyDefinition.AttackCooldown` | per-enemy (default 0.8s; pups 0.6s, dires 1.0s, shades 1.0s, howlers 1.4s) | same |
| `EnemyDefinition.PackSize` | per-enemy (default 1; pups 3) | same |
| `GameScreen.HpScalePerFloor` / `DmgScalePerFloor` | `0.15f` / `0.10f` (linear per descent depth) | `UI/GameScreen.cs` |
| `GameScreen.PackSpreadRadiusMax` | `3` (chebyshev rings filled when expanding a pack) | same |
| `GameLoopController.OutOfCombatRegenDelaySec` / `RegenPerSec` | `3.0f` / `5.0f` (HP/sec after 3s without damage) | `Core/GameLoopController.cs` |
| `GameLoopController.ResourceGainPerAutoAttackHit` | `5` (Rage per landed auto-attack swing) | same |
| `GameScreen.LootDropChance` | `0.08f` (per enemy kill) | `UI/GameScreen.cs` |
| `Potions.HealthPotionHealAmount` | `40` HP | `Items/Potions.cs` |
| `Potions.ResourcePotionRestoreAmount` | `30` resource | same |
| `Potions.DrinkCooldownSec` | `0.5f` (per-potion-type spam gate) | same |
| `Cleave.CooldownSec` / `Cost` / `Damage` | `1.0f` / `10` Rage / `10` (matches base auto-attack) | `Skills/Cleave.cs` |
| `HeavyStrike.CooldownSec` / `Cost` / `Damage` | `1.5f` / `0` / `25` (single-target, range-gated) | `Skills/HeavyStrike.cs` |
| `Charge.CooldownSec` / `Cost` / `Damage` / `MaxDistanceTiles` | `5.0f` / `15` Rage / `15` / `6` | `Skills/Charge.cs` |
| `WarCry.CooldownSec` / `Cost` / `HealAmount` | `12.0f` / `25` Rage / `25` HP | `Skills/WarCry.cs` |
| `Whirlwind.CooldownSec` / `Cost` / `Damage` / `Radius` | `6.0f` / `30` Rage / `15` / `2` (chebyshev) | `Skills/Whirlwind.cs` |
| `QuickShot.CooldownSec` / `Cost` / `Damage` / `MaxRange` | `0.5f` / `0` / `12` / `6` (LOS-gated hitscan) | `Skills/QuickShot.cs` |
| `Volley.CooldownSec` / `Cost` / `Damage` / `MaxRange` / `Radius` | `1.0f` / `12` Focus / `8` / `6` / `1` (3×3 at cursor, LOS-gated) | `Skills/Volley.cs` |
| `Roll.CooldownSec` / `Cost` / `MaxDistanceTiles` | `4.0f` / `10` Focus / `4` (away from cursor) | `Skills/Roll.cs` |
| `Bandage.CooldownSec` / `Cost` / `HealAmount` | `12.0f` / `25` Focus / `25` HP | `Skills/Bandage.cs` |
| `RainOfArrows.CooldownSec` / `Cost` / `Damage` / `MaxRange` / `Radius` | `8.0f` / `35` Focus / `18` / `8` / `2` (5×5 at cursor, no LOS check) | `Skills/RainOfArrows.cs` |
| `RenderSettings.StartingClassId` | `"reaver"` (resolved against `Registries.Classes` at game start) | `Core/RenderSettings.cs` |
| `GameScreen.DashTilesPerSec` | `30f` (lerp speed for skill-induced teleports) | `UI/GameScreen.cs` |
| `SkillVfx.HighlightMaxTint` | `0.7f` (peak bg-color blend for area highlights) | `UI/Effects/SkillVfx.cs` |
| `SkillVfx.FlashPeakAlpha` | `160` (peak screen-flash alpha out of 255) | same |
| Per-skill VFX (highlight life / shake / flash) | Cleave 0.25s/2px·0.08s, HS 0.2s/3px·0.1s, WW 0.3s/5px·0.15s, WC green flash 0.4s | each `Skills/<Name>.cs` |
| `GameScreen.TopHudHeight` / `BottomHudHeight` | `1` / `5` (rows reserved at viewport edges for HUD overlays) | `UI/GameScreen.cs` |
| `StatusPanel.OrbWidth` / `OrbHeight` | `5` / `5` (HP / resource orb dimensions in cells) | `UI/Effects/StatusPanel.cs` |
| `StatusPanel.SlotWidth` / `SlotHeight` / `SlotGap` | `6` / `5` (full panel) / `4` | same |
| `StatusPanel.ConsumableWidth` / `ConsumableHeight` | `4` / `5` (potion-slot dimensions, flank the orbs) | same |
| `GameScreen.SlotCount` | `5` (M2, Q, W, E, R) | `UI/GameScreen.cs` |
| `GameScreen.ClickTargetRadius` | `1.5f` (Euclidean tiles around click for enemy pick) | `UI/GameScreen.cs` |
| `ClickIndicator.LifeSec` | `0.25f` (click-pulse fade-out duration) | `UI/Effects/ClickIndicator.cs` |
| `Wolf.RarityWeight` | `4` (common) | `Enemies/Wolf.cs` |
| `DireWolf.RarityWeight` | `1` (rare; ~20% of zone rolls) | `Enemies/DireWolf.cs` |
| `DireWolf.BaseHealth` / `BaseDamage` | `50` / `9` | same |
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

### Hunter — second playable class with full Focus kit
- **`Skills/QuickShot.cs`** (new, M2) — hitscan single-target shot. Range-gated to chebyshev 6 from caster + LOS-gated via `TileLineOfSight` so an arrow into a wall does no damage. Picks the nearest enemy within `TargetRadius=1` of the cursor cell so a near-miss click still connects. 12 dmg, 0 cost, 0.5s cd, glyph `'`.
- **`Skills/Volley.cs`** (new, Q) — 3×3 chebyshev-1 AOE at the cursor. Range 6, LOS-gated from caster to cursor (the volley as a whole; individual splash hits don't re-check LOS per target). 8 dmg per hit, 12 Focus, 1.0s cd, glyph `"`.
- **`Skills/Roll.cs`** (new, W) — disengage-dash mirroring Charge. Walks one tile at a time AWAY from the cursor up to 4 tiles; stops at the first wall, lands on the last walkable tile. Cursor-on-caster falls back to east. 0 dmg, 10 Focus, 4.0s cd, glyph `~`. Returns `Teleported = true` to `TryCastSkill` so GameScreen plays the dash visual lerp.
- **`Skills/Bandage.cs`** (new, E) — instant heal mirroring War Cry mechanically; cream/linen flash differentiates from War Cry's green. 25 HP, 25 Focus, 12.0s cd, glyph `+`.
- **`Skills/RainOfArrows.cs`** (new, R) — 5×5 chebyshev-2 AOE at the cursor. Range 8, **no LOS check** (arrows arc); the higher cost / longer cooldown is what gates the wall-skip. 18 dmg per hit, 35 Focus, 8.0s cd, glyph `:`.
- **`Classes/WalkerClassDefinition.cs`** — added `IReadOnlyList<string?> StartingSlotSkills` (length = `GameLoopController.SlotCount`). Each entry is a skill id resolvable against `Registries.Skills`, or null for an empty slot. Reaver / Hunter populated; Cipher / Speaker still all-null.
- **`UI/GameScreen.cs` + `Sim/TickRunner.cs`** — drop the per-class slot-wiring branches in favor of a generic loop reading `classDef.StartingSlotSkills`. Resolves the "hardcoded twice" item from prior open issues. GameScreen now reads `RenderSettings.StartingClassId` (defaults `"reaver"`) for the live-game class selection — flip to `"hunter"` and recompile to play Hunter; class-select UI is still deferred.
- **Tests** — `Skills/{QuickShot,Volley,Roll,Bandage,RainOfArrows}Tests.cs` cover damage / range / LOS / radius / on-top-of-caster fallbacks. `Classes/StartingSlotSkillsTests.cs` pins kit length, all-non-null-ids-resolve, and Hunter-kit-uses-Focus. Per the new CLAUDE.md tests-with-features rule.

### Health & Resource potions — drop, pickup, drink, HUD
- **`Items/Potions.cs`** (new) — two `ItemDefinition`s (`health_potion`, `resource_potion`, both `ItemSlot.None` glyph `!`) plus the per-potion tunables: `HealthPotionHealAmount = 40`, `ResourcePotionRestoreAmount = 30`, `DrinkCooldownSec = 0.5f`. Heal / restore amounts and glyph colors live here instead of growing `ItemDefinition` for every new consumable shape — when more variants land we promote them to a `ConsumableData` record.
- **`Entities/FloorItem.cs`** (new) — `Entity` subclass with `RenderLayer = 20` (between terrain `10` and creatures `50` so live actors draw on top). Holds an `ItemDefinition` reference; glyph + name forwarded from the definition.
- **`Inventory/Inventory.cs`** (new) — first-cut consumables-only inventory on `Player`. Tracks `HealthPotionCount` / `ResourcePotionCount`; `Add(ItemDefinition)` increments and `TryConsume(ItemDefinition)` decrements + reports false on empty stack. Full 32-slot bag + equipment slots (GDD §6) lands when equipment loot does.
- **`Items/LootDropper.cs`** (new) — pure-logic `RollDrop(enemy, rng, dropChance)` returning a `FloorItem?`. Lifted out of GameScreen so the roll is unit-testable + the sim harness can call the same code path when balance sweeps pull loot into scope.
- **`Core/GameLoopController.cs`** — controller now owns the `List<FloorItem>` and a `TryPickupFloorItems` step inside `Tick` that pulls every item on the player's tile into `Player.Inventory`. Shared list reference (mirroring the enemy pattern) means GameScreen's `_floorItemVisuals` reaps orphaned visuals after each tick.
- **`UI/GameScreen.cs`** — `LootDropChance = 0.08f` (≈ 1 drop per 12 kills). `OnEntityDied` calls `LootDropper.RollDrop`; on success a `SadEntity` visual is added to `_entityManager` and the FloorItem to the shared list. `LoadFloor` clears items + visuals on descent / death (potions don't persist between floors). New `1` / `2` keys in `ProcessKeyboard` invoke `TryDrinkHealthPotion` / `TryDrinkResourcePotion` (gated by per-potion `_hpPotionCooldown` / `_resourcePotionCooldown`); each drink fires a brief screen-flash via `SkillVfx` (HP green like War Cry, Resource warm orange) for the "drank something" cue. `DrawHud` now reads live counts from `_player.Inventory` so the `ConsumableSlot` placeholders show real `x{Count}`.
- **Tests** — `Items/InventoryTests.cs`, `Items/PotionPickupTests.cs` (drives `GameLoopController.Tick` directly with a player on top of a `FloorItem`), `Items/LootDropperTests.cs` (forced drop chance + RNG-branching coverage). Pickup test pattern mirrors the existing AI tests — no SadConsole dependency.

### Headless game-loop controller, xUnit suite, `tarpg-sim` CLI
- **`Core/GameLoopController.cs`** (new) — extracts the per-tick logic (movement, combat, AI, regen, slot cooldowns, threshold detection, FOV recompute, floor-item pickup) out of GameScreen.Update into a UI-free controller. Constructor takes `Player`, `List<Enemy>`, `Map`, `MovementController`, `CombatController`, `List<FloorItem>?`. Public `Tick(deltaSec, cellAspect, frozen, lastPlayerTile)`; caller checks `SteppedOnThreshold` / `PlayerDied` flags after each tick. Slot state (`_slotSkills` / `_slotCooldowns`) and the regen timer / accumulator now live here. `TryCastSkill` gates on cooldown + resource and returns a `CastResult` carrying `PreCastPosition` / `PostCastPosition` so the caller can choose to animate the snap (GameScreen) or leave the player at the snapped position immediately (sim).
- **`UI/GameScreen.cs`** — Update is now: dash-lerp visual → `_loop.Tick` → react to flags → tick UI effects → render. `TryActivateSlot` is a thin wrapper over `_loop.TryCastSkill` that handles the dash visual setup (rolling the player back to PreCast, starting the lerp). The behavior change vs. before: old enemies on the descent floor get one final AI tick before `Descend()` swaps the floor, and new floor enemies don't tick on the descend frame (was the inverse before). Imperceptible at 60Hz.
- **Explicit RNG seeding** — `GameScreen` now takes an optional `Random?` constructor arg; uses `_rng.Next()` for floor seeds and weighted enemy picks instead of `Environment.TickCount` / `Random.Shared`. `Program.cs` seeds from `Environment.TickCount`; tests / sim runners pass a fixed seed for reproducibility.
- **xUnit suite** (5 files in `src/Tarpg.Tests/`) — first real tests after the `UnitTest1.cs` stub: `MovementController` (arrival timing, wall slide), `CombatController` (range / cooldown gates), `MeleeChargerAi` + `RangedKiterAi` (FOV-symmetric aggro, kiting band, hitscan), `BspGenerator` (same-seed determinism, walkable entry / threshold / spawns, threshold reachable from entry, deeper-floor slot count). `Helpers/TestMaps.cs` builds open-floor + walls fixtures without going through the full BSP pipeline.
- **`Sim/`** in the main project (new) — `TickRunner` runs a single floor headless under an `ISimPilot` decision layer, accumulates kills / damage / HP min / skill uses / per-enemy-id kill counts in a `SimResult`. Ships `GreedySimPilot` (nearest-enemy chase, melee, fire AOE on dense clusters, walk to threshold when clear) — a "pressure-test" pilot, not optimal play.
- **`src/Tarpg.Sim/`** (new console project, `tarpg-sim` binary) — sweeps a (floor × seed) grid for one (zone, class, pilot) combo and writes per-run CSV (`seed, floor, outcome, ticks, sim_seconds, initial_enemies, enemies_killed, dmg_dealt, dmg_taken, hp_end, hp_min, skill_uses, kills_<id>...`) plus a per-floor aggregate footer (cleared%, died%, timeout%, HpEnd p50, HpMin p50, kills avg, time avg). Args: `--zone`, `--class`, `--floors a-b`, `--seeds n`, `--seed-base n`, `--pilot greedy`, `--out path`. `sim.bat` wrapper alongside `run.bat` / `test.bat`.
- **First sweep eyeballed**: `sim --floors 1-3 --seeds 5` returns 100% cleared at F1-F3 with HpEnd p50 ≈ 65 / 65 / 65, taking damage on every floor (HpMin drifts down with depth). Greedy pilot's not optimal so this is a soft baseline; real balance work waits for a second class.

### Skill VFX system — area highlights, screen shake, screen flash
- **`Skills/ISkillVfx.cs`** (new) — interface lives in `Tarpg.Skills` so skill behaviors don't pull a UI dependency for what's logically "side effects of the ability." Three primitives: `PlayAreaHighlight(tiles, color, lifeSec)`, `PlayScreenShake(intensityPx, durationSec)`, `PlayScreenFlash(color, durationSec)`. All fire-and-forget; the renderer ticks the resulting state independently.
- **`Skills/SkillContext.cs`** — added `ISkillVfx? Vfx`. Null in headless contexts; GameScreen plugs the concrete renderer in when activating skills.
- **`UI/Effects/SkillVfx.cs`** (new) — concrete implementation. Highlights re-tint the world surface's bg toward the highlight color (alpha-faded toward zero over `lifeSec`); screen shake stacks-by-max and feeds an additive pixel offset into `UpdateCamera`; screen flash paints a fullscreen translucent fill on a dedicated overlay child. `Tick(deltaSec)` advances timers, `Render()` repaints (after `DrawMap` so highlight tints aren't immediately overwritten), `GetShakeOffsetPx()` is sampled once per camera update, `Clear()` resets state on floor regen.
- **`UI/GameScreen.cs`** — new `_flashOverlay` child Console placed between the world and HUD layers (so flashes tint the playfield without dimming the bars). `_skillVfx` constructed with both surfaces. Wired through: ticked alongside HitFeedback / ClickIndicator, rendered after `DrawMap`, shake offset added to `_worldConsole.Position` in `UpdateCamera`, cleared on `LoadFloor`. `SkillContext.Vfx` plumbed in `TryActivateSlot`.
- **Per-skill effects** wired into each `Execute`:
  - **Cleave** — 8-tile chebyshev ring highlight (orange `(220,90,40)`, 0.25s) + 2px shake for 0.08s. The "where the swing landed" footprint reads at a glance.
  - **HeavyStrike** — 3×3 highlight at the click cell (orange, 0.2s) + 3px shake for 0.1s. Slightly punchier than Cleave per hit since it's single-target.
  - **Whirlwind** — full chebyshev-2 footprint (5×5 around caster, 25 cells, orange, 0.3s) + 5px shake for 0.15s. Big visible spin.
  - **WarCry** — fullscreen green flash `(80,220,120)` over 0.4s. Cleanly conveys "shouted, steeled, healed" without changing world cells.
  - **Charge** — no extra VFX; the lerp dash is its own feedback.

### Reaver skill kit — Heavy Strike, Charge, War Cry, Whirlwind
- **`Skills/HeavyStrike.cs`** (new, M2) — single-target on the cursor cell with a 1-tile chebyshev forgiveness radius (matches `ClickTargetRadius`). Range-gated to chebyshev 2 from the caster so M2 reads as a "wider, slower auto-attack." 25 dmg, 0 cost, 1.5s cooldown, glyph `!`.
- **`Skills/Charge.cs`** (new, W) — straight-line dash from the player toward `_lastCursorCell`, capped at 6 tiles. Walks the line one tile at a time; stops at the first wall (lands on the last walkable tile before it) or the first enemy (deals damage and parks the caster adjacent). 15 dmg, 15 Rage, 5.0s cooldown, glyph CP437 `►`.
- **`Skills/WarCry.cs`** (new, E) — instant heal, no buff system needed. 25 HP back on a 12s cooldown for 25 Rage. Glyph `*` (the burst-shout reading) — buff version of War Cry waits for the buff state machine.
- **`Skills/Whirlwind.cs`** (new, R) — single-pulse AOE around the player, chebyshev radius 2, 15 dmg per hit. 30 Rage, 6.0s cooldown, glyph `%`. Channel-while-held variant deferred until a channel state machine lands.
- **`UI/GameScreen.cs`**:
  - All 5 slots now wired in the ctor: `_slotSkills[M2..R] = HeavyStrike, Cleave, Charge, WarCry, Whirlwind` (Cleave stays on Q where the player learned it).
  - New `_lastCursorCell` field — updated on every `ProcessMouse` and used as `SkillContext.Target` so cursor-aimed skills hit where the player is looking. Self-AOE skills (Cleave, War Cry, Whirlwind) ignore Target and use `caster.Position`.
  - `TryActivateSlot` snapshots `_player.ContinuousPosition` before behavior execution. If the skill teleports the caster (Charge), the leftover walk target / combat target are cleared so the dash doesn't immediately get undone by stale state, AND the position change is converted into a brief animated lerp instead of a 1-frame snap. Tween fields `_dashStart` / `_dashEnd` / `_dashTotalSec` / `_dashRemainingSec` advance in `Update`; duration = `distance / DashTilesPerSec` (30 t/s, ~4× walk speed) so a 6-tile charge lands in 200ms while a 1-tile dash lands in ~33ms.
  - Dash state extends the existing `frozen` flag (alongside hit-stop), so player input + enemy AI both pause for the duration of the lerp. `LoadFloor` resets `_dashRemainingSec` so descent / death never inherit a stale animation.

### M2 skill slot + consumable scaffolding in the bottom HUD
- **`UI/GameScreen.cs`** — promoted `_equippedSkill` / `_skillCooldownSec` (scalars) to `SkillDefinition?[] _slotSkills` + `float[] _slotCooldowns` indexed by `SlotCount = 5`. Slot indices: `M2 = 0, Q = 1, W = 2, E = 3, R = 4`. `Q` is the only one wired today — Cleave from the registry. `TryActivateEquippedSkill()` became `TryActivateSlot(int)`. Update tick decrements every slot's cooldown; LoadFloor `Array.Clear`s the cooldown array.
- **Right-click → M2 skill activation**: `ProcessMouse` no longer force-moves on RMB-held. Instead it tracks a `_wasRightButtonDown` press-edge and calls `TryActivateSlot(SlotIndexM2)` once per click. The previous force-move utility is covered by `ClickTargetRadius`-far floor clicks; user feedback was that RMB-walk wasn't being used.
- **Q / W / E / R keys** all bind in `ProcessKeyboard` (pressed-event so holds don't auto-spam). W/E/R no-op until the skill content lands; M2 likewise.
- **`UI/Effects/StatusPanel.cs`** — `Render` now takes `(player, skillSlots, hpPotion?, resourcePotion?)`. New `ConsumableSlot` record (Keybind, Glyph, Count) and `DrawConsumableSlot` renderer at 4 wide × 5 tall, mirroring the skill-slot layout (keybind top, glyph middle, count bottom when > 0). Empty (Count = 0) draws as dim placeholder so the slot's role is visible before any potion exists. Skill-slot row now centers between the consumable slots so adding/removing potions doesn't drift the skill bar.
- **Layout (80 wide)**: HP orb 0–4, HP potion 5–8, 5 skill slots centered between cols 9 and 70, resource potion 71–74, resource orb 75–79. Skill slots: M2 / Q / W / E / R left-to-right.
- **`UI/GameScreen.DrawHud`** passes 5 SkillSlots and 2 placeholder ConsumableSlots (count = 0) to `_statusPanel.Render` each frame. Wired so when potion items + drop / pick-up logic land, the HUD just gets real `Count` values without further UI work.

### Bottom-bar HUD (HP orb, resource orb, skill slots) + camera-aware visible region
- **`UI/Effects/StatusPanel.cs`** (new) — owns rendering for the bottom-of-viewport panel. Layout in 80×5: HP orb at cols 0–4, resource orb at cols 75–79, four skill slots centered in between. Each orb is a 5×5 cell block whose vertical fill rises with current/max (rounded to row count, with a 1-row "sliver" floor when value > 0 but rounds to zero so the orb never goes pure-empty until you actually hit zero). Current value renders as text on the middle row over whatever fill color landed there — at low HP the number sits on dim red as a built-in danger cue. Resource orb color follows `WalkerClassDefinition.Resource`: Rage warm orange, Focus yellow, Insight blue, Echo purple. Skill slots show keybind / glyph / status (cooldown seconds, "low" rage, or "rdy"); empty slots dimmed. Public `record struct SkillSlot(Keybind, Skill?, CooldownRemaining)` is the data the panel consumes.
- **`Skills/SkillDefinition.cs`** — added `required char Glyph` so the bottom-bar slot has something to render. Cleave's glyph is `X` (slash-mark feel).
- **`UI/GameScreen.cs`**:
  - New `_bottomHudConsole` child Console (80×5) cell-positioned at viewport row 25, plus a `StatusPanel` wrapper. `DrawHud` now writes only the top row (zone+floor on the left, in-combat target on the right) and delegates HP/Rage/skill rendering to `_statusPanel.Render(_player, slots)`. Slot list is `[Q=Cleave, W=null, E=null, R=null]` for now; W/E/R fill in once more skills land.
  - `TopHudHeight = 1`, `BottomHudHeight = StatusPanel.PanelHeight = 5` constants. `UpdateCamera` now centers the player in the **visible region** (rows 1..24) instead of the full 30-row viewport — Y center = `TopHudHeight + visibleHeight/2 = 13`. Y-axis clamp loosened from `worldH - viewportH` to `worldH - (TopHudHeight + visibleHeight)`, so the player can walk to the very bottom of the world without getting covered by the bottom panel. X-axis math unchanged (no horizontal HUD overlap).
  - All three child consoles get the same `FontSize` in the ctor and on every zoom step.
- **HUD line rewrite**: top is just `" the Wolfwood F1                                              > wolf 18/18 "`. All player stats and skill state moved to the bottom panel where they read at-a-glance.

### Passive HP regen + Cleave skill on Q
- **`Skills/SkillDefinition.cs`** — `CooldownTicks` (int) → `CooldownSec` (float). Tick-based cooldowns were a pre-real-time-loop placeholder; seconds match how `Update(deltaSec)` already drives everything else.
- **`Skills/SkillContext.cs`** — added `IReadOnlyList<Entity> Hostiles`. AOE / cleave behaviors filter this by distance or direction; single-target behaviors will look up the entity at `Target` once those land.
- **`Skills/Cleave.cs`** — implemented `CleaveBehavior.Execute`: damages every chebyshev-adjacent live enemy (the 8 surrounding tiles) for `Damage = 10` (matches `CombatController.BaseDamage` so "full weapon damage" is honest). Set `CooldownSec = 1.0f`. Caster's own tile filtered so a stacked enemy on top of the player still wouldn't take this swing.
- **`UI/GameScreen.cs`**:
  - `_equippedSkill` field initialized from `Registries.Skills.Get("cleave")` in the ctor; ProcessKeyboard binds `Q` to `TryActivateEquippedSkill()`.
  - `TryActivateEquippedSkill` gates on `_skillCooldownSec > 0f` and `_player.Resource < def.Cost`, then builds a `SkillContext` (caster = player, target = player.Position, hostiles = current `_enemies` list cast to `Entity`) and runs the behavior. Skill TakeDamage calls fire HitFeedback's flash + damage-number on every hit enemy, so the visual cue is "many enemies pop at once."
  - `_combat.TryAttack(_player, deltaSec)` return value is now captured; on a successful hit, `GrantResourceOnHit()` adds `ResourceGainPerAutoAttackHit = 5` Rage (clamped to MaxResource). Cleave's own hits don't grant Rage so it can't infinite-combo.
  - `_skillCooldownSec` ticks down each Update including across hit-stop, matching ARPG cooldown convention.
- **HP regen**:
  - `OutOfCombatRegenDelaySec = 3.0f`, `RegenPerSec = 5.0f` constants at the top of `GameScreen`.
  - `_timeSinceLastDamage` resets to 0 on every player-side `OnEntityDamaged` event; advances each Update.
  - `TickHpRegen` uses a float `_regenAccumulator` so fractional HP carries between frames (avoids "regen 0 HP forever" rounding). Stops at MaxHealth.
  - `LoadFloor` resets cooldown / regen accumulator / regen timer; on death (`restoreFullHealth = true`) Resource is also reset to 0 so a fresh-start floor doesn't hand the player a free Cleave.
- **HUD line** rewritten to fit 80 cells with the new info: ` Reaver  HP 65/65  Rage 35/100  F1  > wolf 18/18  Q[Cleave ready]`. Trimmed the "the " article from class names; dropped zoom from the line (it's still bound on `+`/`-`/wheel, just not displayed). When the cooldown is active, the slot reads `Q[Cleave 0.7s]`; when underfunded, `Q[Cleave low]`.

### Spawn distribution rewrite — no more empty rooms / zero-enemy floors
- **`World/Generation/BspGenerator.cs`** — `ChooseEnemySpawns` was iterating rooms in BSP traversal order and taking 1–2 spawns per room until cap, which let a few early-iterated rooms eat all the slots while the rest of the floor sat empty. With small rooms close to entry, the per-tile distance filter could waste all 20 attempts and produce zero spawns despite slots being available.
- New algorithm: build a `candidateRooms` pool once (skipping entry / boss / rooms whose center is within `MinSpawnDistanceFromEntry` chebyshev of entry), shuffle, then **first pass** places one spawn per candidate (guarantees no empty room while slots remain), **second pass** distributes the leftover slots randomly until cap or saturation. The room-level distance filter replaces the per-tile one — if a room is too close to entry, it never enters the pool and never wastes attempts.
- Tuning: `MinSpawnDistanceFromEntry` 6 → **4** (still a buffer at spawn but not so wide it excludes most of the map at small entry-room sizes). `MaxEnemySlotsBase` 5 → **6** (F1 spawns 7 slots ≈ ~10–11 enemies after pup-pack expansion). `Cap` stays at 12.
- Side effect: F2+ should never feel sparse again, and the "TON of empty rooms" pattern goes away — every reachable far-enough room gets at least one occupant before extras pile up.

### BSP connectivity bugfix — corridors target rooms, not leaf bounds
- **`World/Generation/BspGenerator.cs`** — corridor endpoints used to be the *leaf bounding-box midpoints*. Because each room is carved with a 1-tile margin and a random offset *inside* its leaf, that midpoint frequently fell in the wall margin between the room and the leaf edge. When that happened, the corridor was carved through wall but never actually entered the room — the room shipped sealed. (User repro: F1 with the Reaver spawning in a fully enclosed 6×6 room.)
- Fix: each `BspNode` now stores a representative `Room` rect — the leaf's own room for leaves; inherited from the left subtree for internal nodes. `ConnectSubtree` carves L-corridors between `node.Left.Room` and `node.Right.Room` midpoints, both guaranteed to be inside floor tiles. The previous "deterministic without descending the tree" optimization comment is gone — the descent gets us the connectivity guarantee for free.
- Added `VerifyAllRoomsReachable` flood-fill check after corridor carving: BFS from `entry` over walkable tiles, confirm every room has at least one tile in the visited set. Throws with the seed in the message if any room is unreachable, so future regressions surface immediately instead of bricking a run.

### Enemy archetypes, hordes, stat baselines, per-floor scaling
- **`Enemies/EnemyDefinition.cs`** — codified per-enemy baselines: `MoveSpeed` (default 6f), `AttackCooldown` (default 0.8s), `PackSize` (default 1). Combat AIs now read these instead of using AI-class consts, so adding a faster/slower/swarmier flavor is purely a definition tweak.
- **`Movement/MovementController.cs`** — `TilesPerSecond` const → `DefaultTilesPerSecond = 8f` (player baseline) + a constructor parameter `tilesPerSecond` so enemies can have their own speed. Player still uses default via `new()`.
- **`Entities/Enemy.cs`** — added per-instance `Damage` field, initialized from `Definition.BaseDamage` by `Enemy.Create`. AIs now read `self.Damage` instead of `self.Definition.BaseDamage`, which lets `GameScreen.SpawnEnemy` apply per-floor damage scaling without mutating the shared definition singleton. `ResolveAi` switch updated to take the full `EnemyDefinition` (so AIs can read MoveSpeed / AttackCooldown).
- **`Enemies/Ai/SkirmisherAi.cs`** (new, `melee_skirmisher` tag) — bite-and-retreat melee. After a successful swing, picks a tile `RetreatDistanceTiles = 4.0f` away in the direction opposite the player and drifts there for `RetreatDurationSec = 0.6f` before re-engaging. Same FOV-symmetric aggro / 3s memory model as `MeleeChargerAi`. Movement uses the actor's `EnemyDefinition.MoveSpeed`.
- **`Enemies/Ai/RangedKiterAi.cs`** (new, `ranged_kiter` tag) — maintains a 4–6 tile band from the player and fires hitscan damage on cooldown when LOS holds. Backpedals when the player closes inside the band; advances when the player breaks LOS or pulls past the band. No projectile entity for v0 — the existing HitFeedback flash + damage number on the player is the "you got shot" cue.
- **`Enemies/Ai/MeleeChargerAi.cs`** — refactored to read movement speed and attack cooldown from the `EnemyDefinition` instead of holding them as AI consts. `AggroMemorySec` and `MeleeRange` stay as consts because they're behavioral, not stat-tier.
- **Five enemy definitions** in `Enemies/`:
  - `Wolf.cs` — weight bumped 4→5, explicit `MoveSpeed=6.0f`, `AttackCooldown=0.8f`. Backbone of every Wolfwood floor.
  - `WolfPup.cs` (new) — `'p'`, weight 3, HP 5, Dmg 1, speed 7, cooldown 0.6s, `PackSize=3`. Horde tier: each spawn slot expands to a 3-pup cluster.
  - `DireWolf.cs` — explicit `MoveSpeed=4.5f`, `AttackCooldown=1.0f`. Slow elite (was using AI default).
  - `Wolfshade.cs` (new) — `'s'`, weight 1, HP 22, Dmg 6, speed 6.5, cooldown 1.0s, `melee_skirmisher`.
  - `Howler.cs` (new) — `'h'`, weight 1, HP 14, Dmg 5, speed 5, cooldown 1.4s, `ranged_kiter`.
  Spawn weights total 11, so a typical floor sees ~45% wolf, ~27% pup-pack, ~9% each elite/skirmisher/ranged.
- **`UI/GameScreen.cs`** — new `SpawnPack(def, center)` fans out `def.PackSize` copies through expanding chebyshev rings around the BSP point (capped at `PackSpreadRadiusMax = 3`), skipping non-walkable / occupied tiles via a small `TryPlaceAt` helper. `SpawnEnemy` now calls `ApplyFloorScaling(enemy)` post-create which multiplies `MaxHealth` and `Damage` by `(1 + HpScalePerFloor·(floor−1))` and `(1 + DmgScalePerFloor·(floor−1))`. `HpScalePerFloor = 0.15f`, `DmgScalePerFloor = 0.10f`.
- **`World/Generation/BspGenerator.cs`** — `MaxEnemiesPerFloor = 8` const → `MaxEnemySlotsBase = 5` + `Cap = 12`. `Generate` computes `maxEnemySlots = min(Cap, Base + floor)` and threads it through `ChooseEnemySpawns`. The `_ = floor;` stub from descent v0 is gone — `floor` now actually steers generation.

### Input polish — drift, click pulse, shift-toggle
- **`Movement/MovementController.cs`** — `RetargetTo` no longer nulls `_finalTarget` when A* fails (target unreachable, OOB, or surrounded by walls). The drift toward the cursor stays active and `ResolveCollision` handles the wall slide. Click on a wall = walk into it and stop; click off the map = drift to the world edge. Combat chase paths benefit too: enemy-chase that can't path through still tries to drift after the target instead of giving up.
- **`UI/Effects/ClickIndicator.cs`** (new) — `+` glyph spawned at the click cell on left-button release, alpha-fades over `LifeSec = 0.25f` and despawns. Same SadEntity-on-EntityManager pattern as `HitFeedback`, attached to `_worldConsole` so the pulse pans with the camera.
- **`UI/GameScreen.cs`**:
  - Added `_clickIndicator` field initialized after `_hitFeedback`; ticked alongside hit feedback and cleared on `LoadFloor` so stale pulses don't carry across descent / death.
  - Added `_wasLeftButtonDown` field; `ProcessMouse` detects the press↔release edge manually (SadConsole exposes "held" but not the edge) and calls `_clickIndicator.Spawn` on release.
  - LMB-held + shift over empty floor branch now calls `_movement.Stop()` (instead of returning silently) — but only when there's no active combat target, so shift+click on an enemy followed by dragging across floor still preserves the attack-stand. This is what makes shift work as a clean walk↔stand toggle without having to release the mouse first.

### Second enemy type + weighted rolls + looser click-target
- **`Enemies/DireWolf.cs`** (new) — `EnemyDefinition` with id `"dire_wolf"`, glyph `'W'`, color cool desaturated purple, `BaseHealth=50`, `BaseDamage=9`, `AiTag="melee_charger"` (reuses the existing brain — different stats, not yet a different behavior tree), `ZoneIds=["wolfwood"]`, `RarityWeight=1`. Drops in via the registry's reflection auto-discovery — no central edits anywhere else.
- **`Enemies/EnemyDefinition.cs`** — clarified the `RarityWeight` comment to standard weighted-pick semantics (higher = more likely; 0 disables spawn rolls). Replaces the old "0 = infinitely common; higher = rarer" wording which was inverted from how almost every weighted-roll API works.
- **`Enemies/Wolf.cs`** — explicit `RarityWeight=4` (was relying on the default `=1`). Wolves now make up ~80% of Wolfwood spawn rolls vs ~20% for dire wolves.
- **`UI/GameScreen.cs`** — new `PickEnemyForZone()` does a standard two-pass weighted pick over `Registries.Enemies.All` filtered by `ZoneIds.Contains(_zone.Id)` and `RarityWeight > 0`. Both spawn loops (ctor + `LoadFloor`) now call `SpawnEnemy(PickEnemyForZone(), spawn)` instead of hardcoding `Wolf.Definition`. Adding a new wolfwood enemy is now a single new file with no further wiring.
- **`UI/GameScreen.cs`** — `FindLiveEnemyAt(Position)` now picks the nearest live enemy within `ClickTargetRadius = 1.5f` tiles of the click (Euclidean, against `enemy.ContinuousPosition`) instead of demanding exact-tile equality. ARPG attacks read as "swing in a direction" rather than precise picks against a moving target, so this matches player expectation when clicking into a clump of enemies. Right-click force-move is unchanged, so "walk past an enemy" remains an explicit gesture.

### Camera follow + larger maps
- **`UI/GameScreen.cs`** — three-layer rendering structure:
  - **GameScreen** (parent `Console` at viewport size): owns input + framing. Its own surface is unused — the children cover it.
  - **`_worldConsole`** (child `Console` at `WorldWidth × WorldHeight = 160 × 60`, `UsePixelPositioning = true`): renders the world cells and entities. Each frame `UpdateCamera` adjusts both `Surface.View` (integer cells) and `Position` (sub-cell pixel remainder) so the camera pans **smoothly between cells** instead of snapping a full cell at every tile boundary. View width is `viewportW + 1` so a buffer cell exists for the partial right/bottom render that appears when the camera is mid-pan.
  - **`_hudConsole`** (child `Console` at viewport×1): renders the HUD on a non-shifted overlay, so the status line stays glued to screen y=0 while the world slides underneath. Added after the world child so it draws on top.
- `UpdateCamera` math: `camPx = player.ContinuousPosition * FontSize - viewportHalfPx`, clamped to `[0, (mapPx - viewportPx)]`. `viewX/Y = floor(camPx / fontSize)`, `subPx = camPx - viewX * fontSize`. View origin gets the cell offset; `_worldConsole.Position = -subPx` (rounded) gets the pixel offset. Result is integer-pixel-precise sub-cell camera pans at any zoom level.
- `DrawMap` paints only cells inside `Surface.View` — viewport-bounded loop (~2400 SetGlyph calls per frame regardless of world size). Off-screen cells retain their last paint until they scroll back in.
- Window is sized to the **viewport** via `Game.Instance.ResizeWindow(_viewportCellsW, _viewportCellsH, fontSize, true)`, not the world surface, in both the ctor and `ChangeZoom`. Zoom syncs `FontSize` across all three consoles.
- Mouse routing: `_worldConsole.UseMouse = false` and `_hudConsole.UseMouse = false`, so clicks bubble up to GameScreen. `state.CellPosition` is in viewport-cell space; world tile = `state.CellPosition + _worldConsole.Surface.View.X/Y`. Entities use `UsePixelPositioning = true` and live on `_worldConsole`'s entity manager, so they pan with the world layer automatically.

### Multi-floor descent
- **`World/Generation/IZoneGenerator.cs`** — added `int floor` parameter to `Generate`. Reserved for per-floor difficulty / density / size scaling.
- **`World/Generation/BspGenerator.cs`** — accepts `floor` (currently unused, with a comment marking it as the wire-in point for scaling when it lands).
- **`UI/GameScreen.cs`** — added `_currentFloor` (1-indexed, starts at 1) and a `_zone` field cached from `Registries.Zones.Get("wolfwood")`. Refactored the old `RegenerateFloor` into `LoadFloor(restoreFullHealth, reason)`, with two callers: `Descend()` (increments `_currentFloor`, keeps HP) and `RegenerateAfterDeath()` (restores HP, leaves floor depth untouched until corpse-run lands).
- Tile-transition path in `Update` checks the new tile against `TileTypes.Threshold`; if the player just stepped onto one, calls `Descend()` (which itself recomputes FOV + camera at the new entry and resets `_lastPlayerTile`). Otherwise, refreshes FOV at the new tile. Same `Position != _lastPlayerTile` guard as before, just with one extra branch.
- HUD line now includes `{ZoneName} F{N}`, e.g., `"the Wolfwood F3"`. Seed log on each load includes the reason: `"[Tarpg] descent: loading the Wolfwood F2 (seed 12345)"`.

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

### 1. Skill / class balance pass
**Goal**: both kits (Reaver, Hunter) are in but tuned by gut. With two classes online we can compare across kits instead of adjusting in isolation. Drive `tarpg-sim --floors 1-15 --seeds 100` for both classes, compare per-floor cleared% / HpEnd / kill-time distributions, adjust constants, re-run until each skill has a clear identity (M2 = small/free, Q = primary, W = engage, E = bail-out, R = nuke) AND each class has a recognizable signature (Reaver = "win the melee," Hunter = "control the distance").

**Approach**: open `tarpg-sim` outputs side-by-side for both classes; tune `Skills/*.cs` numbers (and possibly enemy HP / damage scaling on the same axis). Likely also factor weapon damage out of skills into a `PlayerStats.cs` so weapon drops can scale skill damage uniformly without per-skill edits.

**Files to touch**: each `Skills/*.cs` (numbers), `Enemies/*.cs` if scaling shifts, possibly new `Entities/PlayerStats.cs`.

**Estimated effort**: 1 focused tuning session per class plus the weapon / level scaling abstraction (~half a session) if we go that route.

### 2. Live class-select UI
**Goal**: today, switching classes requires editing `RenderSettings.StartingClassId` and recompiling. Add a class-select screen at game start (or a debug F1/F2/F3/F4 cycle) so balance-tuning playthroughs aren't gated on a build-edit-relaunch loop. Cipher and Speaker stubs are still empty kits — class-select also surfaces "you can't pick these yet" naturally.

**Approach**: simple SadConsole screen rendered before the GameScreen; arrow-keys to highlight, enter to confirm. Skip when only one class has a non-empty `StartingSlotSkills` (single-option = no menu).

**Files to touch**: new `UI/ClassSelectScreen.cs`, `Program.cs` to chain screens, `RenderSettings.StartingClassId` becomes a default-only constant.

**Estimated effort**: 1 session.

### 3. Real corpse-run death
**Goal**: replace "regen on death, same floor, full HP" with corpse drop + XP loss + return-to-town. Town doesn't exist yet so this couples to the town milestone — but the death loop itself can land first as "die → reset to F1 with carried inventory minus a dropped item."

**Approach**: on death, spawn a corpse FloorItem at the death tile carrying the player's potions + (later) a dropped equipment item. Reset to F1 with full HP + empty inventory. Walking onto the corpse retrieves the items.

**Files to touch**: `UI/GameScreen.cs` (replace `RegenerateAfterDeath` semantics), new `Entities/Corpse.cs` (or reuse FloorItem), `Inventory/Inventory.cs` (drop / restore methods).

**Estimated effort**: 1 session for the corpse-only loop; town integration waits.

---

## Roadmap (ordered, but not strict)

### Soon — next 3–5 sessions
- [ ] Skill / class balance pass (above)
- [ ] Live class-select UI (above)
- [ ] Real corpse-run death (above)
- [ ] **Cellular automata roughening for Wolfwood floor edges** + flood-fill connectivity repair.
- [ ] **Distinct boss-anchor tile**: today the Threshold doubles as boss-spawn marker; once descent meets boss arenas they can't be the same.
- [ ] **Pack composition rolls**: today every BSP slot rolls a single weighted draw; deep-floor variety would benefit from "mixed packs" (e.g., 2 pups + 1 howler around the same anchor).
- [ ] **Per-zone weight curves vs flat depth scaling**: spawn weights could shift with depth (e.g., pups dominate F1–F3, dires + howlers ramp F5+). Currently weights are floor-invariant.

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
- **No player corpse on death** — when the player dies, the floor regenerates at full HP (corpse-run loop is item #3 in Up next). Enemy kills DO drop loot (HP / Resource potions at `LootDropChance = 0.08`).
- **Pickup is automatic on step** — walking onto a `FloorItem` tile vacuums it into inventory with no preview or confirm. Fine for potions today; later equipment drops may want a hold-Alt-to-highlight + click-to-pick-up flow per GDD §6.
- **Drink cooldown is per-potion-type, not global** — you can chain HP + Resource potions in the same frame since `_hpPotionCooldown` and `_resourcePotionCooldown` are independent. May want a unified "consumable busy" gate.
- **Click-to-target ignores FOV** — Shift+click on enemy still works at any distance regardless of visibility; click-to-walk targets and `FindLiveEnemyAt` consult position only. Now that the player can take damage, this lets you stand-attack unseen wolves through walls — worth tightening soon.
- **Enemy↔enemy collision doesn't exist** — multiple wolves chasing the player will overlap on the same tile when they reach melee range. Visual stacking only; doesn't break gameplay.
- **`EnemyDefinition.ZoneIds` is a string array, no compile-time check** — typoing a zone id silently makes an enemy un-spawnable. Worth a typed `ZoneRef` or registry-validation pass at startup once the zone count grows.
- **`EnemyDefinition.AiTag` is a string, no compile-time check** — same issue as ZoneIds. The `Enemy.ResolveAi` switch throws at first spawn if the tag is wrong, but it'd be nicer to fail at content-init time.
- **No projectile entities for ranged attacks yet** — the howler's hitscan damage gives no spatial cue for the attack itself; the player only knows damage happened from the HitFeedback flash + number. A short glyph trail along the LOS line (or a real projectile entity that travels) is the natural follow-up.
- **Stat scaling is single-zone, single-curve** — `HpScalePerFloor` / `DmgScalePerFloor` are global. Different zones (Drowned Hall, Hollow Court, etc.) will likely want their own curves, and "elite" enemies probably shouldn't scale linearly the same way commons do.
- **Pack spawn doesn't preserve formation across regen** — a pup pack that was a tight 3-cluster at spawn time can scatter as the AIs chase the player. Future "alpha + pack" mechanics may want the pack to stick together.
- **GreedySimPilot is pressure-testing, not optimal play** — it doesn't kite, doesn't skip enemies that aren't worth the HP cost, doesn't manage resource strategically. CSV outputs are useful as relative comparisons (this skill kit vs that one, Reaver vs Hunter) but absolute "win rate" numbers will undershoot what a competent player achieves. Worth landing a Hunter-flavored pilot variant (`KitingSimPilot` — keep distance, fire ranged) so Hunter-vs-Reaver sweeps test the kit, not the pilot's mismatch with the kit.
- **Class can't be switched in-game** — `RenderSettings.StartingClassId` is the only knob; flipping it requires a recompile. The class-select UI is item #2 in Up next.
- **Cleave-vs-auto-attack feels off at low enemy counts** — 10 dmg × N adjacent enemies on a 1.0s cooldown for 10 Rage vs the auto-attack's 10 dmg single-target on 0.8s for free. Math wins for Cleave only at 3+ adjacent enemies, and even there it doesn't *feel* like a payoff. Holding for the deferred balance pass — once a second class lands, drive `tarpg-sim` numbers to retune the whole kit holistically.
- **Threshold tile is the descent trigger AND the BSP boss-anchor marker** — these will conflict the moment boss arenas land (you'd descend onto the boss tile instead of fighting). Need a distinct boss-spawn tile (or a sidecar marker layer on Map) before the Wolf-Mother arena.
- **Camera tracks player center exactly** — no dead zone, lookahead, or velocity-aware easing. Sub-cell pixel smoothing is in place (no cell-snap pop), but rapid direction changes still translate 1:1 to camera moves; could add a small dead zone around the player if it ever feels twitchy.
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
    GameLoopController.cs             headless per-tick game logic (movement, combat, AI, regen, cooldowns, FOV, pickup)
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
    GameScreen.cs                     orchestrator — input, render, tick (top HUD + bottom panel children)
    Effects/
      HitFeedback.cs                  flash + damage numbers + kill burst + hit-stop
      ClickIndicator.cs               brief "+" pulse on left-button release
      StatusPanel.cs                  HP orb + resource orb + 4 skill slots, bottom bar overlay
      SkillVfx.cs                     area-highlight tints + screen shake + screen flash for skills
  Enemies/
    EnemyDefinition.cs                registry schema — id, name, stats, AiTag, ZoneIds, RarityWeight, MoveSpeed, AttackCooldown, PackSize
    Wolf.cs / WolfPup.cs / DireWolf.cs / Wolfshade.cs / Howler.cs    Wolfwood enemy definitions (5 tiers)
    Ai/
      IEnemyAi.cs                     strategy interface — Tick(self, player, map, dt, aspect)
      MeleeChargerAi.cs               Wolf / pup / dire wolf brain: FOV-aggro w/ 3s memory, A* chase, melee swing
      SkirmisherAi.cs                 Wolfshade brain: bite + retreat 4 tiles for 0.6s, re-engage
      RangedKiterAi.cs                Howler brain: kite at 4–6 tile band, hitscan damage on LOS
  Entities/
    Entity.cs                         base — ContinuousPosition, Health, IsDead, TakeDamage
    Player.cs                         walker class, level, resource, Inventory
    Enemy.cs                          wraps EnemyDefinition
    FloorItem.cs                      Entity subclass for items on the ground (zIndex 20)
  Inventory/
    Inventory.cs                      first-cut consumables-only — HP / Resource potion counts
  Items/                              Definition, Tier, Slot, Affix, ILegendaryEffect, Wolfbreaker, Potions, LootDropper
  Skills/                             Definition, ISkillBehavior, ISkillVfx
                                      Reaver:  Cleave, HeavyStrike, Charge, WarCry, Whirlwind
                                      Hunter:  QuickShot, Volley, Roll, Bandage, RainOfArrows
  Classes/                            WalkerClassDefinition, Reaver/Hunter/Cipher/Speaker
  Bosses/                             BossDefinition, WolfMother stub
  Modifiers/                          ModifierDefinition, IModifierBehavior, BurningFloor
  Sim/
    SimTypes.cs                       SimConfig / SimResult / SimOutcome
    ISimPilot.cs                      strategy interface for what the player does each tick
    GreedySimPilot.cs                 nearest-enemy melee pressure-test pilot
    TickRunner.cs                     single-floor headless runner — drives loop until cleared / died / timeout

src/Tarpg.Sim/                        tarpg-sim console runner — sweeps (floor x seed) grids, writes CSV
  Program.cs                          arg parser + sweep driver + aggregate summary

src/Tarpg.Tests/                      xUnit project (21+ tests across Movement / Combat / AI / BSP / Items / Sim)
  Helpers/TestMaps.cs                 small open-floor / walls fixtures for unit tests
  Movement/MovementControllerTests.cs
  Combat/CombatControllerTests.cs
  Enemies/Ai/MeleeChargerAiTests.cs   Enemies/Ai/RangedKiterAiTests.cs
  Items/InventoryTests.cs             Items/PotionPickupTests.cs   Items/LootDropperTests.cs
  Skills/{QuickShot,Volley,Roll,Bandage,RainOfArrows}Tests.cs   Hunter kit coverage
  Classes/StartingSlotSkillsTests.cs  per-class kit length / id-resolution / resource-type coverage
  Sim/WolfwoodBalanceTests.cs         smoke tests for the harness — fixed seeds, weak invariants
  World/Generation/BspGeneratorTests.cs

tarpg.sln, global.json, .gitignore, run.bat, test.bat, sim.bat, simi.bat (interactive sim wrapper)
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
