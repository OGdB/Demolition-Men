# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository layout

- `Demolition Men/` — the Unity project (Unity **6000.4.9f1**, 2D, built-in render pipeline). All code lives under `Demolition Men/Assets/`.
- `docs/building-destruction-design.md` — design rationale for the destruction system ("Option C" support-graph), the NGO (Netcode for GameObjects) networking plan, and the PlayMode manual test checklist.
- `docs/roadmap.md` — the plan to v1.0: current-state gap analysis, milestones M0–M6, the procedural building/level generation design, the art pipeline and the agentic content pipeline, plus the design decisions. Check it before starting feature work.
- The `*.csproj` / `*.slnx` files and `Library/`, `Temp/`, `Logs/`, `UserSettings/` are Unity-generated or per-user and gitignored. Unity regenerates `Demolition Men.slnx` (the four project assemblies) when the editor opens the project.

## Commands

All paths below are relative to the repo root.

**Compile check (fast, works while the editor is open):**
```
dotnet build "Demolition Men/Demolition Men.slnx"
```
The generated csproj files list sources explicitly (`<Compile Include>`), so a newly created `.cs` file is not compiled by this until Unity regenerates the projects (focus the editor, or Preferences ▸ External Tools ▸ Regenerate project files). Output goes to `Temp/obj`. One CS0649 warning in `MainMenu` is pre-existing.

**EditMode tests** (`Assets/Tests/EditMode/`, assembly `Demolition.Tests.EditMode`: `BuildingStructure`, `RunState`, `RunScore`):
- With the editor open: Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All. Batch mode refuses to run while the editor holds `Temp/UnityLockfile`. From an agent, the Unity MCP `RunCommand` can start `TestRunnerApi` with an `ICallbacks` that writes results to `Temp/` (the callback class must be top-level, not nested; `System.Reflection` is blocked).
- With the editor closed (PowerShell):
  ```
  & "C:\Program Files\Unity\Hub\Editor\6000.4.9f1\Editor\Unity.exe" -batchmode -projectPath "Demolition Men" -runTests -testPlatform EditMode -testResults "$env:TEMP\editmode.xml" -logFile -
  ```
  Single test: add `-testFilter "Demolition.Tests.BuildingStructureTests.RemoveMidColumn_DetachesEverythingAbove"`. Don't pass `-quit` with `-runTests`.

Only the pure models have automated tests; everything physics/visual is verified manually in `Assets/Scenes/DemolitionTestBranch.unity` (a `DemoBootstrap` test bench that builds ground, a building, player, camera and HUD entirely from code).

Build scene order: MainMenu → Tutorial → HubWorld → Level 1.

## Assemblies and the dependency boundary

- `Assets/Scripts/Demolition/Demolition.asmdef` → assembly **`Demolition`**: the destruction system, players, the run loop, its HUD. References `Unity.InputSystem`, `Unity.TextMeshPro`, `UnityEngine.UI`. Any other package used from this folder must be added to the asmdef's `references` or it won't compile (even though Assembly-CSharp sees it).
- Everything else in `Assets/Scripts/` (Camera/Parralyx, Menu, Generations) → **Assembly-CSharp**, which references `Demolition` — never the reverse. Menu choices reach the run through `Demolition.Run.Session` (e.g. `PlayerCount`).

Players are located at runtime **by GameObject name** `"Player_1"` / `"Player_2"` (`RunController`, `TerrainGenerator`) — scene references to players are not serialized, so keep those names.

## Run loop

`Scripts/Demolition/Run/`, one `Prefabs/Run/Run.prefab` per level scene (Level 1, Tutorial):
- **Model:** `RunState` (countdown → playing → finished/failed; out-of-bounds grace, co-op free rejoin then Resurrect, finish + last call), `RunScore` (material points, collapse chain multiplier, speed bonus vs par, grade), `Session`.
- **Controller:** `RunController` finds the players, feeds the models (viewport out-of-bounds, finish crossing, health depletion, `DestructionEvents`) and applies their decisions (`PlayerHealth.Kill/Revive`, controls on/off, camera targets). `FinishLine` marks the finish (in `levelPart_End`).
- **View:** `ScrollCamera` on `Main Camera 1` (push/auto-scroll, ground-anchored zoom framing, letterbox clear), `RunHud` and `ResultsView` (uGUI built in code through `UiFactory`), `FinishLineView`.
- Pickups: `Scripts/Demolition/Pickup/` (`Pickup`, `PickupSlot` in each level part, `PickupView`), prefab `Prefabs/Pickups/Pickup.prefab`. Resurrect calls `RunController.Current.TryResurrect`.
- Score attribution: `TakeDamage(amount, instigator)` carries the player index (`PlayerInputReader.PlayerIndex`, assigned by the run). Collapsed blocks are credited to whoever destroyed the support; blocks credited once never score again. `PlayerHealth.ManagedByRun` makes depletion raise `Depleted` instead of the test-bench respawn.

## Destruction architecture (support-graph)

Three layers; read `docs/building-destruction-design.md` for the why.

1. **`Building/Model/BuildingStructure.cs`** — pure C#, no MonoBehaviour/physics. Cells on an integer grid, anchors, 4-neighbour flood-fill from anchors. `RemoveCells()` returns every cell that lost its path to ground. `ComputeStress()` is a two-pass heuristic (carried cells → cantilever/span/hanging, then pillar columns sum load and moment) that yields per-cell `Stress/Lean/Drift/Overload`. Fragile cells (glass) are held by neighbours but support never flows through them and they carry no load.
2. **`Building/Controller/DestructibleBuilding.cs`** — owns one structure plus the live blocks. On `OnBlockDestroyed` it removes the block's cells, calls `BeginFalling()` on detached blocks (capped by `maxSimultaneousFalling`; overflow is destroyed without simulation), then recomputes stress. A stress **baseline** captured at `Start` is subtracted so intact buildings show no strain. Supports overloaded past baseline + `overloadTolerance` take crumble damage in `Update`, through the normal `TakeDamage` path.
3. **`Building/Controller/BuildingBlock.cs`** — per-block lifecycle: **Static** while supported → **Dynamic** while falling (strips any leftover `Joint2D`, damages players it lands on, takes self-damage on hard landings, glass shatters on contact) → **Static rubble** once settled.

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
- `BlockMaterial` (Brick/Metal/Wood/Glass/Support) sets default health; Glass registers as fragile.

Code-driven alternative (`DemoBootstrap`): call `BuildingBlock.Initialise` + `DestructibleBuilding.RegisterBlock` before the building's `Start`, which then skips the scan.

`Prefabs/Buildings/Crane.prefab` keeps its authored joints on purpose — it's a physics prop outside the destruction system.

## Players

`Scripts/Demolition/Player/Controller/`: `PlayerHealth`, `PlayerInputReader` (Input System; the device assigned from `Session` by the join screen, or gamepad index + keyboard when a level is started directly), `PlayerMotor` (impulse movement, better-jump gravity), `PlayerCombat` (per-weapon × per-material damage profiles, `Physics2D.OverlapCircleAll` hits), `Dynamite`. Prefabs: `Prefabs/Demolition/Player_1`, `Player_2`, `Dynamite`. The same scripts drive the primitive no-art player in `DemoBootstrap`.

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

- Unity 6 physics API: `Rigidbody2D.linearVelocity` / `linearDamping` (not `velocity` / `drag`).
- When creating, moving or deleting assets from the filesystem, move/delete the matching `.meta` file too — prefabs and scenes reference scripts by the GUID in it.
- Commit messages use conventional prefixes (`feat:`, `fix:`, `docs:`).
