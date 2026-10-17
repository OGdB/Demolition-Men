# Demolition Men — Roadmap to v1.0

**Status:** in progress · written 2026-10-16 from a full pass over the code, scenes and prefabs ·
updated 2026-10-17 after the overnight build (M3, most of M6, phones + TV, ads seam)
**Supersedes:** the project-level parts of `building-destruction-design.md` §6 (its
destruction-specific phases are folded in below).

---

## 1. The game

One or two Demolition Men walk a level from left to right, from start to finish. The goal is
to **destroy as much as possible in as short a time as possible**, with fists, tools
(axe, wrecking ball) and utilities (dynamite and similar). When a player first reaches a
threshold, the camera starts scrolling right and keeps going. A player who falls behind the
left edge has **3 seconds to get back in** before they're out. When players climb, jump or
drop, the camera zooms so that **both players stay in frame**.

What makes it more than a beat-'em-up against walls is the support-graph destruction:
buildings collapse when they lose their path to the ground. **The best players read a
building and hit its weak point**, so the scoring and the level generator should both
reward that.

---

## 2. Current state (2026-10-16)

### 2.1 What works

| Area | State | Where |
|---|---|---|
| Destruction core | Solid. Support graph, collapse on detach, stress lean/tremble, overload crumble, persistent rubble, glass, 13 unit tests. | `Scripts/Demolition/Core`, `DestructibleBuilding`, `BuildingBlock` |
| Hand-made buildings | ~20 prefabs auto-scanned onto the grid. | `Prefabs/Buildings`, `TestBuildings`, `Sky's Prefabs` |
| Code-built buildings | Supported via `Initialise` + `RegisterBlock`. Proven by `DemoBootstrap`'s procedural building. | `DemoBootstrap.CreateBuilding` |
| Players | Two sprite prefabs: movement, jump feel, fists / axe / wrecking ball per-material damage, 3 dynamite, health. | `Scripts/Demolition/Player`, `Prefabs/Demolition` |
| Scrolling camera | Stands still until a player touches the front trigger, then scrolls (+6 while pushed, 1.5 base). Follows the players' midpoint upward and zooms to frame both. | `CameraMovement`, `cameraSpeed`, `Main Camera 1.prefab` |
| Level assembly | `Level 1` chains 10 random parts from a list of 13, then an end part. Building slots in the parts spawn a random hand-made building. | `TerrainGenerator`, `BuildingGenerator`, `Prefabs/Generator` |
| Run end | Run ends when every player is permanently dead. End menu shows Retry / Quit. | `EndMenu`, `PlayerHealth.Kill` |
| Menus | Main menu (solo/co-op, play, tutorial), pause (Esc / Start). | `UI/` |
| Art & audio | Pixel art at 32 px/unit (player sheets, object set, tile sets), 8 sound clips, 2 music tracks. | `Sprites/`, `Animations/`, `Palettes/`, `Sounds/` |

### 2.2 Gaps against the core loop

| Loop element | Gap |
|---|---|
| **Finish** | No finish line or win state. `levelPart_End` is a decorative trailer tilemap, and the camera scrolls straight past it. |
| **"Short timespan"** | No timer, no par time, no time bonus. |
| **Score** | A flat +10 per block destroyed by damage. **Blocks that collapse and survive as rubble score nothing**, so hitting a weak point (the system's best moment) pays less than grinding bricks. No per-player attribution. No level-wide destruction %; `DestroyedFraction` exists per building and is used only by the test-bench HUD. |
| **Falling behind** | No 3-second countdown. The left trigger calls `PlayerHealth.Kill()` instantly. |
| **Zoom down** | The camera never goes below its starting Y (`baseY`), so sewer parts are only framed by zooming out. |
| **Tools & utilities** | Weapon pickups were deleted in the August migration. Weapons switch only with keyboard keys 1/2/3, so **gamepad players can't switch weapons at all**. Dynamite never refills. |
| **Co-op input** | Player 2 needs a second gamepad. Keyboard + one gamepad doesn't work, because P1 merges the keyboard with gamepad 0. |
| **Results / progression** | No results screen or saves. The hub (trailer with TV and door) has no behaviour and is never visited: `MainMenu.PlayGame` loads build index 3 (Level 1). The skill-shop canvas isn't in any scene. |
| **Tutorial** | `Tutorial.cs` isn't attached anywhere, and its prompts refer to the deleted pickups. |
| **Content** | One level type, one look, about 20 hand-made buildings. No pipeline to make more. |

### 2.3 Debt and bugs found while mapping

- **About 230 uncommitted changes** on `claude/demolition-men-physics-d52rzw`: the whole August
  legacy-system migration (110 deletions, 84 modifications, 36 new files including
  `Score.cs` and `Assets/Editor/`). The branch is also 15 commits ahead of `main` and
  unmerged. A fresh clone of the branch **doesn't compile**, because `Score.cs` is untracked.
- `cameraSpeed`: each player entering adds +6, so two players give +12. When one leaves, the
  speed resets to 1.5 even if the other is still pushing.
- The left kill trigger sits at `-1.2 × orthographicSize`. At 16:9 the visible half-width is
  `1.78 × size`, so players die while still about 16% of the screen width inside the left
  edge.
- `TerrainGenerator` never despawns parts behind the camera, which is fine for 10 parts but
  not for long levels. It also paces itself only against Player 1.
- `GlobalVariables.FixedUpdate` calls `GameObject.Find` and `GetComponentInChildren` on every
  physics tick.
- Sprite import settings vary: 32 px/unit for most art, but some assets use 100, 64, 34, 150
  or 6.
- The stale `Demolition Men/Demolition Men/` snapshot is still tracked.

---

## 3. What v1.0 is

**In scope**
- The full loop: a 3-2-1 start, scrolling camera, a 3-second out-of-bounds countdown, a finish
  line, a timer, scoring that rewards collapses, a results screen with a grade.
- 1–2 local players with a join screen (keyboard and/or gamepads, any mix).
- Fists, axe, wrecking ball and dynamite as **pickups**, plus 1–2 utilities (e.g. spring pad,
  weight drop).
- **3 themes × 3–4 campaign levels**, all generated from data (spec + seed), plus a
  **daily seed** mode.
- **Procedural buildings and levels**, with an **agentic content pipeline** producing themes,
  level specs and coherent sprites.
- The hub as level select, saves (best grade per level, unlocks), settings, juice (shake,
  hit-stop, dust), and a Windows build.
- A **skill shop** in the hub: banked run score buys player upgrades (D6).

**Out of v1 (post-v1 backlog)**
- Online co-op over **Netcode for GameObjects (NGO)**. The structure is ready for it (see
  design doc §5), but it adds months (D4). Plan: `docs/multiplayer.md`.
- Online leaderboards, endless mode, player level editor.

---

## 4. Milestones

Sizes are relative: **S** ≈ a few sessions, **M** ≈ 1–3 weeks part-time, **L** ≈ 3–6 weeks.
The art-pipeline tooling in M4 is Python-only, so it can **run in parallel** with M2 and M3.

```
M0 Stabilize ─▶ M1 Core loop ─▶ M2 Building gen ─▶ M3 Level gen ─▶ M5 Agentic pipeline ─▶ M6 Ship v1.0
                                   └──────── M4 Art pipeline (parallel) ───────┘
```

### M0 — Stabilize (S) ✅ 2026-10-16
- ✅ Commit the migration in logical commits, then merge the branch into `main`.
- ✅ Delete the stale `Demolition Men/Demolition Men/` copy; stop tracking `Logs/` and
  `UserSettings/`.
- ✅ Remove dead code (`PointsScript`, `Tutorial.cs` until it's rebuilt in M1).
- ✅ Update design doc §2/§3 (they described `SimplePlayerController`).
- ✅ MVC convention and folder layout recorded in `CLAUDE.md`.
- Moved to M4: sprite pixels-per-unit. Changing legacy sprites would rescale existing
  prefabs for art that M4 replaces; the M4 import rules enforce 32 px/unit on new art.
- **Done when:** `main` compiles from a fresh clone, EditMode tests pass, and Level 1 plays.

### M1 — Core loop complete (M/L) — ✅ built, awaiting a hands-on playtest
The described game, playable start to finish with today's content. Code lives in
`Scripts/Demolition/Run/` (see the Run loop section of `CLAUDE.md`).
- ✅ **`RunController`** + `RunState` model: `Countdown(3-2-1) → Playing → Finished | Failed`,
  timer, controls locked during the countdown. `EndMenu`, `ScoreManager`, `GlobalVariables`
  and `Score` are gone; menu choices go through `Session`.
- ✅ **Finish line** (`FinishLine` in `levelPart_End`). The run finishes when all living
  players have crossed; a 5-second "last call" starts when the first one crosses.
- ✅ **Camera v2** (`ScrollCamera`, replaces `CameraMovement` + `cameraSpeed`)
  - Out-of-bounds from the viewport: left of the screen (or below the bottom) starts a
    **3-second countdown**, shown as the warning sign pinned to the left edge at the
    player's height. Getting back in cancels it.
  - The front player pushes the camera at 70% of the screen; the first push starts the
    auto-scroll. Fixes the speed-up stacking bug.
  - Framing keeps the authored ground at the bottom and zooms out for jumps; a player below
    the ground (sewer) lowers the bottom edge. Explicit per-segment camera rails move to M3,
    where level specs can carry them.
- ✅ **Co-op elimination (D1).** Out when the countdown runs out or health hits 0. First
  time: rejoin next to the partner after 5 s, −500 points. After that: out until the partner
  collects a Resurrect. The run fails when no player is alive. Solo: out ends the run.
- ✅ **Scoring v2** (`RunScore`, fed by `DestructionEvents`): points per material (glass 5 …
  support 40), collapsed blocks credited to whoever destroyed the support, chain multiplier
  `×(1 + n/20)` capped at ×3, speed bonus ×1.5 at ≤60% of par down to ×1.0 at par, grade
  S/A/B/C from level-wide destruction (80/60/40%).
- ✅ **HUD:** timer, par, destruction %, per-player score/health/weapon/dynamite/free-life,
  rejoin and out status, out-of-bounds warning, 3-2-1/GO!/last call, score popups.
- ✅ **Results screen:** grade, time vs par, points per player, speed bonus, total;
  Retry / Menu (Next and Hub arrive with M3/M6).
- ✅ **Pickups** (`Scripts/Demolition/Pickup/`): axe (25 hits), wrecking ball (20), dynamite
  (+2, carry max 9), Resurrect (co-op only; stays put unless the partner is out). Weapons
  lose a hit per landed swing and fall back to fists; players carry an inventory and cycle
  it with LB/RB or Q/E (1/2/3 hotkeys are debug-build only). Pickups fall when their roof
  collapses. Each level part has a `PickupSlot` rolling a weighted random pickup
  (shortcut until M3 places them from the level spec).
- ✅ **Join screen** (`Menu/Controller/JoinScreen`, `Menu/View/JoinView`): Play opens a lobby
  where any keyboard or gamepad joins with A / Space, leaves with B / Esc, and continues with
  Start / Enter. Joined devices go to `Session`; each player then reads only their own
  device (keyboard + one gamepad works). Replaces the single/co-op and keyboard/controller
  menu steps. Starting a level directly in the editor keeps the default input.
- ✅ **Tutorial** rebuilt: `TutorialPrompt` markers show one hint each in the bottom bar as
  the front player passes them (move/jump, hit, axe + weapon switching, supports, dynamite,
  keep up, finish). An axe and dynamite sit where the hints teach them, the camera
  scrolls slower (0.9), par is 90 s, and the results screen's NEXT goes to Level 1.
- **Done when:** a 3-minute Level 1 can be finished solo and co-op. You can lose by falling
  behind, recover inside 3 seconds, and see a grade. A weak-point collapse visibly
  outscores grinding the same blocks.

### M2 — Procedural buildings (M) — ✅ 2026-10-17
Design in §5.1. Built on **today's sprites**. Code in `Scripts/Demolition/Generation/`.
- ✅ `BuildingSpec`, `BuildingGrammar` (storeys, walls with windows and a door, slabs with
  stairwells, interior supports, setbacks, roof, chimney), `BuildingValidator`,
  `BuildingBuilder`, `GeneratedBuilding` slot, `Pcg32`.
- ✅ The validator replays each ground-level block's destruction on a fresh
  `BuildingStructure`, including overload crumble (weakest first, only crumbles that happen
  within 3 s count). Its predictions match play within a few percent.
- ✅ **5 Town archetypes** (`Assets/Content/Town/Archetypes/*.json`: shack, rowhouse,
  apartment, warehouse, tower) and the `TownTileset` (object-set sprites).
- ✅ **Building Lab** (Tools ▸ Demolition ▸ Building Lab): seed browsing with weak points
  tinted red, metrics, bake to prefab (auto-scans to the same cells), validate 1,000 seeds,
  contact sheet PNG. Static entry points for agents/batch (`BuildingLab.ValidateAllCli`).
- ✅ EditMode tests: 200 seeds per archetype stand and hit targets, same seed → same hash,
  seeds vary, validator unit cases.
- ✅ Result: 0 failures over 1,000 seeds for every archetype, 0.1–4 ms per building.
  Level 1's building slots grow a generated building half the time (shortcut in the legacy
  `BuildingGenerator`; generated buildings never grow into a standing neighbour).
- Deferred to M4: autotiled edge variants (the tileset has one sprite per kind; variants
  slot in per entry when the art pipeline makes them).

### M3 — Procedural levels (M) — ✅ built 2026-10-17, awaiting a hands-on playtest
Design in §5.2. Code in `Scripts/Demolition/Level/` (see the Levels section of `CLAUDE.md`).
- ✅ `LevelSpec` (JSON), `LevelGrammar` (pacing tokens: start, street, cluster:n, tower,
  gap, hill, sewer, gate, breather, finish), `LevelLayout`, `TraversalValidator` (replays
  the player's jump physics; the grammar re-rolls derived seeds until it passes).
- ✅ `LevelBuilder`: ground tilemap up front, buildings/pickups/finish streamed in per
  segment ahead of the camera and despawned behind it. The **Run scene** holds only
  `LevelBootstrap`, which assembles the level from `Resources/GameConfig`. Replaces
  `TerrainGenerator` (Level 1 stays in the project, out of the build).
- ✅ **Bot playtester:** `BotPilot` drives a player through `ScriptedInputSource`;
  `BotRunTests` (PlayMode) plays every campaign level to the finish. Par is now derived
  from the course (time the auto-scroll takes from spawn to finish), not calibrated by hand.
- ✅ Campaign: Town levels 1–4 (`Content/Town/Levels`), level select with unlocks, and a
  **daily seed** that reuses a campaign level's pacing.
- ✅ Level Lab (Tools ▸ Demolition ▸ Level Lab): preview, validate 100 seeds, play / play with bot.
- **Still to check in the editor:** the bot finishes all levels; 60 fps through the
  biggest collapse on a mid-range phone.

### M4 — Art pipeline (L, can start alongside M2) — ✅ built 2026-10-17
Built: `tools/artpipe` (master palette, procedural tile generators, normalizer, QA with
contact sheets, `unity_theme.py` writing tiles/tileset/theme assets), the
`ContentSpriteImporter` AssetPostprocessor, and two themes made with it — **Port** and
**Neon**, 17 sprites each, 4 archetypes and 4 levels each. Not yet: autotile edge variants,
per-theme parallax backdrops (the Town backdrop is tinted per theme), SpriteAtlas.

Original plan (design in §6):
- Style-bible schema, procedural tile generator, normalizer, QA and contact sheets.
- Unity `AssetPostprocessor` enforcing import settings.
- Proof: **re-skin Theme 1 (Town)** from the existing art as the anchor, then make
  **Theme 2** with the pipeline.
- **Done when:** a Theme 2 tile set, props and parallax background pass QA and look like
  the same game as the demolition man sprite.

### M5 — Agentic content pipeline (L) — roles written 2026-10-17
The five roles exist as Claude Code subagents in `.claude/agents/` (theme-director,
architect, artist, level-designer, integrator), each ending in validators it can't argue
with (`tools/unity-check/check.sh`, `tools/artpipe/qa.py`). Not yet run end to end on a
Theme 4 pitch, and no workflow script chains them.

Original plan (design in §7):
- Agent roles, artifacts, validators runnable from the command line, human gates.
- Run it end to end to produce **Theme 3** with almost no hand work. Then use it to fill
  the 3 × 3–4 campaign.
- **Done when:** one pitch sentence becomes a playable, reviewed theme PR.

### M6 — Meta, juice, ship (M/L) — partly built 2026-10-17
Built: JSON save in `persistentDataPath` (`Meta/`), unlocks in campaign order, the **skill
shop** (power, hard hat, boots, TNT belt, tool grip; cash = score / 4, x1.25 for a
finish), settings (volume, music, screen shake, HUD size, touch controls mode/size/opacity),
code-built pause menu, results with cash and best-result badges, screen shake by collapse
size, hit-stop, dust, pooled particles. **Decision change:** the level select is a menu
overlay (`LevelSelectView`) instead of walking the hub — it has to work with a thumb on a
phone and from a sofa with a gamepad; the hub scene stays for a later "home base".
Still open from the list below: crack-overlay damage states, material break sounds and a
music track per theme, composite debris, rebinding and resolution settings, builds and
store assets.

Original plan:
- Hub as level select (TV = theme/level select, door = start), unlocks by grade, JSON save in
  `persistentDataPath`.
- **Skill shop** in the hub (D6): run score is banked as cash and spent on upgrades, saved
  with progress. Start with tuning values the player scripts already expose:
  `PlayerCombat.damageMultiplier` and `DynamiteCount`, `PlayerHealth.maxHealth`,
  `PlayerMotor.maxSpeed`. Reuse the unused `Canvas_Skillshop` prefab where it fits.
- Juice: screen shake scaled to collapse size, hit-stop on heavy hits, dust clouds,
  crack-overlay damage states, material-specific break sounds, a music track per theme.
- Perf: merge large detached islands into one composite body (design doc Phase 2); profile
  the worst collapse.
- Settings (volume, rebinding, resolution, screen-shake toggle), Windows build, store page
  assets (the art pipeline can make key art).

### Post-v1
Online co-op with NGO (replicate level id + seed + cell health; `docs/multiplayer.md`),
daily-seed leaderboards, endless mode, player level editor, more themes from the pipeline.

---

## 5. Procedural generation design

**Principle: generate data, not scenes.** Generators turn a JSON spec plus a seed into a
`BuildingSpec` / `LevelSpec`, which is validated by pure code and then instantiated. Agents
and people edit specs, never prefab or scene YAML. Because the output is deterministic, a
level is fully described by `{specId, seed}`. That gives shareable daily seeds for free and,
later, netcode that only replicates the seed and the destroyed cells.

### Code layout
Feature folders inside the `Demolition` assembly, split by MVC layer (no separate asmdef):
```
Assets/Scripts/Demolition/Generation/
  Model/      Pcg32, BuildingArchetype (JSON), TileKind, BuildingSpec, BuildingGrammar,
              BuildingValidator; later LevelSpec, LevelGrammar, TraversalValidator (M3)
  View/       BuildingTileset (ScriptableObject: TileKind → sprites)
  Controller/ BuildingBuilder (spec → blocks), GeneratedBuilding (slot); later LevelBuilder
Assets/Editor/Generation/           Building Lab window + -executeMethod entry points
Assets/Content/<theme>/             Archetypes/*.json, <Theme>Tileset.asset; later levels/*.json
tools/artpipe/                      Python art tooling (see §6)
```
Specs are JSON read with `JsonUtility`; tile names are strings parsed to `TileKind`, so no
extra JSON package is needed. ScriptableObjects hold only the asset references JSON can't.

### 5.1 Buildings: grammar → validate → build
1. **Massing.** The archetype picks width, floors and floor height, plus optional setbacks,
   an L-shape, or a tower on a podium.
2. **Skeleton.** A foundation row (anchors), `Support` columns every *k* cells, floor slabs
   every *h* rows.
3. **Envelope.** Walls, then window rhythm (Glass) and doors at ground level. This uses the
   window rules `DemoBootstrap` already proved: no wall cell may rest on a pane.
4. **Roof.** Flat cap, gable (45° rider planks, like the hand-made roofs), or a prop such as
   a water tower or billboard.
5. **Interior.** Stairs and platforms so players can climb, plus a background wall made of
   `bg` riders.
6. **Decor riders.** Signs, AC units, awnings, antennas. They're cosmetic and fall with
   their host block.
7. **Validate and score.**
   - Every cell is supported at spawn, and the baseline overload stays under
     `overloadTolerance`.
   - **Weak-point profile:** for each structural cell, how many cells detach if it alone is
     removed (a what-if `RemoveCells` on a cloned structure; `BuildingStructure` needs a
     `Clone()`).
   - The archetype sets targets, e.g. "1–3 weak points that each drop 30–80% of the
     building". This is the knob that keeps generated buildings *fun to demolish*, not just
     standing.
8. Failures retry with the next seed offset, a bounded number of times.

Visually, each block picks its sprite variant from a **4-neighbour mask** within its
material (edges, corners, caps), so generated walls read as authored. Gameplay materials
stay the 5 `BlockMaterial` values. A theme maps its own looks onto them; for example,
"concrete" can use Brick stats with a concrete tile set.

Example archetype (`Assets/Content/town/archetypes/rowhouse.json`):
```json
{
  "id": "town.rowhouse",
  "width": [6, 10], "floors": [2, 4], "floorHeight": 3,
  "structure": { "columnSpacing": [3, 5], "column": "Support", "slab": "Wood" },
  "envelope":  { "wall": "Brick", "window": "Glass", "windowRhythm": "2-1", "door": "ground" },
  "roof": ["flat", "gable"],
  "decor": ["chimney", "satellite_dish", "awning"],
  "targets": { "weakPoints": [1, 3], "cascade": [0.3, 0.8] }
}
```

### 5.2 Levels: pacing → segments → build
- A level spec gives the theme, length, seed, scroll speed and **pacing**: a sequence of
  segment types with an intensity curve (build-up, peaks, breathers).
- **Segment library:** `start`, `street`, `cluster:n` (n buildings), `gap` (jump), `tower`
  (vertical climb, camera zooms), `sewer` (camera rail dips), `gate` (a wall that must be
  broken to pass: forced destruction), `hazard-yard`, `breather`, `finish`.
- Segments keep today's contract: they connect at an entry/exit ground height, like the
  current `EndPosition`. Ground comes from theme tiles on a Tilemap. Buildings fill the
  slots from the theme's archetypes, weighted by intensity.
- **Validation:**
  - The traversal validator checks every gap and ledge against jump arcs computed from
    `PlayerMotor`'s `maxSpeed` / `jumpImpulse` / gravity.
  - The total destructible cell count and weak-point count fall in a target band.
  - The bot run completes under the camera speed.
- **Runtime:** a level is generated at load from `{spec, seed}`, with no baking. Segments
  stream in ahead of the camera and are destroyed once they're behind the kill edge, so
  long levels stay cheap.

Example level (`Assets/Content/town/levels/town-03.json`):
```json
{
  "id": "town-03", "theme": "town", "seed": 48213,
  "length": 420, "scrollSpeed": 1.6, "parTime": 150,
  "pacing": ["start", "street", "cluster:2", "gap", "tower", "breather",
             "sewer", "cluster:3", "gate", "finish"],
  "pickups": { "dynamite": 4, "axe": 1, "wreckingBall": 1, "resurrect": 1 }
}
```

---

## 6. Art pipeline

The goal is coherent pixel art without a human artist. The reliable approach is to **split
the work by what each method does well**, and push *everything* through the same normalizer
and QA gate.

| Asset kind | Method | Why |
|---|---|---|
| Material tiles (brick, concrete, metal, wood, glass, girder) + autotile edges | **Procedural, code-drawn** (Python) from palette ramps | Tileable, on-palette and consistent by construction. Agents write these generators well. |
| Damage cracks (3 stages), rubble chunks, dust, UI frames | Procedural | Shared across materials; cheap, big feel. |
| Props, signs, vehicles, water towers, key art | **AI image generation**, then normalize | Needs variety and character, and is forgiving of small inconsistencies. |
| Parallax backgrounds (3–4 layers per theme) | AI generation + normalize + desaturate/value-shift per layer | Big, low-detail, sits behind gameplay. Reuses the unused `Parralyx.cs`. |
| Player characters | **Keep the existing hand-drawn sheets.** P2 / skins via palette swap. | The warning-sign demolition man *is* the style anchor. |

**Style bible** (one per theme, machine-readable `style.json` plus a short `style-bible.md`):
- 32 px/unit, pixel art, point filtering.
- A **master palette** (~32–48 colours) shared by all themes; each theme uses a subset plus
  2–3 accent colours, so themes still read as one game. Each material gets a 3–4 step
  ramp.
- Light from the top-left. 1 px outline in the darkest ramp colour on foreground objects
  only. Backgrounds have no outline, lower contrast and cooler values.
- Reference sheet: the existing object set and player sheet are attached to every
  generation request.

**Tooling (`tools/artpipe/`, Python + Pillow + numpy)**
- `tiles.py`: material generators (running-bond brick with mortar and per-brick tone
  jitter, panelled metal with rivets, plank wood with grain, glass with a diagonal
  highlight, X-truss girders), their 16 autotile variants, crack overlays.
- `normalize.py`: any image → target grid (nearest/box downscale), palette quantize (nearest
  in CIELAB), hard alpha threshold, foreground outline pass, trim and pivot.
- `qa.py`: 100% palette compliance, no semi-transparent pixels, pixel-grid integrity, tile
  seam continuity, foreground/background contrast, and a **contact sheet** next to the
  reference art for visual review.
- Unity side: an `AssetPostprocessor` sets import settings for anything under
  `Assets/Content/**/Sprites` (32 PPU, Point, no compression, pivot rules by name), plus one
  SpriteAtlas per theme.

**Generators:** the project already has Unity AI (`com.unity.ai.assistant`), and the Unity
MCP server exposes asset-generation tools, so start there because it's already wired into
the editor. Fall back to an open pixel-art model via Hugging Face. Check commercial-use terms
for whichever model ships art. AI output is **never** used raw: always normalize → QA →
human pick from 3–4 candidates.

---

## 7. Agentic content pipeline

Each stage is an agent role that writes **files in the repo**, runs **validators it can't
argue with**, and stops at a **human gate**. The repo is the shared memory, and a PR is the
review unit (one branch per theme).

```
pitch ─▶ Theme director ─▶ [gate] ─▶ Architect ─┐
                                    Artist ─────┼─▶ Integrator ─▶ [gate: play + merge PR]
                                    Level designer ┘
```

| Role | Writes | Automatic checks | Human gate |
|---|---|---|---|
| **Theme director** | `theme.json`, `style.json`, `style-bible.md`, archetype list, hazard/prop list, asset manifest | Schema valid; palette contrast and ramp checks | ✅ Approve concept + palette (the one gate that matters most) |
| **Architect** | `archetypes/*.json` + preview contact sheet | `BuildingValidator` over 1,000 seeds: stands, stress baseline, weak points in target | Glance at the sheet |
| **Artist** | Sprites listed in the manifest | `normalize` → `qa` → critique loop (looks at the contact sheet next to the reference and the bible checklist, retries with notes) | ✅ Pick candidates for hero assets |
| **Level designer** | `levels/*.json` | Level generation + traversal + bot run: completable, par time, destruction potential in band | ✅ Play it |
| **Integrator** | ScriptableObjects, imports, PR | `dotnet build`, EditMode tests, PlayMode smoke (spawn the level, simulate, no exceptions, fps budget), screenshots via the Unity MCP camera capture | ✅ Merge |

**How it runs**
- Each role is a Claude Code subagent (`.claude/agents/<role>.md`) with a skill holding its
  checklist, chained by a workflow script. Any stage can be re-run alone, because it reads
  and writes files.
- Validators are command-line entry points so agents get hard pass/fail:
  - Unity: `-executeMethod Demolition.Generation.Cli.Validate <theme>`, which writes a JSON
    report.
  - Art: `python tools/artpipe/qa.py`.
  - Batch mode only works with the editor closed. When it's open, the agent goes through
    the Unity MCP server (run command, test runner, console logs) instead.
- Candidate and scratch art lives outside `Assets/` (`content-work/<theme>/`), so Unity
  never imports rejected files.
- **Order matters:** the pipeline is only as good as the generators it feeds. Building it
  before M2/M3 exist would just produce prompts with nothing to run them through.

---

## 8. Decisions needed

| # | Question | Recommendation |
|---|---|---|
| D1 | Co-op: is a player who falls behind (or dies) out for the whole run? | **Decided:** one free rejoin at the partner after 5 s (score penalty). After that, out until the partner collects a Resurrect pickup. Details in M1. |
| D2 | When does a level finish? | When all living players cross the finish line, with a 5 s "last call" after the first crosses. |
| D3 | Destruction vs speed weighting | Destruction-led score with collapse credit and chain multiplier; speed as a ×1.0–1.5 multiplier against par (§M1). |
| D4 | Online multiplayer in v1? | **Decided:** post-v1, using Netcode for GameObjects (NGO), not Fishnet. Local co-op first. Plan and its decisions (D11–D18): `docs/multiplayer.md`. |
| D5 | Campaign levels: baked scenes or generated at load? | **Generated at load from `{spec, seed}`.** Bake only if a level needs hand polish. |
| D6 | Skill shop / upgrades in v1? | **Decided:** yes, in v1 (M6). |
| D7 | Art anchor | Keep the existing demolition man and object set as Theme 1 (Town) and as the reference for every generated theme. |
| D8 | Image generator | Unity AI first (already in the project and the MCP). Check commercial terms before shipping its output. |
| D9 | Target platform | **Decided (2026-10-17):** PC (Windows, Steam Deck), TV (gamepads, couch co-op) and phones/tablets in landscape (touch). WebGL (browser) added later the same day, online play included (`docs/multiplayer.md` D19). One build of the UI for all: code-built uGUI through `UiFactory`, safe area, HUD size setting. |
| D10 | Business model | **Decided (2026-10-17):** free, with ads on phones only (interstitials between runs, opt-in rewarded "2x cash"); a "remove ads" purchase later. See `docs/monetization.md`. |

---

## 9. Risks

| Risk | Mitigation |
|---|---|
| AI art drifts off-style | Procedural for everything tileable, palette lock + normalizer, reference sheet in every request, human gates on hero assets. |
| Generated buildings feel samey | Archetype variety, weak-point targets, curated seeds for the campaign. Generators run in the Building Lab so a human can taste them quickly. |
| Many blocks per level hurt perf | Standing blocks are static (cheap), segments stream and despawn, falling cap, composite debris (M6), a profiling gate in M3. |
| Scope creep (online, shop, editor) | The v1 fence in §3. Post-v1 items wait. |
| Losing the uncommitted migration | M0 first. |
| Pixel shimmer with continuous zoom | Point filtering and integer-ish zoom snapping. `com.unity.2d.pixel-perfect` is installed if it becomes a problem. Low priority. |

---

## 10. Next five tasks (2026-10-17)

1. **Playtest in the editor** the overnight work: Run scene levels (Level Lab ▸ Play), the
   phone flow in the Device Simulator (touch controls, join, level select, results), the
   PlayMode `BotRunTests`. Fix what the list in `docs/overnight-2026-10-17.md` turns up.
2. **Tune** scroll speeds, upgrade costs and pickup budgets from those runs.
3. **Ad network:** install LevelPlay (or AdMob), write the `IAdService` adapter, consent.
4. **Builds:** Windows, Android (IL2CPP ARM64 is set), iOS; app icons and store pages.
5. **Themes 2–3 polish** (art pipeline output) and a music track per theme.
