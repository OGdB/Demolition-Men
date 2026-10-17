# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository layout

- `Demolition Men/` — the Unity project (Unity **6000.4.9f1**, 2D, built-in render pipeline). All code lives under `Demolition Men/Assets/`.
- `docs/building-destruction-design.md` — design rationale for the destruction system ("Option C" support-graph), the NGO (Netcode for GameObjects) networking plan, and the PlayMode manual test checklist.
- `docs/roadmap.md` — the plan to v1.0: current-state gap analysis, milestones M0–M6, the procedural building/level generation design, the art pipeline and the agentic content pipeline, plus the design decisions. Check it before starting feature work.
- `docs/multiplayer.md` — the online co-op plan: player-hosted NGO over a self-hosted WebSocket relay on the VPS (no game server, no paid services, works in WebGL), who decides what, every message and its size, milestones O1–O5, what the VPS is for.
- `docs/monetization.md` — the ads seam (free game with ads on phones), placements, wiring a real ad network, store/consent checklist.
- `tools/unity-check/` — compile-check and model tests without the Unity editor (see Commands).
- `server/relay/` — the online co-op WebSocket relay for the VPS (ASP.NET Core, .NET 10, no game code) with its tests (`dotnet test Relay.Tests`) and `deploy/deploy.sh <ssh-target> <domain>`; see its README.
- The `*.csproj` / `*.slnx` files and `Library/`, `Temp/`, `Logs/`, `UserSettings/` are Unity-generated or per-user and gitignored. Unity regenerates `Demolition Men.slnx` (the four project assemblies) when the editor opens the project.

## Commands

All paths below are relative to the repo root.

**Without the editor (cloud agents, CI, Linux):** `tools/unity-check/setup.sh` once, then
```
tools/unity-check/check.sh          # compile all assemblies + run the EditMode model tests
tools/unity-check/check.sh build    # compile only
tools/unity-check/check.sh test -- --where "class == Demolition.Tests.MetaTests"
tools/unity-check/refs.py --since <rev>   # hand-edited scenes/prefabs/assets: unresolved GUIDs, dangling fileIDs
tools/unity-check/check.sh levels && python3 tools/artpipe/level_preview.py   # every level drawn with its theme's art: content-work/levels/*.png
```
It compiles `Demolition`, `Demolition.Online` (`Online/Controller/`, its own asmdef), `Assembly-CSharp`, `Assets/Editor` (against an older UnityEditor.dll, so a brand-new editor API can false-fail), the `Demolition` assembly with `UNITY_EDITOR` defined, and the PlayMode tests, against Unity 6000.4 reference assemblies; then runs the EditMode tests on plain .NET with `UnityShim.cs` standing in for the UnityEngine value types. Only code under `*/Model/` (minus `Session.cs`) is in the test build, so models must not touch scenes, MonoBehaviours or devices. The registry's uGUI it compiles renamed `TMP_FontAsset`; `TmpCompat.cs` puts the editor-bundled name back (compile only). Netcode for GameObjects 2.13.3 (with Transport, Collections, Burst, Mathematics) and NativeWebSocket are built from source too, unpatched; package versions and the defines mirrored from each asmdef live in `setup.sh` and `projects/*.csproj`. New `.cs` files are picked up automatically (globs), but **every new asset needs its `.meta`** — Unity is not there to make one.

**Compile check (fast, works while the editor is open):**
```
dotnet build "Demolition Men/Demolition Men.slnx"
```
The generated csproj files list sources explicitly (`<Compile Include>`), so a newly created `.cs` file is not compiled by this until Unity regenerates the projects (focus the editor, or Preferences ▸ External Tools ▸ Regenerate project files). Output goes to `Temp/obj`.

**EditMode tests** (`Assets/Tests/EditMode/`, assembly `Demolition.Tests.EditMode`: `BuildingStructure`, `RunState`, `RunScore`, building and level generation, meta/ads/input models):
- With the editor open: Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All. Batch mode refuses to run while the editor holds `Temp/UnityLockfile`. From an agent, the Unity MCP `RunCommand` can start `TestRunnerApi` with an `ICallbacks` that writes results to `Temp/` (the callback class must be top-level, not nested; `System.Reflection` is blocked).
- With the editor closed (PowerShell):
  ```
  & "C:\Program Files\Unity\Hub\Editor\6000.4.9f1\Editor\Unity.exe" -batchmode -projectPath "Demolition Men" -runTests -testPlatform EditMode -testResults "$env:TEMP\editmode.xml" -logFile -
  ```
  Single test: add `-testFilter "Demolition.Tests.BuildingStructureTests.RemoveMidColumn_DetachesEverythingAbove"`. Don't pass `-quit` with `-runTests`.

**PlayMode tests** (`Assets/Tests/PlayMode/`): `BotRunTests` plays every campaign level to the finish with the `BotPilot` at 3x speed (minutes; editor only).

Only the pure models have fast automated tests; everything physics/visual is verified manually in `Assets/Scenes/DemolitionTestBranch.unity` (a `DemoBootstrap` test bench that builds ground, a building, player, camera and HUD entirely from code) or in a generated level (Tools ▸ Demolition ▸ Level Lab ▸ Play / Play with bot).

Build scenes (enabled): MainMenu → Tutorial → Run. `Level 1` (hand-made parts) and `HubWorld` stay in the project but out of the build.

## Assemblies and the dependency boundary

- `Assets/Scripts/Demolition/Demolition.asmdef` → assembly **`Demolition`**: the destruction system, players, the run loop, its HUD. References `Unity.InputSystem`, `Unity.TextMeshPro`, `UnityEngine.UI`. Any other package used from this folder must be added to the asmdef's `references` or it won't compile (even though Assembly-CSharp sees it).
- Everything else in `Assets/Scripts/` (Menu, Generations) → **Assembly-CSharp**, which references `Demolition` — never the reverse. Menu choices reach the run through `Demolition.Run.Session` (joined devices) and `Demolition.Levels.LevelSelection` (which level).
- Feature folders inside `Demolition`: `Building`, `Generation` (buildings), `Level` (levels, M3), `Run`, `Player`, `Input`, `Pickup`, `Meta` (profile, shop, settings, level select), `Ads`, `Common`, `Sandbox`.

Players are located at runtime **by GameObject name** `"Player_1"` / `"Player_2"` (`RunController`, `TerrainGenerator`) — scene references to players are not serialized, so keep those names.

## Run loop

`Scripts/Demolition/Run/`, one `Prefabs/Run/Run.prefab` per level (placed in Tutorial; instantiated by `LevelBootstrap` in the Run scene). The prefab carries `RunController`, `RunHud`, `ResultsView`, `PauseController` + `PauseView`, `TouchControls` and `RunJuice`:
- **Model:** `RunState` (countdown → playing → finished/failed; out-of-bounds grace, co-op free rejoin then Resurrect, finish + last call), `RunScore` (material points, collapse chain multiplier, speed bonus vs par, grade by level-wide destruction; players see the grade as 0–3 stars at 40/60/80%, finished runs only, saved as `LevelRecord.bestGrade`), `Session`.
- **Controller:** `RunController` finds the players, feeds the models (viewport out-of-bounds, finish crossing, health depletion, `DestructionEvents`) and applies their decisions (`PlayerHealth.Kill/Revive`, controls on/off, camera targets). `FinishLine` marks the finish (in `levelPart_End`; placed by hand in Tutorial). `TutorialPrompt` markers show one hint each as the front player passes them. `nextScene` on the run enables the results screen's NEXT.
- **View:** `ScrollCamera` on `Main Camera 1` (push/auto-scroll, ground-anchored zoom framing that lowers to show floors below the street in view or just ahead via `LowestGround`, letterbox clear, applies `CameraShake`), `RunHud` (its demolition meter lights a star at each threshold) and `ResultsView` (uGUI built in code through `UiFactory`; a skippable reveal: title slam, meter fill lighting up to 3 stars with chimes, count-ups, record stamp; NEXT / RETRY / SHOP / MENU, the shop opening over the results; the rewarded "2x cash"), `PauseView`, `RunJuice` (shake by collapse size, hit-stop, dust), `FinishLineView`.
- **End of run:** `RunController.Bank()` records the run in the profile (`SaveData.RecordRun`: cash, best grade/score/time, first clear) once the phase is Finished/Failed. `LevelId` is the spec id for generated levels, else the scene name.
- Pickups: `Scripts/Demolition/Pickup/` (`Pickup`, `PickupSlot` in each level part, `PickupView`), prefab `Prefabs/Pickups/Pickup.prefab`. Resurrect calls `RunController.Current.TryResurrect`.
- Score attribution: `TakeDamage(amount, instigator)` carries the player index (`PlayerInputReader.PlayerIndex`, assigned by the run). Collapsed blocks are credited to whoever destroyed the support; blocks credited once never score again. `PlayerHealth.ManagedByRun` makes depletion raise `Depleted` instead of the test-bench respawn.

## Levels (M3)

`Scripts/Demolition/Level/` (roadmap §5.2), content in `Assets/Content/<Theme>/Levels/*.json`:
- **Model:** `LevelSpec` (JSON: id, name, theme, seed, scrollSpeed, parTime 0 = auto, pacing tokens `start street cluster:n islands:n tower gap hill plateau sewer gate breather finish` (`cluster:n` is a terrace, wall to wall; `islands:n`: a pit and a landing strip between buildings; `plateau`: a building on high ground), pickup budget (weapons spawn just before a building), allowed archetypes, `props` (breakable street props: crate, car, barrel, container; empty = the first three)) → `LevelGrammar.Generate` → `LevelLayout` (ground height per column, `NoGround` = pit, building placements each a validated `BuildingSpec`, pickups, spawn, finish, segments). Street dressing comes last from its own random stream (it never changes the course): props are tiny one-block buildings in `LevelLayout.Buildings` (`BuildingPlacement.IsProp`, archetype `prop.*`; damage, score, the meter and online sync work as for buildings; gas tanks go off through `Dynamite.Detonate` with `GameConfig.dynamitePrefab`, cosmetic on an online client), kept off doorsteps, the start and the finish; `LevelLayout.Decor` is cosmetic (`DecorKind` lamp, fence, bush, bin, sign; a fence along short stretches between buildings), drawn by `LevelBuilder` from the theme's `decor` sprites at sorting order -1. `TraversalValidator` replays the player's jump (`JumpProfile` = `PlayerMotor` + prefab Rigidbody2D tuning) and rejects steps/pits a player can't clear; the grammar re-rolls derived seeds until it passes. `Daily` = one seed per date. Pcg32 only — same spec, same level.
- **Controller:** `LevelBootstrap` (the Run scene's only object) builds the selected level: camera, backdrop, players named Player_1/Player_2, `LevelBuilder`, the Run prefab (`Configure(id, par, plannedCells, next)`), music. `LevelBuilder` lays the ground tilemap once (a lit back wall behind trenches, near-black below pit lips plus hazard-striped pit edges, so a drop with a floor never looks like a pit) and streams segments (buildings via `BuildingBuilder`, pickups, finish) in ahead of the camera and out behind it. `LevelSelection` holds the chosen campaign level or daily across the scene load. `GameConfig` (`Resources/GameConfig.asset`) lists prefabs, scene names and the campaign's `LevelTheme`s in order; a theme (`Content/<Theme>/<Theme>Theme.asset`) has ground tiles, building tileset, archetypes, levels, sky, music.
- Gates (`gate` token) use `DestructibleBuilding.CrumbleDetached`: knock out the wooden footing and the wall turns to dust instead of piling up in the street.
- Tools ▸ Demolition ▸ Level Lab previews a spec + seed, validates seeds, plays it (optionally with the bot); `LevelLab.ValidateAllCli` for batch mode.

## Meta, menus and ads

- `Meta/`: `SaveData` (model: cash, `LevelRecord` per level id, upgrades, `GameSettings`, ad counters) persisted by `Profile` (JSON in `persistentDataPath/profile.json`). `UpgradeCatalog`/`Economy` (models) define the skill shop; `PlayerUpgrades.Apply` sets damage, health, speed, starting TNT and weapon hits when the run finds its players. `AppBootstrap` (`RuntimeInitializeOnLoadMethod`) sets up phones (landscape, 60 fps) and applies settings on every scene load; music is any scene object named `BackGroundMusic`.
- Views built in code: `LevelSelectView` (one column per theme, stars and best score per level, unlocks in campaign order, training, daily, shop, settings), `ShopView` (from the level select and the results screen; `Open(parent, closed)`), `SettingsView` (also from the pause menu). Overlays close with Esc / B; `PauseController` ignores Esc while settings are open.
- Menu flow (`Scripts/Menu/`): `MainMenu` plays `TitleView` (the name slams into the middle and rises to the top; any press skips), then `JoinScreen` opens by itself (phones join the touchscreen automatically; START button) → `LevelSelectView` (BACK returns to the lobby, players kept) → Run scene. No quit button: B / Esc from a device that hasn't joined (Android back on a phone) opens `QuitView`. Back from a run (`LevelSelection.ReturnToLevelSelect`) reopens the level select under the risen title.
- Online (`docs/multiplayer.md`, O1 done, O2 code in): `Common/Controller/NetRole` is Offline / Host / Client plus the hooks the `Demolition.Online` assembly (`Online/Controller/`, NGO 2.13 + NativeWebSocket) fills in; offline nothing changes. `OnlineSession` adds the NetworkManager at runtime with `RelayTransport` (WebSocket to the relay at `GameConfig.relayUrl`); `RunNet` sends named messages (cells, damage, pickups, dynamite, a 10 Hz `RunState` snapshot); `NetPlayer` on `Prefabs/Online/NetPlayer_1/2` (made by Tools ▸ Demolition ▸ Online ▸ Build Network Prefabs) syncs pose, health and kit (weapon, hits, dynamite). The client buffers cells of buildings it hasn't streamed in yet; `Pickup.WasTaken` keeps taken pickups from streaming back. Player 2 leaving mid-run: the host plays on alone (`RunState.Leave`, `RunController.PlayerLeft`). Menu: `OnlineView` from the lobby's ONLINE button. A client forwards damage to standing blocks (`NetRole.RequestDamage`) and takes the host's health through `BuildingBlock.ApplyNetHealth`; the host reports changed cells (`BlockHealthChanged`). `DestructibleBuilding.NetIndex` / `Pickup.NetIndex` are indices in `LevelLayout`. `PlayerHealth.MakeProxy` turns the other device's player into a kinematic puppet; `Dynamite.Cosmetic` damages nothing. A client's `RunController` doesn't tick or score; pause online keeps `timeScale` at 1; no ads while connected. `Online/Model/NetPacking` packs health (7 bits + instigator), pose and cell indices.
- `Ads/`: `AdPolicy` (model), `IAdService`, `AdManager` (`BetweenRuns`, rewarded double cash), `NoAdService`, `SimulatedAdService` (development phone builds). No SDK yet: see `docs/monetization.md`.

## Destruction architecture (support-graph)

Three layers; read `docs/building-destruction-design.md` for the why.

1. **`Building/Model/BuildingStructure.cs`** — pure C#, no MonoBehaviour/physics. Cells on an integer grid, anchors, 4-neighbour flood-fill from anchors. `RemoveCells()` returns every cell that lost its path to ground. `ComputeStress()` is a two-pass heuristic (carried cells → cantilever/span/hanging, then pillar columns sum load and moment) that yields per-cell `Stress/Lean/Drift/Overload`. Fragile cells (glass) are held by neighbours but support never flows through them and they carry no load.
2. **`Building/Controller/DestructibleBuilding.cs`** — owns one structure plus the live blocks. On `OnBlockDestroyed` it removes the block's cells, calls `BeginFalling()` on detached blocks (capped by `maxSimultaneousFalling`; overflow is destroyed without simulation), then recomputes stress. A stress **baseline** captured at `Start` is subtracted so intact buildings show no strain. Supports overloaded past baseline + `overloadTolerance` take crumble damage in `Update`, through the normal `TakeDamage` path.
3. **`Building/Controller/BuildingBlock.cs`** — per-block lifecycle: **Static** while supported → **Dynamic** while falling (strips any leftover `Joint2D`, damages players it lands on, takes self-damage on hard landings, glass shatters on contact) → **Static rubble** once settled. Rubble is outside the support graph, so a block that is destroyed, vanishes or starts falling wakes the rubble resting on or wedged against it (`WakeRubbleAround`), which falls again until it re-settles.

Invariants to preserve:
- Standing blocks stay `RigidbodyType2D.Static`; only detached debris is ever dynamic. Don't add forces/joints to standing structure — this is the core perf/stability decision.
- Stress lean/sag/tremble is **cosmetic only**: it moves the `Visual` child transform holding the sprite (migrated there at `Awake`), never the root/collider. Collapse decisions come only from connectivity + overload crumble.
- Damage tint darkens RGB only and is monotonic; alpha is never touched.
- Keep structural decisions discrete and deterministic inside `BuildingStructure` — the planned NGO netcode replicates destroyed-cell sets, not rigidbodies.

### Building authoring contract (hand-made prefabs)

Building prefab roots (`Prefabs/Buildings/`, `TestBuildings/`, `Sky's Prefabs/`, `Oeds/Buildings/`, and the two `LevelPart_Sewer` parts) carry a `DestructibleBuilding` with `autoScanChildren` on. Wall segments in `Prefabs/Building Materials/` deliberately do **not** — each `BuildingBlock` belongs to its nearest ancestor `DestructibleBuilding`.

At `Start`, if nothing registered blocks in code, the building auto-scans children:
- Each block's world AABB (from collider geometry, not stale `Collider2D.bounds`) is rasterized onto a `cellSize` (1 unit) grid relative to a shared origin; multi-cell footprints (beams, platforms) occupy several cells and detach as one rigid unit.
- Foreground and larger blocks claim cells first; blocks whose **name contains "bg"** sort last. A block whose cells are all already claimed becomes a cosmetic **rider** of the overlapping host — no structural role, falls when the host goes.
- Anchors: blocks with a non-block, non-dynamic, non-trigger collider within `anchorProbeDepth` below them. If none touch ground, the lowest row is anchored.
- Cells with no path to an anchor at `Start` (a block that doesn't touch the rest) fall right away, with an editor warning naming the block, instead of hovering until the first hit.
- `BlockMaterial` (Brick/Metal/Wood/Glass/Support) sets default health; Glass registers as fragile.

Code-driven alternative (`DemoBootstrap`): call `BuildingBlock.Initialise` + `DestructibleBuilding.RegisterBlock` before the building's `Start`, which then skips the scan.

`Prefabs/Buildings/Crane.prefab` keeps its authored joints on purpose — it's a physics prop outside the destruction system.

## Building generation

`Scripts/Demolition/Generation/` (see `docs/roadmap.md` §5.1):
- `BuildingGrammar.GenerateValid(archetype, seed, out report)` → `BuildingSpec` (blocks on a cell grid, bottom-left (0,0), ground row anchored). It retries derived seeds until `BuildingValidator` says the building stands and has the archetype's weak-point count. Same seed, same building (`Pcg32`, never `UnityEngine.Random`, inside the grammar).
- Glass carries nothing, so walls with windows hang from the slab above; interior `Support` columns (walk-through triggers) are the backbone. Stairwells never cut a slab piece off from them.
- `BuildingBuilder.Build(spec, tileset, origin)` registers cells in code at runtime; in edit mode it only creates the objects, and a baked prefab auto-scans to the same cells.
- Archetypes are JSON in `Assets/Content/<Theme>/Archetypes/`; looks come from a `BuildingTileset` asset (`TownTileset`). Block visuals sit on a child `Visual`; `BuildingBlock` keeps its authored rotation/scale/offset under the stress lean.
- Back walls: `BuildingSpec.Interior()` lists the open and see-through cells inside each row's outermost blocks; `BuildingInterior` (built by `BuildingBuilder` when the tileset has an `interior` sprite) draws one cosmetic sprite per cell at sorting order -1 and destroys it when the cell it hangs from leaves the structure (`DestructibleBuilding.CellsRemoved`).
- Building Lab: Tools ▸ Demolition ▸ Building Lab. From agents: `BuildingLab.Validate(path, seeds)`, `BuildingLab.RenderContactSheet(...)` (PNG in `Temp/`), `BuildingLab.ValidateAllCli` for batch mode.
- The validator judges with `StructureDefaults` (the `DestructibleBuilding` defaults). A building whose serialized tuning differs plays differently from what the validator predicted.

## Players

`Scripts/Demolition/Player/Controller/`: `PlayerHealth`, `PlayerInputReader` (samples one `PlayerInputFrame` per frame from an `IPlayerInputSource`), `PlayerMotor` (impulse movement, better-jump gravity, swings slow the player and lock facing), `PlayerCombat` (per-weapon × per-material damage profiles, `Physics2D.OverlapCircleAll` hits, weapons only from pickups; jumping head-first into a standing block headbutts it for fist damage × `headbuttFactor`, scaled by rising speed), `Dynamite` (explosion clips and controller in `Animations/Dynamite/`; the spawned effect loops, so it is destroyed after `effectLifetime`), `BotPilot` (autopilot through a `ScriptedInputSource`). Prefabs: `Prefabs/Demolition/Player_1`, `Player_2`, `Dynamite`. The same scripts drive the primitive no-art player in `DemoBootstrap`.

Input sources (`Scripts/Demolition/Input/`): `DeviceInputSource` (keyboard + mouse and/or one gamepad — the device the join screen assigned from `Session`, or gamepad index + keyboard when a level is started directly), `TouchInputSource.Shared` (written by `TouchControls`: floating stick, JUMP/HIT/TNT/SWAP, pause; shown when `Platform.IsTouch`), `ScriptedInputSource` (bots, tests), `CombinedInputSource`. Keep gameplay reading frames, not devices: a networked session would send frames.

The original sprite art is reused through a **name contract** that fails silently if broken:
- Animator parameters: `Speed`, `InAir`, `IsPunching`, `IsAxeing`, `IsWrecking`.
- Animation-event methods: `applyDamage` (attack clips → `PlayerCombat`), `Effect` and `destroyDynamite` (`Explosion.anim` → `Dynamite`). Don't rename these methods.

## Code structure (MVC)

The project follows Model–View–Controller. Keep the structure clean: no person-named folders, no scratch files in `Assets/`, delete dead code instead of leaving it around.
- **Model** — plain C# state and rules. No MonoBehaviour, no scene access, unit-testable in EditMode. Example: `Building/Model/BuildingStructure`.
- **View** — MonoBehaviours that only present: sprites, animation, HUD, particles, audio, camera framing. They read models or listen to events and never change game state.
- **Controller** — MonoBehaviours that turn input and events into model changes and tell views what to show. Example: `DestructibleBuilding`.
- Folders go feature first, then layer: `Scripts/<Feature>/Model|View|Controller/`. New code uses this layout; existing scripts move into it (with their `.meta`) when a milestone touches them. Don't restructure untouched code.
- Legacy exceptions: the person-named content folders (`Prefabs/Building Materials/Oeds/`, `Prefabs/Sky's Prefabs/`, `Scenes/Oeds.unity`) stay until generated content replaces them (roadmap M2/M3).

## Unity conventions here

- Input System supported devices (`Assets/Prefabs/InputSystem.inputsettings.asset`) must include `Mouse` and `Touchscreen`, or UI clicks, mouse attack and every touch control silently do nothing.
- Unity 6 physics API: `Rigidbody2D.linearVelocity` / `linearDamping` (not `velocity` / `drag`).
- Platforms: PC (keyboard/gamepad), TV (gamepads, 10-foot readable HUD), phones/tablets in landscape (touch). UI built in code goes through `UiFactory` (1920×1080 reference with `Expand` matching, so the whole reference rect is on screen at any aspect; `SafeArea` for edge-anchored HUD, `Canvas(scale)` for bigger UI, `EnsureEventSystem` adds an Input System UI module). Every menu must work with mouse, gamepad navigation and touch.
- UI look: `UiFactory` holds it. `DisplayFont` is Black Ops One (SIL OFL, `Resources/Fonts`) turned into a dynamic TMP font at runtime; `Outline`/`Heading` materials only use keyword sets the bundled TMP materials already use (outline; outline + underlay), so the shader variants ship. `Button` is a riveted steel plate (steel, gold when selected) with `UiButtonFeel` (grows when selected, ticks and clanks through `UiAudio`); `Panel` puts a plate with a hazard strip behind an overlay; `Title`, `Stripe`, `Star`. The sprites in `Resources/UI` come from `tools/artpipe/ui.py` (pixel art, 25 PPU = 4 UI units per pixel).
- When creating, moving or deleting assets from the filesystem, move/delete the matching `.meta` file too — prefabs and scenes reference scripts by the GUID in it.
- Commit messages use conventional prefixes (`feat:`, `fix:`, `docs:`).
