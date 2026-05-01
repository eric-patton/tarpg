# TARPG

Solo Diablo-style ARPG with pure-ASCII terminal aesthetic. Stack: .NET 8 + SadConsole + RogueSharp.

## Always read first

**`docs/STATUS.md`** — canonical state-of-the-project. Built / queued / current tunables / ground rules / file map. Read it before any work. Update it after.

Other key docs (don't re-derive from these — STATUS already summarizes what's relevant):
- `docs/design/game-design.md` — the master GDD (15 sections, ~480 lines)
- `docs/research/arpg-design-research.md` — foundational ARPG research

## Run / build

- `run` (or `dotnet run --project src/Tarpg`) — launch the game
- `test` (or `dotnet test`) — runs the xUnit suite (Movement, Combat, AIs, BSP, Items, Sim)
- `sim` (or `dotnet run --project src/Tarpg.Sim --`) — headless balance sweep
- `simi` — interactive sim (prompts for each option)
- `dotnet build tarpg.sln` — verify clean compile

## UI verification (autonomous visual loop)

The `scripts/` PowerShell harness lets you launch the game, screenshot it, and drive keyboard / mouse input — so any UI change can be self-verified without a user-in-the-loop test. **Use this for any UI work** (menus, HUD changes, new screens, in-game effects). Output goes to `/debug/` which is gitignored.

```
scripts/launch-debug.ps1   # build + start + wait for window + return PID
scripts/screenshot.ps1     # PrintWindow → debug/screenshot.png (works through occlusion)
scripts/sendkeys.ps1 KEY…  # keyboard input via keybd_event with proper scan codes
scripts/click.ps1 -CellX X -CellY Y [-Button right]  # mouse click at viewport-cell coords
scripts/kill-debug.ps1     # tear down by window title
```

Recommended iteration loop:
1. `launch-debug.ps1` (or `-SkipBuild` if you just rebuilt)
2. `screenshot.ps1` → use the Read tool on `debug/screenshot.png` to *see* the game
3. `sendkeys.ps1 down`, `sendkeys.ps1 enter`, `click.ps1 -CellX 40 -CellY 15`, etc. — drive input
4. `screenshot.ps1` again, look at the result
5. Repeat. `kill-debug.ps1` when done.

Gotchas (encoded in the scripts; documented here so future-you understands them when something looks wrong):
- **Click on the window first to nail focus** if a fresh keystroke doesn't seem to register. The harness calls `SetForegroundWindow` but the very first input after launch sometimes lands while focus is still settling.
- **Screenshots use `PrintWindow`, not `CopyFromScreen`.** The latter captures whatever pixels are on-screen at the window's reported position — if another window is in front you get the wrong picture. `PrintWindow` asks the game to render its own framebuffer regardless of z-order.
- **Keyboard input uses `keybd_event` with explicit scan codes from `MapVirtualKey`.** Passing scan code 0 works for arrow keys against MonoGame but silently fails for letters / Enter / Space. Friendly-name map in `sendkeys.ps1` covers up/down/left/right, enter, esc, space, tab, q-z, 0-9, F1-F4 — extend it for new keys.
- **Cell-coords for clicks** assume the bundled square 12×12 font at 1.0× zoom. If the player has zoomed via `+` / `-` / wheel, pass `-CellWidth` and `-CellHeight` scaled (e.g. 18 for 1.5× zoom).

When a screen is *purely a function of state* (like ClassSelectScreen), prefer a unit test over the visual loop — faster iteration, runs on CI. Use the visual loop for end-to-end "does this actually look right" verification, multi-frame animation checks, or anything that depends on render-pipeline behavior.

## Critical project conventions

1. **Registry + reflection auto-discovery.** Adding new content (class, item, skill, enemy, boss, modifier, tile type) = drop one file with a `public static readonly XDefinition Foo = new() { Id = "...", ... }` field. `ContentInitializer` finds it via reflection. No central edits.
2. **Behavior via interfaces.** `ISkillBehavior`, `ILegendaryEffect`, `IModifierBehavior`. Definitions hold data + a strategy reference. New behavior types follow the same pattern.
3. **`Entity.ContinuousPosition` (System.Numerics.Vector2 in tile-space) is the movement source of truth.** Integer `Position` is derived via floor. Movement is per-frame velocity-based, not tile-stepped.
4. **`RenderSettings.UseSquareCells`** toggles between Option A (native IBM 8×16 + aspect correction) and Option B (Milazzo 12×12 square font). Program.cs and GameScreen both read from it — keep them consistent.
5. **Every new feature ships with tests.** Non-negotiable. Pick the right level for what changed:
   - **Unit tests** (`src/Tarpg.Tests/`) — for any pure-logic addition: skill behaviors, AI archetypes, inventory rules, registry lookups, generation invariants. Drive the system directly (no SadConsole dependency); use `Tests/Helpers/TestMaps.cs` for fixture maps.
   - **Sim coverage** (`src/Tarpg/Sim/`, `src/Tarpg.Tests/Sim/`) — for systems-level changes that interact across movement / combat / AI / skills / items. Add a `WolfwoodBalanceTests`-style smoke test plus, when applicable, a sweep through `tarpg-sim` to confirm aggregate behavior.
   - **Integration tests via `GameLoopController`** — for cross-system behavior that doesn't need a window (pickup, descent, regen, etc.). Drive `Tick` directly; see `Items/PotionPickupTests.cs`.
   - **UI / SadConsole-coupled code** — for visual / interactive verification, use the `scripts/` PowerShell harness (see "UI verification" above) to launch + screenshot + drive input. Flag what's still not testable in the code comment + open-issues section of STATUS.md. Don't pretend it has tests when it doesn't. If the same logic can be split into a UI-free helper (the `LootDropper.RollDrop` pattern), do that and test the helper.

   When you add a new content type or system, also add the test pattern for it so future content of the same type just slots in. Don't ship a feature with a "tests TBD" tail.

## After every meaningful work session

Update `docs/STATUS.md` per the **"How to update this doc"** section at its bottom:
- Move completed items from "Up next" / "Roadmap" into "Recently completed (newest first)"
- Refresh "Current state of the build" if behavior changed
- Add new "Open questions / known issues" if any surfaced
- Update the **Updated** date at the top
- Update the tunable-constants table if values changed
