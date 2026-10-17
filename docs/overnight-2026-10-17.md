# Overnight build — 2026-10-17

Branch `claude/great-maxwell-inbz4w`. Written without the Unity editor: everything
compiles against Unity 6000.4 reference assemblies (runtime, editor, PlayMode tests) and
the model tests pass on .NET (`tools/unity-check/check.sh`). **Nothing has been run in
Unity yet** — the checklist below is the first playtest.

## What changed

| Area | What you get | Where |
|---|---|---|
| Generated levels (M3) | Level spec JSON + seed → ground, buildings, pickups, finish; traversal validator replays the jump; segments stream in and out. A new **Run** scene builds the selected level from `Resources/GameConfig`. Campaign of 12 levels (Town, Port, Neon × 4) + a daily seed. Generation takes 3–35 ms per level on desktop .NET. | `Scripts/Demolition/Level/`, `Content/*/Levels/`, `Scenes/Run.unity` |
| Art pipeline (M4) + themes | `tools/artpipe`: master palette, procedural tile generators, normaliser, QA + contact sheets, `unity_theme.py` writing tiles/tileset/theme assets. **Port** (rusty docks) and **Neon** (night downtown), 17 sprites each, 4 archetypes + 4 levels each. Import rules via `ContentSpriteImporter`. | `tools/artpipe`, `Content/Port`, `Content/Neon`, `content-work/*/contact.png` |
| Content agents (M5) | Theme director, architect, artist, level designer, integrator as Claude Code subagents ending in hard validators. Not run end to end yet. | `.claude/agents/` |
| Phones (landscape) | Touch controls (floating stick, JUMP / HIT / TNT / SWAP, pause), safe area, HUD size (130% on phones), touch tutorial wording, landscape only, 60 fps, IL2CPP ARM64. | `Input/`, `Common/`, `RunHud`, ProjectSettings |
| TV / PC | Every menu works with gamepad navigation, mouse and touch; Esc / B closes overlays; pause on Start / Esc / Android back. | `Meta/View/`, `Run/View/PauseView` |
| Progression (M6) | Profile save, cash per run (score / 4, x1.25 for finishing), unlocks in campaign order, best grade/score/time, skill shop (5 upgrades), settings, level select replacing the tutorial prompt. First Play on a new profile goes to training. | `Meta/` |
| Ads | Policy (no ads in the first 4 runs, every 3 runs, 3 min apart), interstitial between runs, opt-in "2x cash" rewarded ad, simulated ads in development phone builds. No SDK yet. | `Ads/`, `docs/monetization.md` |
| Juice | Screen shake by collapse size, hit-stop on big collapses, dust, crack overlays, synthesised crunch/clang/rumble sounds, pooled particles. | `Run/View/RunJuice`, `RunAudio`, `BuildingBlock` |
| Tooling | Bot playtester + PlayMode test over all levels, Level Lab window, editor-free compile + test scripts, YAML reference checker. | `BotPilot`, `Tests/PlayMode`, `Editor/Generation/LevelLab`, `tools/unity-check` |
| Netcode prep | Players act on input frames from sources; a level is `{specId, seed}`. | design doc §5 |

## Playtest checklist (in this order)

1. **Open the project.** Unity imports the new scripts/assets. Check the Console for
   errors, especially missing script or broken references on `Run.prefab`,
   `Resources/GameConfig.asset`, `Content/Town/TownTheme.asset`, `Scenes/Run.unity`.
2. **EditMode tests** (Test Runner): all green.
3. **Level Lab** (Tools ▸ Demolition ▸ Level Lab): pick `town-01`, look at the preview,
   *Play*. Expect: street ground, a few buildings, pickups, finish banner; HUD; 3-2-1;
   the camera starts scrolling when you push right. Then *Play with bot*.
4. **PlayMode test** `BotRunTests` — does the bot finish all 12 levels? If not, the
   failure message says where it got stuck. Then Level Lab on `port-01` and `neon-01`:
   do the generated Port/Neon sprites look right in game (compare with
   `content-work/<Theme>/contact.png`)?
5. **Main menu flow** (open `MainMenu`, Play): join with Space / A, Start → (first time)
   training yard, otherwise level select → a level → results → Next / Retry / Menu → back
   at the level select. Shop: buy something, check it applies (e.g. health bar length).
6. **Phone flow:** Game view ▸ Simulator (any landscape phone). Play → touch auto-joins →
   START. Try the stick, HIT, JUMP (tap vs hold), TNT, SWAP, pause button. Check nothing
   hides behind the notch and the HUD text is readable.
7. **Settings:** volume, music, shake on/off, HUD size, touch controls mode/size/opacity.
8. **Feel:** shake and hit-stop strength (`RunJuice` on the Run prefab), sound levels
   (`RunAudio.volume`), crack overlays.

## How it was checked

- `tools/unity-check/check.sh`: Demolition, Assembly-CSharp, editor scripts, the
  `UNITY_EDITOR` build and PlayMode tests compile; 171 EditMode model tests pass (buildings
  and levels of all three themes over many seeds, save/shop/ads policy, input sources, sounds).
- `tools/unity-check/refs.py --since 52d7051`: every GUID and fileID in the hand-edited
  scenes, prefabs and assets resolves.
- Two review passes by a separate agent read the code and YAML for runtime-only bugs.
  The first found 12 issues, all fixed in `bbd4eeb`; the important one was that
  Touchscreen was missing from the Input System's supported devices, so nothing touch would
  have worked on a phone. Results of the second pass are in the commits that follow it.

## Not verified / likely to need a tweak

- Ground alignment: generated buildings stand on the Town ground tiles (34 px sprites at
  32 PPU, like the hand-made parts). If buildings float or sink by a pixel, adjust
  `LevelBuilder` or the tiles.
- Camera framing in the Run scene: placed so the street sits at the bottom with 0.5 units
  of ground showing, like Level 1. The letterboxed camera (bars top and bottom) is kept on
  phones too — see open question 2.
- Collapsed buildings leave rubble you climb or punch through (unchanged); in long
  clusters that can slow the bot. Gates crumble to dust instead.
- Upgrade costs, cash rate and scroll speeds are first guesses.
- `BotPilot` is simple (run, hit, jump, throw TNT at supports).
- The scene `PauseMenu` canvases in Level 1 / HubWorld / Tutorial are dead UI now (their
  buttons pointed at nothing already); the code-built pause menu replaced them.
- Port and Neon reuse the Town parallax backdrop (tinted) and the one gameplay music track.
- Unity will rewrite the generated sprites' `.meta` files on first import
  (`ContentSpriteImporter` sets full-rect meshes); commit that diff.

## Decisions I made for you (easy to reverse)

- **Level select is a menu overlay**, not the walkable hub (thumb- and sofa-friendly). The
  hub scene and Level 1 stay in the project but out of the build.
- **Free with ads on phones only**, never during a run; rewarded ad doubles the run's cash.
- **Par time = auto-scroll time** from spawn to finish (a spec can override).
- Product name set to "Demolition Men"; portrait orientations disabled; Android IL2CPP ARM64.

## Open questions for you

1. Company name and bundle id (`com.<company>.demolitionmen`)? Needed for store builds.
2. Keep the cinematic letterbox on phones, or full-screen the camera there (more vertical
   room on wide phones)?
3. Which ad network: Unity LevelPlay (recommended, mediates AdMob too) or AdMob directly?
4. Should the daily challenge have its own leaderboard later (needs a backend)?
5. Upgrade pacing: a first upgrade after one or two runs, ~40 runs to max everything (estimated from level sizes, not measured). Faster or slower?
