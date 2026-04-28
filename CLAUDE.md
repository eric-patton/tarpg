# TARPG

Solo Diablo-style ARPG with pure-ASCII terminal aesthetic. Stack: .NET 8 + SadConsole + RogueSharp.

## Always read first

**`docs/STATUS.md`** — canonical state-of-the-project. Built / queued / current tunables / ground rules / file map. Read it before any work. Update it after.

Other key docs (don't re-derive from these — STATUS already summarizes what's relevant):
- `docs/design/game-design.md` — the master GDD (15 sections, ~480 lines)
- `docs/research/arpg-design-research.md` — foundational ARPG research

## Run / build

- `run` (or `dotnet run --project src/Tarpg`) — launch the game
- `test` (or `dotnet test`) — runs tests (none written yet)
- `dotnet build tarpg.sln` — verify clean compile

## Critical project conventions

1. **Registry + reflection auto-discovery.** Adding new content (class, item, skill, enemy, boss, modifier, tile type) = drop one file with a `public static readonly XDefinition Foo = new() { Id = "...", ... }` field. `ContentInitializer` finds it via reflection. No central edits.
2. **Behavior via interfaces.** `ISkillBehavior`, `ILegendaryEffect`, `IModifierBehavior`. Definitions hold data + a strategy reference. New behavior types follow the same pattern.
3. **`Entity.ContinuousPosition` (System.Numerics.Vector2 in tile-space) is the movement source of truth.** Integer `Position` is derived via floor. Movement is per-frame velocity-based, not tile-stepped.
4. **`RenderSettings.UseSquareCells`** toggles between Option A (native IBM 8×16 + aspect correction) and Option B (Milazzo 12×12 square font). Program.cs and GameScreen both read from it — keep them consistent.

## After every meaningful work session

Update `docs/STATUS.md` per the **"How to update this doc"** section at its bottom:
- Move completed items from "Up next" / "Roadmap" into "Recently completed (newest first)"
- Refresh "Current state of the build" if behavior changed
- Add new "Open questions / known issues" if any surfaced
- Update the **Updated** date at the top
- Update the tunable-constants table if values changed
