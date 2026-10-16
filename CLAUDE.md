# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository layout

- `Demolition Men/` — the Unity project (Unity **6000.4.9f1**, 2D, built-in render pipeline). All code lives under `Demolition Men/Assets/`.
- `docs/building-destruction-design.md` — design rationale for the destruction system ("Option C" support-graph), the NGO (Netcode for GameObjects) networking plan, and the PlayMode manual test checklist. Note: its §2/§3 still mention `SimplePlayerController` and legacy input; that controller has since been replaced by the components in `Scripts/Demolition/Player/`.
- `docs/roadmap.md` — the plan to v1.0: current-state gap analysis, milestones M0–M6, the procedural building/level generation design, the art pipeline and the agentic content pipeline, plus open design decisions. Check it before starting feature work.
- `Demolition Men/Demolition Men/` — a **stale, tracked copy** of an old project snapshot from the initial commit. Not part of the build. Never edit or search-and-replace into it; exclude it from greps.
- The `*.csproj` files and `Library/`, `Temp/` are Unity-generated (csproj are gitignored). `Demolition Men.slnx` scopes the solution to the four project assemblies.

## Commands

All paths below are relative to the repo root.

**Compile check (fast, works while the editor is open):**
```
dotnet build "Demolition Men/Demolition Men.slnx"
```
The generated csproj files list sources explicitly (`<Compile Include>`), so a newly created `.cs` file is not compiled by this until Unity regenerates the projects (focus the editor, or Preferences ▸ External Tools ▸ Regenerate project files). Output goes to `Temp/obj`. Two CS0649 warnings in `EndMenu`/`MainMenu` are pre-existing.

**EditMode tests** (`Assets/Tests/EditMode/BuildingStructureTests.cs`, assembly `Demolition.Tests.EditMode`):
- With the editor open: Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All. Batch mode refuses to run while the editor holds `Temp/UnityLockfile`.
- With the editor closed (PowerShell):
  ```
  & "C:\Program Files\Unity\Hub\Editor\6000.4.9f1\Editor\Unity.exe" -batchmode -projectPath "Demolition Men" -runTests -testPlatform EditMode -testResults "$env:TEMP\editmode.xml" -logFile -
  ```
  Single test: add `-testFilter "Demolition.Tests.BuildingStructureTests.RemoveMidColumn_DetachesEverythingAbove"`. Don't pass `-quit` with `-runTests`.

Only the pure `BuildingStructure` model has automated tests; everything physics/visual is verified manually in `Assets/Scenes/DemolitionTestBranch.unity` (a `DemoBootstrap` test bench that builds ground, a building, player, camera and HUD entirely from code).

Build scene order: MainMenu → Tutorial → HubWorld → Level 1.

## Assemblies and the dependency boundary

- `Assets/Scripts/Demolition/Demolition.asmdef` → assembly **`Demolition`**: the destruction system, players, health, score. References only `Unity.InputSystem`. Any other package used from this folder must be added to the asmdef's `references` or it won't compile (even though Assembly-CSharp sees it).
- Everything else in `Assets/Scripts/` (Camera, UI, Generations, SkillShop, `GlobalVariables`) → **Assembly-CSharp**, which references `Demolition` — never the reverse.

Because `Demolition` can't see Assembly-CSharp, cross-boundary state is pushed in from the outside:
- Score: `Demolition.Score.Value` is the source of truth (`BuildingBlock` awards +10 per destroyed block); `GlobalVariables.score` is a proxy property so legacy UI keeps its contract.
- Death mode: `CameraMovement` (scrolling death camera) sets `PlayerHealth.PermanentDeath` / calls `Kill()`; otherwise a knocked-out player respawns.

Players are located at runtime **by GameObject name** `"Player_1"` / `"Player_2"` (`CameraMovement`, `EndMenu`, `GlobalVariables`) — scene references to players are not serialized, so keep those names.

## Destruction architecture (support-graph)

Three layers; read `docs/building-destruction-design.md` for the why.

1. **`Core/BuildingStructure.cs`** — pure C#, no MonoBehaviour/physics. Cells on an integer grid, anchors, 4-neighbour flood-fill from anchors. `RemoveCells()` returns every cell that lost its path to ground. `ComputeStress()` is a two-pass heuristic (carried cells → cantilever/span/hanging, then pillar columns sum load and moment) that yields per-cell `Stress/Lean/Drift/Overload`. Fragile cells (glass) are held by neighbours but support never flows through them and they carry no load.
2. **`DestructibleBuilding.cs`** — owns one structure plus the live blocks. On `OnBlockDestroyed` it removes the block's cells, calls `BeginFalling()` on detached blocks (capped by `maxSimultaneousFalling`; overflow is destroyed without simulation), then recomputes stress. A stress **baseline** captured at `Start` is subtracted so intact buildings show no strain. Supports overloaded past baseline + `overloadTolerance` take crumble damage in `Update`, through the normal `TakeDamage` path.
3. **`BuildingBlock.cs`** — per-block lifecycle: **Static** while supported → **Dynamic** while falling (strips any leftover `Joint2D`, damages players it lands on, takes self-damage on hard landings, glass shatters on contact) → **Static rubble** once settled.

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

`Scripts/Demolition/Player/`: `PlayerInputReader` (Input System; merged gamepad + keyboard per player index), `PlayerMotor` (impulse movement, better-jump gravity), `PlayerCombat` (per-weapon × per-material damage profiles, `Physics2D.OverlapCircleAll` hits), `Dynamite`. Prefabs: `Prefabs/Demolition/Player_1`, `Player_2`, `Dynamite`. The same scripts drive the primitive no-art player in `DemoBootstrap`.

The original sprite art is reused through a **name contract** that fails silently if broken:
- Animator parameters: `Speed`, `InAir`, `IsPunching`, `IsAxeing`, `IsWrecking`.
- Animation-event methods: `applyDamage` (attack clips → `PlayerCombat`), `Effect` and `destroyDynamite` (`Explosion.anim` → `Dynamite`). Don't rename these methods.

## Unity conventions here

- Unity 6 physics API: `Rigidbody2D.linearVelocity` / `linearDamping` (not `velocity` / `drag`).
- When creating, moving or deleting assets from the filesystem, move/delete the matching `.meta` file too — prefabs and scenes reference scripts by the GUID in it.
- Commit messages use conventional prefixes (`feat:`, `fix:`, `docs:`).
