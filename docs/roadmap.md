# Demolition Men — Roadmap to v1.0

**Status:** in progress · written 2026-10-16 from a full pass over the code, scenes and prefabs ·
updated 2026-10-17 after the overnight build (M3, most of M6, phones + TV, ads seam) ·
re-planned 2026-10-20 around a **Google Play release in January 2027** (§4 R1–R5; marketing
in `marketing.md`)
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

**Tone: "you're a badass".** Think Broforce and Duke Nukem: over-the-top, loud and swaggering,
with a grin. Music is guitar-driven hard rock and metal (not funk, jazz or cute chiptune).
Hits and blasts are big, and the UI shouts in stencil type. Music, sound, art, store text and
clips all follow this.

---

## 2. Current state (2026-10-20)

The game plays start to finish on PC and in the editor's phone simulator. M0–M4 are built,
most of M6 is built, and online co-op has its code in but hasn't been played between two
devices over the internet yet.

### 2.1 Built

| Area | State |
|---|---|
| Core loop | 3-2-1 start, scrolling camera with a 3 s out-of-bounds grace, finish line with last call, timer and par, scoring with collapse credit and a chain multiplier, 0–3 stars, results screen, co-op rejoin and Resurrect. |
| Destruction | Support graph, collapse on detach, stress and overload crumble, rubble that falls again when its footing goes, glass, back walls that crumble with their cells, blasts (TNT, gas tanks) that hurt and throw players. |
| Content | 3 themes (Town, Port, Neon) × 4 campaign levels, a daily seed, the tutorial. Buildings and levels are generated from JSON + seed and validated (they stand, have weak points, every jump is passable); street props and decor. |
| Players | Fists, axe, wrecking ball, dynamite, headbutt; pickups; skill-shop upgrades. |
| Meta and menus | Save file, unlocks, shop, settings, pause, title, join screen, level select, quit popup. |
| Phones | Touch controls, safe area, landscape lock, IL2CPP ARM64, a sprite atlas per theme, pixel-art import settings. |
| Ads | The `IAdService` seam with interstitial and rewarded placements and a pacing policy; no ad SDK. |
| Online | O1 done; O2 code in (NGO over a WebSocket relay, join codes in `OnlineView`, the host plays on when the client leaves); the relay runs on the VPS behind TLS (`wss://relay.oeds.space/ws`). |
| Tooling | Editor-free compile and 213 model tests, level previews, Building and Level Labs, the bot playtester, the art pipeline, the content-pipeline agents. |

### 2.2 Left before a Play release

| Area | What's missing |
|---|---|
| Android build | No package name, company still `DefaultCompany`, no upload keystore, no app icon. Target API is "highest installed", but Play needs **API 36** for new apps since 31 Aug 2026. App category not confirmed as Game; internet permission not forced on (online needs it). Never built, installed or profiled on a phone. |
| Play account | None yet. A new personal account must run a **closed test with 12+ testers for 14 days** before it may publish to production. |
| Ads | No SDK, no consent form (Google requires a certified consent tool for ads in the EEA/UK), no "privacy options" entry in Settings. |
| Online | O2 play tests, the rest of O3 (a WebGL build on itch.io; phone, PC and browser joining each other over mobile data), O4 hardening (a backgrounded app, timeouts, version mismatch, relay down). |
| Legal and store | Privacy policy, Data safety form, content rating, target audience, ads declaration, EU trader status, store listing text and graphics, trailer. |
| Audio | 8 sound clips and 2 music tracks: every theme plays the same track, no material break sounds, and the origin and licence of the existing clips and music aren't recorded. |
| Polish | Crack overlays, the phone letterbox question, tuning (scroll speeds, upgrade costs and cash rate are first guesses), a phone-first first five minutes. |
| Performance | The worst collapse was never measured on a phone; composite debris (design doc Phase 2) isn't built. |
| Marketing | Nothing yet: no accounts, clips, press kit or community. Plan: `marketing.md`. |

---

## 3. What v1.0 is

**v1.0 is the Google Play release for Android phones and tablets (landscape), in January
2027** (D20).

**In scope**
- Everything in §2.1: the campaign (3 themes × 4 levels), the daily seed, the tutorial, the
  skill shop, solo and local co-op (a gamepad per player on a phone or tablet).
- **Online co-op at launch** (D4): two players on two devices through the VPS relay with a
  join code. The WebGL build on itch.io joins the same games, so a friend without the app
  can play along in a browser.
- Ads (an interstitial between runs, the rewarded 2x cash) with consent.
- The audio, polish and performance work in R2.

**Out of v1 (updates after launch, §4 After launch)**
- The remove-ads purchase, a daily-challenge leaderboard, Theme 4 and more from the content
  pipeline (M5), endless mode, a level editor.
- Other stores and platforms: iOS (needs a Mac and $99 a year), an Android TV listing, PC on
  itch.io or Steam. The code already runs there; the release work for them waits.

---

## 4. Milestones

Sizes are relative: **S** ≈ a few sessions, **M** ≈ 1–3 weeks part-time, **L** ≈ 3–6 weeks.
The art-pipeline tooling in M4 is Python-only, so it can **run in parallel** with M2 and M3.

```
M0 ✅ ─▶ M1 ✅ ─▶ M2 ✅ ─▶ M3 ✅ ─▶ M6 (the rest folded into R1–R2)      M4 ✅ (in parallel)

R1 Android build ─▶ R2 Release candidate ───────────────────────▶ R5 Launch ─▶ updates (v1.x, M5)
  20 Oct–1 Nov        2 Nov–13 Dec                                  week of 11 Jan 2027
                  └▶ R3 Closed test (from 16 Nov, 14+ days) ─▶ R4 Store listing + run-up (Dec–Jan)
```

The release milestones R1–R5 follow M6 below; who does what is marked **You** (only the
account holder can) and **Agent**.

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
- ✅ **Tutorial** rebuilt: `TutorialPrompt` markers each start a stretch whose sign travels
  over the front player and then stays at the next marker as a boundary post (move/jump,
  smash + axe, supports, dynamite, keep up, finish), with the buttons of the devices in use. An axe and dynamite sit where the hints teach them, the camera
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
- ✅ `LevelSpec` (JSON), `LevelGrammar` (pacing tokens: start, street, cluster:n,
  islands:n, tower, gap, hill, plateau, sewer, gate, breather, finish), `LevelLayout`, `TraversalValidator` (replays
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
**Neon**, 17 sprites each, 4 archetypes and 4 levels each. Since then: street props and
dressing, back walls, and one SpriteAtlas per theme written before every build
(`Editor/Atlases/ThemeAtlasPrebuild`); the legacy pixel-art textures import point-filtered and
uncompressed like the generated ones. Not yet: autotile edge variants.

Art and level review, 2026-10-17 (`content-work/levels/*.png`, drawn headless by
`tools/artpipe/level_preview.py`):
- Generated buildings showed the sky through every room, so they read as scaffolding.
  Now each theme has a back wall (`interior`, `interior_top` with the ceiling's shadow)
  that `BuildingInterior` draws behind open and see-through cells; a piece crumbles when
  the block cell it hangs from goes.
- Town's tileset borrowed object-set sprites that didn't fit: pre-cracked rebar slabs as
  concrete, a pole as the door, a log as the beam, a brick corner as the chimney, darkened
  brick behind the glass. The pipeline now draws those (plus wood siding, floors, the room
  behind the glass and the back wall) in the object set's ramps; the red brick, brown
  trusses, glass columns and supports stay hand-drawn.
- Levels were mostly flat street with 4–9 buildings: long walks, buildings always on the
  street. New segments `islands:n` (a pit and a landing strip between buildings) and
  `plateau` (a building on high ground); weapons spawn just before a building; every
  campaign level uses them (more buildings and cells in most levels).
- Street dressing (same day): clusters are terraces (wall to wall); breakable props
  (crate piles, cars, rows of gas tanks that set each other off, Port containers) and
  cosmetic decor (lamp posts, fences that join neighbouring buildings, hedges / tyres /
  bags, bins, signs), all generated per theme.
- Still open: set pieces (cranes, billboards), a sky gradient, Town's near-black ground
  fill, theme-specific archetypes.

Original plan (design in §6):
- Style-bible schema, procedural tile generator, normalizer, QA and contact sheets.
- Unity `AssetPostprocessor` enforcing import settings.
- Proof: **re-skin Theme 1 (Town)** from the existing art as the anchor, then make
  **Theme 2** with the pipeline.
- **Done when:** a Theme 2 tile set, props and parallax background pass QA and look like
  the same game as the demolition man sprite.

### M5 — Agentic content pipeline (L) — roles written 2026-10-17; moved after launch
Not needed for v1: the campaign's 12 levels exist. After launch it makes the content
updates, Theme 4 first (§4 After launch).

The five roles exist as Claude Code subagents in `.claude/agents/` (theme-director,
architect, artist, level-designer, integrator), each ending in validators it can't argue
with (`tools/unity-check/check.sh`, `tools/artpipe/qa.py`). Not yet run end to end on a
Theme 4 pitch, and no workflow script chains them.

Original plan (design in §7):
- Agent roles, artifacts, validators runnable from the command line, human gates.
- Run it end to end to produce **Theme 3** with almost no hand work. Then use it to fill
  the 3 × 3–4 campaign.
- **Done when:** one pitch sentence becomes a playable, reviewed theme PR.

### M6 — Meta, juice, ship (M/L) — partly built 2026-10-17; the rest folded into R1–R2
Built: JSON save in `persistentDataPath` (`Meta/`), unlocks in campaign order, the **skill
shop** (power, hard hat, boots, TNT belt, tool grip; cash = score / 4, x1.25 for a
finish), settings (volume, music, screen shake, HUD size, touch controls mode/size/opacity),
code-built pause menu, results with cash and best-result badges, screen shake by collapse
size, hit-stop, dust, pooled particles. **Decision change:** the level select is a menu
overlay (`LevelSelectView`) instead of walking the hub — it has to work with a thumb on a
phone and from a sofa with a gamepad; the hub scene stays for a later "home base".
Still open from the list below: crack-overlay damage states, material break sounds and a
music track per theme, composite debris, builds and store assets (all in R1–R2). Rebinding
and resolution settings are PC-only and wait for a PC release.

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

### R1 — Android build on your phone (S) — 20 Oct – 1 Nov 2026
**You**
- Create the Play Console account (personal, $25) and finish the identity verification; it
  can take a few days.
- Choose the **package name** (e.g. `com.<you>.demolitionmen`) and the developer name (D22).
  The package name can never change after the first upload.
- Create an upload keystore and keep two backups. Play App Signing holds the real signing
  key; a lost upload key means a key-reset request before the next update.
- Install Android Build Support with SDK platform 36 through Unity Hub.

**Agent**
- Player Settings: package name, company, version 1.0.0 with a versionCode raised on every
  build, target API 36, minimum API 25 (as now), application category **Game** (Android 16
  leaves games out of its large-screen orientation override, so landscape stays locked on
  tablets), Internet Access *Require*, IL2CPP ARM64 (already set).
- Size: R8 minify for release, managed stripping *Medium*, unused packages out of the build
  (ProBuilder, AI Navigation, Sprite Shape, PSD Importer, once checked that nothing uses
  them). Aim for a download under ~50 MB.
- An adaptive app icon from the art pipeline; the splash screen without the Unity logo
  (allowed on Unity Personal since Unity 6).
- A build entry point (Tools ▸ Demolition ▸ Build ▸ Android AAB, plus a batch method) that
  reads the keystore passwords from environment variables and writes the AAB.
- Save the profile on `OnApplicationPause`: Android can kill a backgrounded app without
  quitting it.

**You** first: the URP editor steps in `rendering.md` (D26), so the profiling below measures
the renderer that ships, with Settings ▸ EFFECTS on and off.

**Done when:** the AAB installs from the Play **internal testing** track on your phone and
plays a full level, and the worst collapse holds ~60 fps in a development build (Profiler
over USB), or the cheapest fix is known (a lower falling cap on phones, composite debris).

### R2 — Release candidate (M) — 2 Nov – 13 Dec 2026
Ads and the privacy policy come first: the closed test (R3) can't start without the store
declarations that depend on them.
1. **Ads and consent** (first week): the Google Mobile Ads (AdMob) Unity plugin with its UMP
   consent form (D23), an AdMob adapter behind `IAdService`, test ads in development builds,
   a **Privacy options** button in Settings for EEA/UK players. Steps: `monetization.md`.
2. **Privacy policy** page (on the VPS next to the relay, or GitHub Pages): what the ad SDK
   collects, consent, the relay seeing IP addresses, no accounts, the save file staying on
   the device, a contact address.
3. **Online**, O2–O4 in `multiplayer.md`:
   - play tests between two phones and between a phone and a PC;
   - the WebGL build on itch.io, and a phone on mobile data, a PC and a browser tab joining
     each other through the relay already on the VPS;
   - a backgrounded or locked phone, timeouts, the relay down;
   - a **version check**, so a store build and an older itch build refuse each other with a
     message instead of desyncing.
4. **Audio**: break sounds per material, a collapse rumble, prop and gas-tank blasts, a music
   track per theme. Use CC0 or bought licences and record each source and licence in a
   credits list; replace anything whose origin is unknown. A credits screen in Settings
   (it also names the Black Ops One font, SIL OFL).
5. **Feel and the first five minutes on a phone**:
   - crack overlays (three stages, from the art pipeline);
   - decide the phone letterbox (`overnight-2026-10-17.md`, question 2);
   - tune scroll speeds, upgrade costs and the cash rate from closed-test feedback;
   - touch glyphs in the tutorial prompts;
   - from app start to the first falling building in under a minute.
6. **Clip mode** for marketing, development builds only: hide the HUD, 9:16 framing, slow
   motion on a key (`marketing.md` §2).
7. **Pre-launch report**: Play runs every closed-track upload on real devices for free. Fix
   every crash it finds.

**Done when:** a release-signed AAB with test ads, consent and online play through the VPS
passes the pre-launch report with no crashes, and closed testers finish the campaign.

### R3 — Closed test (12+ testers × 14 days) — start by 16 Nov 2026
- Before the first closed release, Play wants the app content declarations: privacy policy
  URL, ads, content rating (IARC questionnaire), target audience (13+, so not the Families
  program), Data safety, app access, and a basic store listing. Do them in the week of
  9 Nov; they can be improved later.
- **You:** recruit 20+ testers (friends, family, colleagues, the tester call in
  `marketing.md`). 12 must stay opted in for **14 days in a row**; the spares cover
  drop-outs. Prefer people who will play: when you apply for production, Google asks what
  testers did and what you changed.
- Ship updates during the test. Ask testers three things: what was confusing, what was fun,
  would you keep it installed.
- **You:** after 14 days, apply for production access on the Play Console dashboard. Google
  says the review usually takes up to 7 days. Target: access by about 10 Dec.

**Done when:** production access is granted.

### R4 — Store listing and run-up — 1 Dec 2026 to launch
- The listing: title (30 characters), short description (80), full description, icon
  512×512, feature graphic 1024×500, 4–8 landscape screenshots with a caption each, and a
  30–60 s trailer on YouTube. The art pipeline and the clip mode make most of these.
- **You:** EU trader status. An app with ads counts as a trader under the Digital Services
  Act, so Play shows the developer's address, e-mail and phone number on the store page.
  Settle which address may be public (a business address or a P.O. box service) before you
  declare (D24).
- **Pre-registration** as soon as production access is granted: a "notify me" page that
  every clip and post can link to before launch.
- The press kit page and the outreach list (`marketing.md`).

**Done when:** pre-registration is live with the trailer, and the press kit is up.

### R5 — Launch — week of 11 Jan 2027
- Production release with a **staged rollout** (20% → 50% → 100% over a few days) while
  watching Android vitals: Play shows apps less when the user-perceived crash rate is over
  1.09% or the ANR rate over 0.47%.
- Launch week as in `marketing.md`: posts, press e-mails, the small paid push.
- Reply to every review in the first month; fix-only updates go out within days.

**Done when:** the rollout is at 100% with vitals under those thresholds.

### After launch (v1.x)
An update every 2–4 weeks keeps the listing fresh and gives the clips something new to
show:
- **v1.1:** fixes from launch; the **remove-ads purchase** (Unity IAP on Play Billing, needs a
  payments profile); a share button that sends a join code with the store and itch links.
- **v1.2:** **Theme 4** through the content pipeline (M5), first run end to end on one pitch.
- A daily-challenge leaderboard (Play Games Services is free), store listings translated for
  the top countries.
- Later: an Android TV listing, PC on itch.io or Steam, iOS, endless mode, a level editor.

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
- **Segment library:** `start`, `street`, `cluster:n` (n buildings), `islands:n` (n
  buildings with a pit and a landing strip between each), `gap` (jump), `tower`
  (vertical climb, camera zooms), `hill`, `plateau` (a building on high ground), `sewer`
  (camera rail dips), `gate` (a wall that must be broken to pass: forced destruction),
  `breather`, `finish`. Not built: `hazard-yard`.
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
| D4 | Online multiplayer in v1? | **Decided (2026-10-20):** yes, online co-op ships with the Android v1.0 (R2), on Netcode for GameObjects (NGO), not Fishnet. Was post-v1 until then. Plan and its decisions (D11–D19): `docs/multiplayer.md`. |
| D5 | Campaign levels: baked scenes or generated at load? | **Generated at load from `{spec, seed}`.** Bake only if a level needs hand polish. |
| D6 | Skill shop / upgrades in v1? | **Decided:** yes, in v1 (M6). |
| D7 | Art anchor | Keep the existing demolition man and object set as Theme 1 (Town) and as the reference for every generated theme. |
| D8 | Image generator | Unity AI first (already in the project and the MCP). Check commercial terms before shipping its output. |
| D9 | Target platform | **Decided (2026-10-17):** PC (Windows, Steam Deck), TV (gamepads, couch co-op) and phones/tablets in landscape (touch). WebGL (browser) added later the same day, online play included (`docs/multiplayer.md` D19). One build of the UI for all: code-built uGUI through `UiFactory`, safe area, HUD size setting. |
| D10 | Business model | **Decided (2026-10-17):** free, with ads on phones only (interstitials between runs, opt-in rewarded "2x cash"); a "remove ads" purchase later. See `docs/monetization.md`. |
| D20 | Release target | **Decided (2026-10-20):** Google Play first, week of 11 Jan 2027, from a new personal developer account (so a closed test first, R3). Other platforms after launch. |
| D21 | Marketing budget | **Decided (2026-10-20):** €100–500 for launch and the first months. Plan: `marketing.md`. |
| D22 | Package name and developer name | Needed in R1; the package name is permanent. Suggested shape: `com.<developer>.demolitionmen`. |
| D23 | Ad network | AdMob directly, with its UMP consent form: one SDK, consent included and Google-certified for the EEA/UK. LevelPlay (the earlier pick) mediates more networks but needs a consent tool on top; mediation can come later through AdMob. |
| D24 | Public trader details | Before declaring trader status (R4): which address and phone number may be shown on the store page. |
| D25 | Game name | Search the Play Store and the EU/US trademark registers for the name before the listing goes up; renaming after marketing has started loses the audience built so far. A subtitle in the title helps search (e.g. "Demolition Men: Wreck Co-op"). |
| D26 | Render pipeline | **Decided (2026-10-21):** URP with the 2D Renderer, before R1 profiling: 2D lights in the dark themes and bloom/vignette, with an EFFECTS setting for slow phones. Code in `Demolition.Lighting`; editor steps in `rendering.md`. |

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
| Fewer than 12 testers stay the full 14 days (R3) | Recruit 20+, remind them weekly, start by 16 Nov so a restart still fits before January. |
| Production access refused or slow | Apply with real tester feedback and the changes made; about four weeks of buffer between the target (10 Dec) and launch. |
| Online breaks on mobile networks | Test on mobile data early in R2. The game is complete offline, so a late online problem can ship as "online co-op in the next update" without moving the launch. |
| A low-end phone can't hold the frame rate | Profile in R1; a falling cap per platform, composite debris; exclude devices below a RAM floor in Play's device catalog. |
| A small audience | Organic clips first, paid ads only after the listing converts (`marketing.md`). It is a side project: scale the weekly rhythm to the hours you have. |
| Ads trip a policy review | Test ads only in development builds, never tap your own ads, interstitials only between runs, the ads declaration filled in. |

---

## 10. Next five tasks (2026-10-20)

1. **You:** create the Play Console account and start the identity verification; pick the
   package name (D22).
2. **Agent:** the R1 Player Settings, app icon and build entry point; **you** build, upload
   to internal testing and install it on your phone.
3. **You + agent:** profile the worst collapse on your phone and fix it if it drops frames.
4. **Agent:** the AdMob + UMP adapter, the Privacy options button and a privacy policy draft
   (R2 steps 1–2), so the R3 declarations can be filled in by 13 Nov.
5. **You:** open the marketing accounts and post the first clips; post the tester call
   (`marketing.md` §5).
