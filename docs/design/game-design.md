# TARPG — Game Design Document

> **Working title**: TARPG (Terminal ARPG)
> **Genre**: Solo single-player ARPG (Diablo-likes), terminal-aesthetic
> **Engine**: SadConsole (.NET, MonoGame host), pure ASCII / CP437
> **Status**: Design locked at the brainstorm level. Implementation details follow during prototyping.

---

## 1. Vision & Pillars

A pure-ASCII terminal game that **plays** like a modern Diablo — real-time combat, mouse-driven Diablo controls, click-to-move/hold, juicy hits, click-to-attack — but **looks** like a classic roguelike: glyphs and color, no pixel art. The setting is mythic, dreamlike, archetypal: a town built on a thin spot in reality where myths called Echoes leak through, and the player descends into infinite, looping mythic zones to fight, loot, and grow.

### Design Pillars

1. **The classic ARPG loop, intact.** Fight enemies → get loot → grow stronger → fight harder enemies. Tight, satisfying, every kill a slot-machine pull.
2. **Mythic, not generic.** Every glyph carries identity. `W` is *the* Wolf. `K` is *the* King. Recurring named bosses become shared language.
3. **Pure ASCII, fully juiced.** The terminal aesthetic is a feature, not a constraint. Color, hit-flashes, screen shake, and audio do the work that pixel art usually does.
4. **Endgame-first.** Campaign is a tutorial; the loop is the product. Players should be having a great time on their 50th hour.
5. **Solo-dev scope.** Tight content surface (5 zones, 5 bosses, 4 classes, 8 NPCs). Add depth through systems, not authoring.

### What Makes This Game Distinctive

- Pure-ASCII presentation paired with **real-time mouse-driven Diablo controls** is a niche almost no one occupies.
- The **Echo-pact** companion system — defeating a named myth-boss forces a choice between its signature loot and binding it as kin — is unique to our game.
- **Themed mythic zones** that loop infinitely with scaling difficulty + per-floor PoE-style modifiers blend D2/D3/PoE strengths cleanly.

---

## 2. The Setting: The Echoes

The game world is built around one central conceit:

> **The Echoes are myths given form.** They emerge from a thin spot in reality where dreams, dead timelines, and mythic archetypes leak through. The town of **Walker's Hold** sits at this thin spot. The townsfolk who can step into the unreal are called **Threshold-walkers**.

### Tone and Texture

- **Vibe**: Elden Ring meets classic roguelike. Mythic, fragmented, melancholic. Lore is environmental — delivered through item flavor text, NPC asides, and lore-scraps the player finds in zones.
- **No big inciting event.** The Echoes have always been here. The town has always been here. People have always walked into them. The game starts with the player being just another walker.
- **Familiar archetypal beats.** The Wolf King, the Drowned Bride, the Hollow Court — names that *feel* like they should already mean something. Players drop into the world feeling the weight without us authoring an epic.

### The Five Mythic Zones (Themes of the Descent)

| Zone | Floors (loop 1) | Vibe | Boss |
|---|---|---|---|
| **The Wolfwood** | 1 – 7 | Old growth forest, twilight, hunger, savagery | The Wolf-Mother |
| **The Drowned Hall** | 8 – 14 | Sunken cathedral, rising water, choral dread | The Drowned Bride |
| **The Hollow Court** | 15 – 21 | Ruined throne room, gilded decay, false grandeur | The Hollow King |
| **The Forgotten Fair** | 22 – 28 | Carnival in fog, masks, music boxes, things that smile | The Forgotten Knight |
| **The Last Room?** | 29 – 35 | Unknowable. Different every loop. The end of stories | The Last Hour |

After floor 35, the descent loops: floors 36–42 are **Wolfwood II**, then Drowned II, etc. Each loop is a difficulty tier (see §11).

### Walker's Hold (the town)

A small fortified village (~25×15 tiles) clustered around the **Threshold** — a crack in the world. Eight named NPCs, each functional, each with personality.

```
##############################
#............................#
#..####.....##....####.......#
#..#  #.....##....#  #.......#
#..#E #...........#R #..#####.
#..####...........####..#  >.#  > = Threshold
#........#####.........#  ..#
#........#   #..@......#  ..#
#........# T #.........#####.
#..####..#####...####........#
#..#A #..........#  #........#
#..#  #..........#M #........#
#..####...........####.......#
#............................#
##############################
```

**The Eight:**

| NPC | Role | Flavor |
|---|---|---|
| **The Eldest** | Lore-keeper, free respec at her shrine | Oldest living walker. Blind. Sees what others can't. |
| **The Reader** | Identifies Rare+ items at the Reading Stone | Speaks in fragments. Hears the items' stories. |
| **The Steward** | Stash master, character book-keeping | Knows every walker's name, living and dead. |
| **The Smith** | Repairs gear; reforges magic affixes | Lost her arm to the Drowned. Doesn't talk about it. |
| **The Apothecary** | Brews potions and Scrolls of Reading | Half-walker, half-cook. Best stew in the Hold. |
| **The Innkeeper** | Rest, save, gossip | Has buried more walkers than he's served drinks. |
| **The Marshal** | Manages Echo-pact bindings; tracks pacts | Walked once. Came back wrong. Stayed. |
| **The Sigil-Maker** | Sockets sigils into gear | Quiet. Carves. Listens. |

---

## 3. The Player: A Threshold-Walker

You are one of the working walkers of the Hold. Not the chosen one. Not a prophesied hero. Just one of the dozens who can cross.

- **Narrative density: low.** The world has the mystery, not your past.
- **You start at level 1** in the Hold, kit appropriate to your tradition.
- **Found family:** other walkers in town are your peers. Some retired. Some legendary. Some who walked in last week.
- **Death is recoverable.** The Threshold pulls you back when you fall — at a cost (see §9).

---

## 4. The Four Traditions (Classes)

Fixed classes; **easy respec** at the Eldest's shrine. Skill swap on the fly. Mistakes are cheap; experimentation is encouraged.

Each tradition has:
- A distinct **glyph color** (so you can tell at a glance who you are)
- A unique **resource** (so each class *feels* different to drive)
- A **role** in the classic ARPG quartet
- A **theme** rooted in the Echoes setting

### `@` THE REAVER — *red*
> *"If the Echoes can bleed, they can be killed."*
- **Resource:** Rage (built by hitting, spent on big skills)
- **Role:** Aggressive melee striker
- **Style:** Dual-wield, momentum-based, no shields, in-your-face
- **Sample skills:** Cleave, Bloodtide, Sunder, Lunge, Whirl
- **Item theme:** Two-handers, dual blades, savagery glyphs, light armor

### `@` THE HUNTER — *green*
> *"Pin the myth before it can speak."*
- **Resource:** Focus (regenerates over time, big shots cost more)
- **Role:** Ranged tracker
- **Style:** Kiting, bows, traps, mobility, optional spirit-companion
- **Sample skills:** Aimed Shot, Snare Trap, Fox Step, Volley, Pin
- **Item theme:** Bows, throwing knives, leathers, hunter's charms

### `@` THE CIPHER — *violet/white*
> *"The world is a sentence. I edit."*
- **Resource:** Insight (regenerates between casts, glass-cannon DPS)
- **Role:** Reality-magic caster
- **Style:** AOE, space-bending, light/gravity manipulation, blink mobility
- **Sample skills:** Unwrite, Lattice, Refraction, Glass Flame, Pull
- **Item theme:** Tomes, robes, focus crystals, written charms

### `@` THE SPEAKER — *deep blue / cyan*
> *"The dead remember everything. They will tell me what I need."*
- **Resource:** Echo (gained per kill, spent on summons & rituals)
- **Role:** Echomancer / summoner
- **Style:** Pet-driven, debuffs, ritual buffs, weak in solo combat
- **Sample skills:** Call the Hound, Bindword, Echo of the King, Hollow Whisper
- **Item theme:** Idols, ritual blades, totems, bone charms

> *Detailed skill lists per class are deferred to prototyping. Each class needs ~10 skills at launch (a few in each "school" within the tradition). Skill design will iterate against playtesting, not pre-spec.*

---

## 5. Combat & Controls

### Real-time, mouse-driven, grid-logic-under-the-hood

- **Logical layer:** the world is a grid. Pathfinding, FOV, AOE, collision all operate on tiles.
- **Visual layer:** TBD during prototyping — either step-per-tile movement (snappy, simple) or interpolated movement between tiles (smoother, more work). This is a feel knob, not an architectural decision.
- **Input model:** click to move; click-and-hold to keep walking toward cursor; click on enemy to walk-to-attack-range and engage; right-click to fire skill at cursor; number keys (or hotbar slots) to swap active skill.

### Combat Juice (the make-or-break)

Pure-ASCII can absolutely sell weight if we lean on:
- **Hit-stop / hit-pause.** ~60–100ms freeze on heavy hits. Sells impact more than any visual.
- **Glyph flashes.** Struck enemy whitens for one frame.
- **Screen shake.** Tiny on small hits, more on big hits, restraint always.
- **Color pulses.** Critical hits saturate red. Heavy hits flash yellow. Boss hits desaturate the whole screen for a beat.
- **Particles.** Radial spray of `*`, `'`, `,` glyphs on heavy hits and kills.
- **Floating damage numbers.** Optional, toggleable. Default on.
- **Audio.** Distinct SFX per hit type (slash / crush / pierce / magic / crit). Sound conveys impact more than visuals when you're working in glyphs.
- **Silence.** Between waves and during boss buildups. Silence is its own juice.

### Default Controls (subject to iteration)

| Action | Input |
|---|---|
| Move | Left-click or hold |
| Attack basic | Click on enemy / hold near enemy |
| Skill 1–4 | Right-click (active skill) / number keys to swap |
| Quaff potion | Q |
| Open inventory | I |
| Open character / passives | C |
| Map | Tab |
| Pickup | Walk over item / hold Alt to highlight items |
| Camera center | Space |

---

## 6. Loot

### Tier System (D3-style)

| Tier | Color | Notes |
|---|---|---|
| Normal | white | Vendor trash, occasional starter gear |
| Magic | blue | 1–2 random affixes; **shows stats instantly** |
| Rare | yellow | 3–6 random affixes; **must be ID'd** |
| Legendary | orange | Named, **transforms a skill**; must be ID'd |
| Set | green | Named, gives **set bonuses** at 2/4 pieces; must be ID'd |

### Item Naming Conventions

- **Magic items** procedurally named: "Sharp Bow of the Hunt", "Reinforced Hood of Light Step"
- **Rare items** procedurally named with two affixes: "Worldcleaver Dawnbringer"
- **Legendary items** named after Echoes / mythic figures: "Wolfbreaker", "the Drowned Vow", "Crown of the Hollow King"
- **Set items** named for a complete myth-set: "the Wolf-Mother's Wedding" (4-piece), "the Hollow Court Regalia" (4-piece)

### Identification

- Magic items show stats on drop.
- Rare+ items appear as `?? echo-shrouded ??` until read.
- Read at the **Reading Stone** in town (free) or in-dungeon with a **Scroll of Reading** (purchased from the Apothecary).
- The Reader's flavor text on each ID'd item is part of the lore experience.

### Sample Legendary Effects (deferred)

> Detailed Legendary design is deferred to prototyping, but the *pattern* is: each Legendary either transforms a skill ("Your Volley becomes a single super-arrow that pierces") or adds a build-defining passive ("You gain Rage on getting hit"). Not just bigger numbers.

### Loot Sources

- Mob drops (random, weighted by class relevance and floor difficulty)
- Boss drops (named bosses have a curated drop pool — their signature gear)
- Lore-quest rewards (bespoke Legendaries unlocked by collecting Echo lore, see §10)
- Vendor stock (Smith / Apothecary; rotates per delve)

---

## 7. The Descent

### Structure

The dungeon is a **single, infinite, looping descent** through five themed mythic zones (see §2 for zone names and themes).

- **Loop 1:** floors 1–35 (one pass through all five zones)
- **Loop 2 (Echo II):** floors 36–70, all zones harder, scaled stats and modifier counts
- **Loop 3 (Echo III):** floors 71–105
- **... and so on, forever.**

Each loop is a **difficulty tier**. Loot quality scales with tier.

### Per-Floor Modifiers

Each floor rolls **0–3 random modifiers** that change the encounter. Examples:
- "Echoes deal +30% fire damage"
- "Healing reduced by 50%"
- "Echoes are invisible until adjacent"
- "Drops are doubled"
- "The floor is always dim (FOV reduced)"
- "Boss appears at half health but enraged"

More modifiers = more loot. Players self-pace by choosing how stacked a floor they're willing to push.

> Target mod count for launch: ~15 distinct modifiers covers the variety surface. Add more post-launch.

### Procedural Generation

- **Layout:** Hybrid BSP + cellular automata. BSP for macro structure (rooms, corridors), cellular automata for organic details and zone-specific texture (the Wolfwood is more cave-like; the Hollow Court is more rectangular).
- **Anchor points:** every zone has at least one fixed encounter (the boss room). Some zones have hand-tuned vault rooms that proc into procgen layouts (~10% chance per floor).
- **Themed monsters:** each zone has its own monster vocabulary (Wolfwood: `w` wolves, `s` strays, `H` huntsmen; Drowned Hall: `f` floaters, `d` drowned, `s` swimmers).

### Traversal

- **First pass:** must descend from the top of each unlocked zone.
- **Threshold-ritual fast travel:** after clearing a zone, the threshold can drop you in at any floor of that zone you've previously reached.
- **No mid-delve teleport** within a delve — once you commit, you push or die.
- **Death** returns you to the Hold; corpse persists until you reclaim or it expires (see §9).

### Pinnacle Bosses

The five named Echoes recur at every loop. They scale with the loop tier:
- **Loop 1:** vanilla mechanics, the "introductory" version.
- **Loop 2+:** new mechanics layer in. The Wolf-Mother gets a second phase. The Drowned Bride starts spawning ghost-children.
- **Loop 5+:** full mechanics, signature drops at maximum quality.

> Detailed boss mechanics are deferred to prototyping. Each boss needs a unique signature (Wolf-Mother = pack mechanic / leap; Drowned = drowning gauge / waves of water; Hollow King = throne summons; Forgotten Knight = duel + masks; Last Hour = phase-shifting).

---

## 8. Companions: The Echo-Pact

The signature mechanic of TARPG. Every time the player defeats a named Echo, they choose:

> **Take the loot — OR — bind the Echo as kin (one slot).**

### Mechanics

- **One Echo bound at a time.** Swapping breaks the prior pact (their gear and progression banked at the Marshal).
- **Bound Echoes follow you in delves.** They have their own AI, levels alongside you, contribute distinct passives.
- **Each named Echo plays differently as a companion.** The Wolf-Mother's Hound is an aggressive flanker. The Drowned Bride's Veil is an AOE debuffer. The Hollow King's Crown is a buffer. Etc.
- **The Marshal in town manages bindings.** Swap, dismiss, level reports.
- **Echo loot is forfeit when bound.** Hard tradeoff: you cannot have *both* the Wolfbreaker AND the Wolf-Mother's Hound from the same kill. Subsequent re-kills (later loops) give a second chance.

### Why this works for our game

- Adds a roleplay-flavored choice to every boss kill — "is this the Echo I want at my side?"
- Gives every boss kill a permanent footprint (loot OR companion, never zero).
- Adds a layer of build identity beyond class — your bound Echo says something about how you play.
- Costs less to design than a full hireling system (each bound Echo is essentially one boss you've already designed, repurposed as an ally).

---

## 9. Death & Persistence

### On Death

The Threshold pulls you back when you fall.

- Lose **10% XP toward current level**
- Drop **all carried gold**
- Drop **one held item** (random from inventory bag — equipped slots are safe)
- Wake at the Threshold in town
- Your **corpse persists** in the dungeon for a limited time / number of delves; reclaim by walking to it

**Equipment, stash, bound Echo: all safe.**

### Save System (default)

- **Auto-save** on entering town, leveling, completing major milestones, and at fixed intervals during play.
- **Multiple character slots** — players can run a Reaver and a Cipher simultaneously.
- **No save-anywhere mid-delve.** Force the player to engage with the death system instead of save-scumming.
- **Stash is shared** across characters of the same player profile.

> No hardcore / permadeath mode at launch. May add post-launch as an optional toggle.

---

## 10. Quests & Lore

A **light** quest layer. Two channels:

### Eldest's Charges (milestone goals)

The Eldest issues short, mechanical charges that anchor early progression and give players "next thing to do" hooks:
- "Reach Drowned 1."
- "Survive a floor with three modifiers."
- "Defeat the Wolf-Mother on Echo II."
- "Bind your first Echo."

Reward: gold, essence, sometimes a Sigil. Mostly: a sense of progress.

### Echo Lore-Quests (the bespoke loot path)

Each named Echo has **lore scraps** scattered as item drops in their zone. Collect all (4 per Echo) to unlock:
- The Echo's **backstory** (a few paragraphs of lore at the Reading Stone)
- A **bespoke Legendary** unique to the Echo (e.g., the Wolf-Mother's Lullaby — the bow she used to hush the wood)
- A **flavor reaction** from a town NPC (the Eldest reacts when you find the Hollow King's lore; the Smith when you find the Drowned Bride's)

Five Echoes × four lore scraps × five loops to find them all = built-in long-tail content.

---

## 11. Progression & Difficulty

### Player Leveling

- Standard XP curve, steeper late-game so early levels reward fast and late levels feel earned.
- Suggested cap: **level 50** for vanilla content; paragon-style infinite progression beyond (small drips per level above 50: +1 stat point, etc.). Confirm during balance phase.
- Skill points awarded per level; passives unlocked at thresholds.

### Build Layers

Three axes of expression:
1. **Class** (locked at character creation, but free respec)
2. **Active skill loadout** (4 active slots, swappable on the fly)
3. **Passives + Legendary effects + Set bonuses + bound Echo + Sigils**

### Difficulty Tiers (Loops)

| Loop | Tier | Difficulty | Loot quality |
|---|---|---|---|
| 1 | Echo I | Tutorial / story | Up to mid-Magic |
| 2 | Echo II | The "real" game starts | Up to Rare |
| 3 | Echo III | Endgame entry | First Legendaries common |
| 4 | Echo IV | Endgame mid | Set pieces start |
| 5+ | Echo V+ | Endgame deep | Best rolls, perfect Legendaries |

> Concrete numerical scaling deferred to balance pass.

---

## 12. Audio / Visual Direction

### Color Palette

- **Restrained, atmospheric.** Black backgrounds. Most glyphs in muted colors.
- **Reserve high-saturation colors** for moments that matter: bright red for blood/danger, bright gold for treasure, bright cyan for magic.
- **Per-zone signatures** — each of the five zones has a color identity:
  - Wolfwood: greens, browns, blood-red
  - Drowned Hall: pale blues, silvers, drowned-greens
  - Hollow Court: gold, dust, ash
  - Forgotten Fair: foggy whites, candy-pinks, mask-black
  - Last Room?: shifts. Sometimes pure white-on-black. Sometimes inverted.

### Audio Vision

- **Per-zone ambient loops.** Drone-heavy, sparse. Each zone has its own mood.
- **Distinct SFX per glyph type** — every monster letter has its own attack and death sound.
- **Hit feedback layered audio** — different sound for slash vs crush vs pierce vs magic, plus a crit overlay.
- **Silence is sacred.** Boss buildups go silent. The town has *very* quiet ambience. Combat is loud against quiet, not loud against louder.

> No specific composer / palette defined; reference the research doc on Diablo 1's score philosophy.

---

## 13. v1 Launch Scope

The smallest content surface that ships a full ARPG experience.

| Surface | Launch target |
|---|---|
| Classes | 4 (Reaver / Hunter / Cipher / Speaker) |
| Skills per class | ~10 (~40 total) |
| Zones | 5 (one for each theme) |
| Named bosses | 5 (one per zone) |
| Per-zone monster types | ~6–8 (~30–40 total) |
| Town NPCs | 8, all named, all functional |
| Floor modifiers | ~15 distinct |
| Legendaries | ~30 (rough — enough for ~6–8 per class to support build variety) |
| Sets | 5 (one per Echo theme) |
| Bound Echo companions | 5 (one per named boss) |
| Lore scraps | 20 (4 per Echo × 5 Echoes) |
| Eldest's charges | ~15–20 |
| Per-NPC arcs | 1–2 each (~12–16 total small arcs) |

This is the **floor**, not the ceiling. Post-launch content adds zones, classes, bosses without changing systems.

---

## 14. Deferred Decisions (for prototyping)

The brainstorm intentionally did **not** lock in:

- **Specific skill lists per class.** Iterate against playtesting; ten skills can change a hundred times.
- **Specific Legendary effects.** Design these against actual play — what feels good on the Reaver vs the Cipher.
- **Specific monster bestiary per zone.** Rough glyph alphabet outlined; specific stats/mechanics during prototyping.
- **Visual movement model** (step vs interpolated). A feel knob to tune during the first playable build.
- **Concrete numerical balance.** XP curve, damage scaling, stat ranges, modifier weights — all balance-pass work, not design.
- **Specific NPC dialogue.** Personalities are sketched in §2; lines come during writing.
- **UI layout details.** Inventory, skill bar, character sheet — wireframe during scaffolding.
- **Specific procgen tuning** per zone (room sizes, corridor styles, density).

These are all known unknowns. They get answered with prototypes, not specs.

---

## 15. Implementation Plan (high-level)

A rough order of operations once we start scaffolding. **Not** a sprint plan — just sequence:

1. **SadConsole scaffold** — empty project with a window, an `@`, mouse click-to-move on a static grid.
2. **One zone, BSP-generated** (Wolfwood). Static enemies, no AI yet.
3. **Combat loop v0** — Reaver only, melee, no juice. Just "things die when you hit them."
4. **FOV + lighting model.**
5. **Loot drop + inventory + slot equipping.**
6. **Tier system + ID flow at the Reading Stone.**
7. **Town as a static map with NPCs as dialogue stubs.**
8. **All four classes' starter skills.**
9. **Wolf-Mother boss** (the first iconic encounter end-to-end).
10. **Hit-stop, screen shake, glyph flashes** — the juice pass.
11. **Procgen polish, modifiers, second zone (Drowned Hall).**
12. **Echo-pact mechanics.**
13. **Iterate.**

> This is roughly the order in which decisions stop being theoretical and start being playable. Don't follow it religiously.

---

## Appendices (planned)

- **A.** Skill list per class (deferred — see §4)
- **B.** Legendary catalog (deferred — see §6)
- **C.** Monster bestiary per zone (deferred — see §7)
- **D.** Modifier list (deferred — see §7)
- **E.** Lore scraps & Echo backstories (writing pass)
- **F.** UI wireframes (scaffolding pass)

---

## Reference

- Foundational research: [`docs/research/arpg-design-research.md`](../research/arpg-design-research.md)
- This document is the living source of truth. Update it when decisions change.
