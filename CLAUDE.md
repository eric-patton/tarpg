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

## Critical project conventions

1. **Registry + reflection auto-discovery.** Adding new content (class, item, skill, enemy, boss, modifier, tile type) = drop one file with a `public static readonly XDefinition Foo = new() { Id = "...", ... }` field. `ContentInitializer` finds it via reflection. No central edits.
2. **Behavior via interfaces.** `ISkillBehavior`, `ILegendaryEffect`, `IModifierBehavior`. Definitions hold data + a strategy reference. New behavior types follow the same pattern.
3. **`Entity.ContinuousPosition` (System.Numerics.Vector2 in tile-space) is the movement source of truth.** Integer `Position` is derived via floor. Movement is per-frame velocity-based, not tile-stepped.
4. **`RenderSettings.UseSquareCells`** toggles between Option A (native IBM 8×16 + aspect correction) and Option B (Milazzo 12×12 square font). Program.cs and GameScreen both read from it — keep them consistent.
5. **Every new feature ships with tests.** Non-negotiable. Pick the right level for what changed:
   - **Unit tests** (`src/Tarpg.Tests/`) — for any pure-logic addition: skill behaviors, AI archetypes, inventory rules, registry lookups, generation invariants. Drive the system directly (no SadConsole dependency); use `Tests/Helpers/TestMaps.cs` for fixture maps.
   - **Sim coverage** (`src/Tarpg/Sim/`, `src/Tarpg.Tests/Sim/`) — for systems-level changes that interact across movement / combat / AI / skills / items. Add a `WolfwoodBalanceTests`-style smoke test plus, when applicable, a sweep through `tarpg-sim` to confirm aggregate behavior.
   - **Integration tests via `GameLoopController`** — for cross-system behavior that doesn't need a window (pickup, descent, regen, etc.). Drive `Tick` directly; see `Items/PotionPickupTests.cs`.
   - **UI / SadConsole-coupled code** — flag what's not testable in the code comment + open-issues section of STATUS.md. Don't pretend it has tests when it doesn't. If the same logic can be split into a UI-free helper (the `LootDropper.RollDrop` pattern), do that and test the helper.

   When you add a new content type or system, also add the test pattern for it so future content of the same type just slots in. Don't ship a feature with a "tests TBD" tail.

## After every meaningful work session

Update `docs/STATUS.md` per the **"How to update this doc"** section at its bottom:
- Move completed items from "Up next" / "Roadmap" into "Recently completed (newest first)"
- Refresh "Current state of the build" if behavior changed
- Add new "Open questions / known issues" if any surfaced
- Update the **Updated** date at the top
- Update the tunable-constants table if values changed
