# TARPG

> Solo single-player, Diablo-style ARPG that **plays** like a modern action-RPG and **looks** like a classic roguelike.

Real-time mouse-driven combat, click-to-move, click-to-attack, auto-attack melee, juicy hits — but rendered in pure ASCII / CP437 glyphs, no pixel art. The setting is mythic and dreamlike: a town built on a thin spot in reality where myths called Echoes leak through, and the player descends into infinite looping mythic zones to fight, loot, and grow.

**Working title**: TARPG (Terminal ARPG). Solo dev project, scope-locked to 5 zones / 5 named bosses / 4 classes / 8 NPCs.

---

## Status

This is a **scaffold-stage project** — the engine plumbing is in place, one map / one enemy / one playable class are wired up, and core gameplay primitives (movement, combat, FOV) work end-to-end. No procedural generation, no items dropping yet, no enemy AI, no town. The roadmap from here is in `docs/STATUS.md`.

**Currently runnable**:
- 80×30 scaffold map with a bordered room, an interior wall, a door, and a threshold tile.
- One playable class (Reaver) — `@` glyph, glides smoothly toward the cursor.
- Two stationary wolves — `w` glyph, take damage, die, are reaped.
- Auto-attack melee combat: click an enemy, the player walks into range and swings every 0.8s.
- Field of view / fog of war: ~10-tile radius reveals around the player; explored-but-not-currently-visible tiles render dim; walls and doors block sight; enemy sprites hide outside FOV.
- Mouse modes: left-click to move/attack, right-click held to force-move (ignore enemies), Shift+left-click on enemy to force-stand-attack.
- Zoom: 0.5× / 1× / 1.5× / 2× / 2.5× / 3× via `+` / `-` / scroll wheel; OS window resizes to match.
- A* pathfinding with axis-separated wall-slide collision and LOS-based waypoint optimization.

**See [`docs/STATUS.md`](docs/STATUS.md) for the canonical living roadmap** — what's built, what's queued, current tunables, and the file map. It's the first thing to read when picking up the project cold.

---

## Stack

- **.NET 8 LTS** (pinned via `global.json`)
- **[SadConsole](https://github.com/Thraka/SadConsole) 10.9** — terminal-style rendering on top of MonoGame
- **[MonoGame.Framework.DesktopGL](https://www.monogame.net/) 3.8.4.1** — windowing + input host
- **[RogueSharp](https://github.com/FaronBracy/RogueSharp) 4.2** — `IMap`, `Cell`, `PathFinder`, `FieldOfView`
- **xUnit** — test project (no tests written yet)

Bundled assets: a preprocessed Adam Milazzo 12×12 CP437 font (`src/Tarpg/Content/font_12x12.font` + `.png`) for square-cell rendering.

---

## Quick start

From the repo root (the `.bat` files all `cd /d "%~dp0"` so they work from any subdirectory too):

| Command | What it does |
|---|---|
| `run.bat` (or `dotnet run --project src/Tarpg`) | Launches the game window |
| `test.bat` (or `dotnet test`) | Runs the xUnit suite (no tests yet) |
| `dotnet build tarpg.sln` | Verifies clean compile |

You'll need the .NET 8 SDK installed (any 8.x feature release). `global.json` pins to 8.0.0 with `rollForward: latestFeature`.

---

## Controls

| Input | Behavior |
|---|---|
| **Left-click on enemy** | Attack-target — walk into melee range and auto-attack |
| **Left-click on floor** | Walk to that tile |
| **Hold Right-click** | Force-move toward cursor (ignores enemies) |
| **Shift + Left-click on enemy** | Force-stand-attack — no approach, only swings if already adjacent |
| **`+` / `-` / mouse wheel** | Zoom in/out through six levels |

---

## Project layout

```
docs/
  research/arpg-design-research.md    Foundational ARPG design research (~330 lines)
  design/game-design.md               Master GDD — 15 sections, ~480 lines
  STATUS.md                           Living roadmap — read this first
src/Tarpg/
  Program.cs                          SadConsole bootstrap; picks font from RenderSettings
  Content/                            Bundled fonts (.png + SadConsole .font metadata)
  Core/                               Position, Registry, ContentInitializer, RenderSettings
  World/                              Map (RogueSharp wrap), Tile, TileType definitions
  Movement/                           Continuous movement, A* fallback, grid LOS
  Combat/                             Auto-attack target tracking + cooldown
  Entities/                           Entity base, Player, Enemy
  Items/ Skills/ Classes/             Definition + behavior interfaces + examples
  Enemies/ Bosses/ Modifiers/         Definition + behavior interfaces + examples
  UI/GameScreen.cs                    Orchestrator — input, render, tick
src/Tarpg.Tests/                      xUnit project (placeholder)
tarpg.sln, global.json, run.bat, test.bat
```

---

## Architecture conventions

These are the four ground rules the codebase consistently follows. They're called out in detail in `CLAUDE.md`; in short:

1. **Registry + reflection auto-discovery.** Adding a new class / item / skill / enemy / boss / modifier / tile type means dropping one file with a `public static readonly XDefinition Foo = new() { Id = "...", ... }` field somewhere in the assembly. `Core/ContentInitializer.cs` finds it via reflection at startup. **No central registration list to edit.**
2. **Behavior via interfaces.** `ISkillBehavior`, `ILegendaryEffect`, `IModifierBehavior`. Definitions hold data + a strategy reference. New behavior types follow the same pattern.
3. **`Entity.ContinuousPosition` is the movement source of truth.** It's a `System.Numerics.Vector2` in tile-space. Integer `Position` is derived via floor. Movement is per-frame velocity-based, not tile-stepped. RogueSharp's grid still drives FOV / pathfinding.
4. **`RenderSettings.UseSquareCells`** toggles between the native IBM 8×16 font (with aspect correction) and the bundled Milazzo 12×12 square font. `Program.cs` (font loader) and `UI/GameScreen.cs` (cell sizing) both read from this single source so they always agree.

Tunable constants (movement speed, melee range, attack cooldown, FOV radius, dim factor, etc.) live as `static readonly` fields next to the system that uses them — see the table in `docs/STATUS.md` for the canonical list.

---

## Design docs

- **[`docs/design/game-design.md`](docs/design/game-design.md)** — the master GDD. 15 sections covering vision, the Echo setting, the five mythic zones, the Echo-pact companion mechanic, classes, descent loop, bosses, town, economy, difficulty tiers, scope, and implementation order.
- **[`docs/research/arpg-design-research.md`](docs/research/arpg-design-research.md)** — foundational research that informed the GDD. 13 design pillars distilled from Diablo 2/3/4, Path of Exile, Last Epoch, and roguelike literature, with 19 sources.

---

## Contributing

This is a solo dev project. There's no contribution flow set up. If you've stumbled across this and want to poke at it, fork freely.

---

## License

TBD. No license file yet — treat the contents as all-rights-reserved by the author until one lands.
