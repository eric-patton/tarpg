# ARPG Design Research

> **Project**: TARPG — a solo Action RPG inspired by Diablo 1/2/3, rendered in a terminal aesthetic via SadConsole. Glyphs for actors, polished tile maps for environments.
>
> **Status**: Reference document. Used as the shared foundation for the design brainstorm that follows.

---

## 1. Purpose & How To Use This Doc

This document captures the design pillars that make Diablo-likes work. It is not a spec — the actual game design will diverge based on our brainstorming and the constraints of a terminal-style presentation.

Use this doc to:
- Anchor design discussions ("does this idea support the core loop?")
- Avoid re-discovering known-good principles
- Track open design questions until we resolve them (see section 12)

When we make a decision during brainstorming, update section 12. When we discover something new, add a section.

---

## 2. The ARPG Core Loop

> **Fight enemies → Get loot → Grow stronger → Fight harder enemies**

The loop is the heartbeat of every Diablo-like. Simple on the surface, but psychologically engineered:
- Every kill is a slot-machine pull
- Every drop is a possible jackpot
- Every level-up is a small dopamine hit

**Design implications**:
- The loop must be tight — seconds to minutes between rewards, never more.
- Reward magnitudes must vary — many small rewards punctuated by occasional huge ones.
- Audio-visual feedback sells every step. The sound of a unique dropping should be unmistakable. The sound of a critical hit should feel different from a normal hit.
- The loop must be visible at every scale — moment-to-moment (kill → drop), session-to-session (run → upgrade), and long-term (build → endgame).

---

## 3. Itemization

Items are not decoration — they ARE the progression. The whole game is a delivery system for items.

**Tiered rarity** with clear visual language:
- Normal → Magic → Rare → Set → Unique (Diablo 2 model)
- Each tier visually distinct: color of glyph/text, drop sound, beam-of-light effect for the rarest

**Random affixes** with stat ranges:
- Even the same item rolls differently each time
- Rarer items roll from better affix pools, with higher stat ranges
- Players chase "perfect rolls" indefinitely

**Weighted drops**:
- Loot biased toward class relevance, but not deterministic
- Cross-class drops still happen, fueling alt characters and trading

**Build-defining items** (uniques/legendaries):
- Don't just be "bigger numbers" — change *how* a build plays
- Example: a unique that converts physical damage to fire opens up a new build entirely

**Currency / safety nets**:
- Crafting, gambling, vendoring, trading — all give players agency on top of pure RNG
- Without them, bad luck feels like punishment; with them, it feels like a setback

**The "item porn" moment**:
- A beam of light, a distinctive sound, an unidentified scroll, a gold-bordered tooltip
- This is the single most memorable feeling in any ARPG. Get it right.

---

## 4. Progression & Build Diversity

Two dominant philosophies, each with merit:

**Diablo 3 model — accessibility**
- Respec-friendly, low commitment
- "Every hour of play gives a small upgrade"
- Lower depth but lower friction
- Wider audience

**Path of Exile / D2 model — depth**
- Massive passive trees, skill gems, ascendancies
- High commitment, high theorycrafting
- Build IS the puzzle; mistakes have weight
- Hardcore audience

**What works regardless of philosophy**:
- **Distinct class fantasies** — Necromancer plays NOTHING like Druid. In a glyph game, glyph + color alone should telegraph who you are.
- **Three axes of build expression** — active skills + passive modifiers + gear synergies. One axis is too thin.
- **Cross-branch synergies** — hybrids stay viable; specialization rewarded but not mandatory.
- **Visible numbers** — players need to see DPS, EHP, resistances. Hidden math kills theorycrafting.

---

## 5. Combat Feel ("Juice")

What separates a great ARPG from a spreadsheet:

**Hit-stop / hit-pause** — a few frames of frozen time on impact sells weight. Even in a glyph game, freezing the world for 60–100ms on a heavy hit transforms it.

**Visual feedback** — screen shake, flash, particles, gibs. Generous on big hits, restrained on small ones.

**Audio per hit type** — slash, crush, magic, crit. Sound conveys impact more than visuals do, especially in a low-res presentation.

**Crowd-clearing power fantasy** — packs of enemies dying to one well-timed ability is the genre's signature high. The "swarm of weaker enemies" is not a side feature; it's the core experience.

**Restraint** — overdoing juice causes fatigue. Reserve the big effects for big moments. Silence between waves makes combat feel like an event.

**In a glyph-based ARPG, juice translates to**:
- Glyph flashes and color pulses
- Brief screen shake on heavy hits
- One-frame whitening of struck enemies
- Distinct SFX per hit type
- Kill-burst particles (radial spray of `*` or similar)
- Floating damage numbers
- Camera focus / brief zoom on big hits or boss attacks
- Silence between encounters

---

## 6. Procedural Generation (with structure)

Diablo's "every run is different" comes from procgen — but with **fixed anchor points** (set bosses, set quest locations). Pure random feels samey; pure handcrafted kills replayability.

**The trick**: handcrafted rooms + procedural arrangement + procedural loot.

**Algorithm options**:

| Algorithm | Best For | Notes |
|---|---|---|
| **BSP (Binary Space Partitioning)** | Dungeons, crypts, towns | Clean rectangular rooms with corridors. Tree structure ensures no overlap. Easy to constrain (room count, sizes). |
| **Cellular Automata** | Caves, organic biomes | Seed grid with ~45% walls, run "become wall if 5+ neighbors are walls" for 4–5 iterations. Smooth, eroded blob shapes. |
| **Drunkard Walk** | Mines, sewers, weird zones | Chaotic winding paths. Combines well with cellular automata. |
| **Hybrid (BSP + walker/automata)** | Production default | BSP for macro, walker/automata to soften corridors and add variety. |

**Anchor points** to keep the game from feeling random:
- Set boss encounters at the end of each level
- Set NPCs in towns
- Set lore/quest objects at procedural-but-known positions
- Hand-tuned mini-rooms (vaults) randomly inserted into procedural floors

---

## 7. Difficulty & Pacing

**Sudden-jump-then-mastery**:
- Enter a new zone feeling under-leveled
- Master it through play and gear
- Leave feeling powerful
- Next zone resets the feeling

**XP curve**:
- Steep late game so early levels feel fast and rewarding
- Late levels feel earned, not handed out
- Common shape: exponential or quadratic, not linear

**Multiple difficulty tiers** (the campaign-as-tutorial pattern):
- Normal → Nightmare → Hell (D2)
- Or torment levels (D3)
- Or rifts/maps with infinite scaling (D3 / PoE)
- The campaign is the tutorial; the real game starts at higher difficulties

**Death cost**:
- Lose gold, XP, durability, or have a corpse run
- Real tension requires real consequences
- Hardcore (permadeath) mode is iconic — optional but signals genre seriousness

---

## 8. Endgame

**The campaign is the tutorial; the endgame is the product.**

Mechanics that work:
- **Scalable challenges** — rifts, maps, monoliths. Player picks their own difficulty in exchange for better loot. Self-pacing is key.
- **Infinite progression** — paragon-style levels, masteries, or atlas progression. Small drips after the level cap keep the loop going.
- **Pinnacle bosses** — capstone fights that gate the rarest loot and require specific builds.
- **Seasonal content** (less relevant for solo) — fresh ladder, new mechanics layered in. Resets the meta.

**For a solo single-player game like ours**, seasons are less central, but:
- Scalable infinite dungeons + pinnacle bosses give the game legs after the main story
- "New Game+" / higher difficulty replays of the campaign work too
- Build diversity is the real replayability — making a new character to try a new build IS the season

---

## 9. Atmosphere

Diablo 1 was scary. Diablo 2 was epic. Diablo 3 was triumphant. Tone is a deliberate design output, not a happy accident.

Matt Uelmen's Diablo 1 score is the masterclass:
- Gothic rock + orchestral + ambient drone
- Distant screams baked into the dungeon track
- Big percussion, distorted guitars, sparse melodies
- **Sound is half the horror.**

**For a terminal ARPG, atmosphere lives in**:

- **Color palette** — restrained, desaturated. Red for danger and blood. Gold for treasure. Pale blue for magic. Black backgrounds make every glyph pop.
- **Lighting / FOV** — torchlight radius, fog of war, glyphs fading as they leave the lit area. The unknown is scarier than the known.
- **Audio** — droning ambient loops per zone, distinct SFX per enemy type, rare and chilling moments (a scream from off-screen).
- **Text** — flavor text on items, on rooms entered, on lore objects. ASCII games live and die on prose.
- **Pace breaks** — quiet town hubs between violent dungeons. The relief makes the dungeons hit harder.

---

## 10. Roguelike-Adjacent Considerations (Terminal-Specific)

Our visual style invites roguelike conventions, even though our core design is ARPG. Borrow what works:

- **Every glyph carries meaning** — `@` = you, `D` = dragon, `#` = wall, `.` = floor, `*` = treasure. Don't fight this convention; embrace it. It's a feature, not a constraint.
- **Color does heavy lifting** — same glyph in two colors = two creatures. Use color as a primary differentiator.
- **FOV / line of sight** — dungeon exploration is half the fun. Don't show what the player can't see. Reveal map as you go. Remember-but-grayed-out previously seen tiles.
- **Permadeath option** — even if not default, a hardcore mode is iconic and signals the game takes itself seriously.
- **Identification mechanic** — unidentified items are a roguelike staple and a great fit for a terminal aesthetic. They synergize beautifully with item-porn moments.
- **Turn-based vs real-time** — see open question in section 12.

---

## 11. SadConsole Engine Notes

**What we're working with**:
- .NET 8/9/10 library
- Runs on MonoGame or SFML hosts
- Tile-based rendering, but tiles can be full graphical sprites — we can have nice pixel-art tile maps and still represent characters/enemies as glyphs
- Built-in GUI controls (buttons, list boxes, text fields) — useful for inventory, character sheet
- Entity system for thousands of movable objects — supports monster crowds
- Mouse + keyboard support
- Importers for DOS ANSI files, REXPaint, Playscii, TheDraw text fonts

**Ecosystem**:
- Mature roguelike tutorials exist (RogueSharp + SadConsole) — we won't be inventing FOV, pathfinding, or basic dungeon gen from scratch
- RogueSharp provides field-of-view, pathfinding, dice rolling — pairs with SadConsole

**What this means for design**:
- SadConsole supports both real-time and turn-based
- It supports both glyph-only and tile+glyph hybrid presentations
- It supports both mouse-driven (Diablo-style) and keyboard-driven (roguelike-style) input
- **Nothing about the engine forces our hand on the core ARPG question of "how does combat feel?"** — that's a pure design call we make.

---

## 12. Open Design Questions for Brainstorming

> **Status: RESOLVED.** All 13 open questions below were worked through in the kickoff brainstorming session. The decisions are captured in [`../design/game-design.md`](../design/game-design.md). The questions are preserved here as a record of what we considered and why.

These are the decisions we'll work through together. Each one significantly shapes the game.

### A. Combat Model: Turn-based vs Real-time
- **Turn-based** — roguelike-authentic, easier to implement well in a glyph game, every action is deliberate, lower technical complexity, supports careful tactical play.
- **Real-time** — Diablo-authentic, harder to make "feel" right with glyphs, demands more juice and animation, supports the swarm-clearing power fantasy.
- **Hybrid (e.g., real-time with pause)** — possible but unusual. Stoneshard, Cogmind use variants.

### B. Primary Input: Mouse vs Keyboard
- **Mouse-driven (Diablo-style)** — click to move, click to attack. Familiar to ARPG fans.
- **Keyboard-driven (roguelike-style)** — directional keys, hotkeys for skills. Familiar to roguelike fans.
- **Hybrid** — most modern roguelikes support both.

### C. Solo Character vs Hireling/Party
- **Solo only** — pure focus on one character.
- **Hireling/follower** — D2 mercs added flavor. AI complexity rises.
- **Small party** — diverges further from Diablo into more traditional RPG territory.

### D. Permadeath
- **No permadeath** — friendlier, more save-scumming.
- **Permadeath as default** — pure roguelike commitment.
- **Optional hardcore mode** — best of both worlds. Iconic for ARPGs.

### E. Story Scope
- **Full campaign with acts** — D2 model. Heavy authoring burden.
- **Light frame story + deep dungeon descent** — more roguelike. Easier scope.
- **Both** — campaign for first run, infinite dungeon for endgame.

### F. Class System
- **Fixed classes** (D2: Barb, Sorc, Necro, etc.) — strong identity, focused balance.
- **Class-less** (PoE-ish) — pure build freedom.
- **Hybrid** (start with class, branch into hybrid trees) — D2's actual implementation.

### G. World Structure
- **Town hub + linear acts** (D2/D3) — well-paced, authored.
- **Open world** (D4) — exploration-heavy, harder to balance.
- **Pure descent** (Nethack/DCSS) — lean, replayable.

### H. Setting & Theme
- **Gothic horror** (D1) — restrained palette, dread, religious imagery.
- **High dark fantasy** (D2/D3) — epic scope, demons, angels.
- **Original setting** — full creative freedom but more authoring.

### I. Endgame Model
- **Infinite descent** — bottomless dungeon with scaling.
- **Rifts / maps** — discrete scalable challenges.
- **Both** — common modern pattern.

### J. Crafting & Trading
- **None** — pure drop-based.
- **Crafting only** (single-player friendly).
- **Both** (trading less relevant in solo).

### K. Inventory Model
- **Tetris** (D2) — items take grid space. Flavorful, fiddly.
- **Slot-based** (D3/D4) — fixed slots. Cleaner.
- **Unlimited** — purely numerical capacity.

### L. Save System
- **Single save slot per character** — roguelike-style, sharper consequences.
- **Multiple saves / save anywhere** — friendlier.

### M. Visual Style Resolution
- **Pure CP437/ASCII** (extended ASCII) — purest terminal feel.
- **Tile-based environments + glyph actors** — what you initially described. More visually appealing.
- **Mixed approach with detailed item icons** — items get full sprites in inventory, glyphs in world.

---

## 13. Sources

- [Reverse Design: Diablo 2 — The Game Design Forum](http://thegamedesignforum.com/features/RD_D2_1.html)
- [Why Diablo 2 Holds Up Almost 20 Years Later — David Craddock](https://medium.com/@dlcraddock/why-diablo-2-holds-up-almost-20-years-later-df4be1a5b740)
- [Game Design Analysis of Diablo II — Yu-kai Chou](https://yukaichou.com/gamification-study/game-design-analysis-diablo-ii-level-ii-octalysis/)
- [ARPG Itemization — Marc Leo Seguin](https://marcleoseguin.com/2026/04/08/arpg-itemization/)
- [Why the ARPG Gameplay Loop Keeps Players Hooked — Fextralife](https://fextralife.com/the-arpg-loop-players-never-get-tired-of/)
- [Top Action RPGs in 2026 — Tribality](https://www.tribality.com/articles/top-action-rpgs-arpgs-in-2026-the-best-isometric-loot-driven-games/)
- [Path of Exile vs Diablo 3 Mega-Comparison — PureDiablo](https://www.purediablo.com/path-of-exile-vs-diablo-3-mega-comparison-list)
- [What Features Influence Impact Feel? (academic, arXiv)](https://arxiv.org/pdf/2208.06155v3)
- [Juice in Game Design — Blood Moon Interactive](https://www.bloodmooninteractive.com/articles/juice.html)
- [Procedural Map Generation — Cogmind / Grid Sage Games](https://www.gridsagegames.com/blog/2014/06/procedural-map-generation/)
- [Dungeon Generation Algorithms: Patterns and Tradeoffs — PulseGeek](https://pulsegeek.com/articles/dungeon-generation-algorithms-patterns-and-tradeoffs/)
- [SadConsole — GitHub (Thraka)](https://github.com/Thraka/SadConsole)
- [Creating a Roguelike Game in C# (RogueSharp + SadConsole)](https://roguesharp.wordpress.com/)
- [RogueBasin: ASCII](https://www.roguebasin.com/index.php/ASCII)
- [Roguelike — Wikipedia](https://en.wikipedia.org/wiki/Roguelike)
- [Diablo IV Paragon System Interview — PlayStation Blog](https://blog.playstation.com/2023/06/02/diablo-iv-interview-paragon-system-pvp-replayability-and-more/)
- [Diablo Soundtrack Review — The Greatest Game Music](https://www.greatestgamemusic.com/soundtracks/diablo-soundtrack/)
- [Best RPGs With The Most Build Diversity — Game Rant](https://gamerant.com/rpgs-best-build-diversity-multiclass-skill-tree/)
- [Game Balance Concepts: Advancement, Progression and Pacing](https://gamebalanceconcepts.wordpress.com/2010/08/18/level-7-advancement-progression-and-pacing/)
