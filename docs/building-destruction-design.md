# Building Destruction & Collapse — Option C Design & Implementation

**Status:** Approved direction (Option C) · prototype landed
**Scope:** How buildings are composed, destroyed, and collapse — performant, stable,
emergent (Duke Nukem / Broforce), and ready for **Fishnet** networked multiplayer + local
co-op.

> **Decision:** buildings use a **structural support-graph** — blocks are **static while
> supported** and become dynamic rigidbodies **only while falling** — with break-apart debris
> for feel. The full option comparison and the performance/stability reasoning that led here
> is in [Appendix A](#appendix-a--why-option-c-perf--stability). This document now focuses on
> the design of C and how to build and test it.

---

## 1. The idea in one paragraph

A building is a grid of cells. Some cells are **anchors** — the foundation row (and any
load-bearing `Support` columns) that count as "connected to the ground." A cell is
**supported** if a chain of intact cells (4-neighbour) reaches an anchor. Standing cells are
**static** rigidbodies, so they cost almost nothing and can't jitter. When a cell is
destroyed we re-run a flood-fill from the anchors; any cell that lost its path to ground
**detaches**, flips to a **dynamic** rigidbody, and falls. Knock out a support column's base
and every floor it was holding loses its ground path at once — an emergent collapse — while
99% of the city stays static and cheap.

---

## 2. Architecture (as implemented)

Delivered as a self-contained module under `Assets/Scripts/Demolition/` with its own assembly
definition, so it's independently testable and doesn't disturb the existing prototype scripts.

| Layer | File | Responsibility |
|---|---|---|
| **Structural core** (pure C#, no physics/MonoBehaviour) | `Core/BuildingStructure.cs` | The support-graph: cells, anchors, `RemoveCell()` → returns detached cells via BFS from anchors. Unit-testable headlessly. |
| Material data | `BlockMaterial.cs` | Enum (`Brick/Metal/Wood/Glass/Support`) + per-material health and debug tint. |
| Per-cell behaviour | `BuildingBlock.cs` | Health + damage; **Static** rigidbody while supported; `BeginFalling()` flips it to **Dynamic** debris with a lifetime. |
| Building owner | `DestructibleBuilding.cs` | Holds the `BuildingStructure` + live blocks; on a block's death re-runs support and calls `BeginFalling()` on detached cells. Cap on simultaneous dynamic bodies. |
| Test player | `SimplePlayerController.cs` | Legacy-input keyboard controller with a **punch** (OverlapCircle → `TakeDamage`, knockback only on already-loose debris). |
| Test harness | `DemoBootstrap.cs`, `PrimitiveSprite.cs` | Builds ground + building + player + camera from code — no prefabs or art needed. |

### Data flow on a punch
```
SimplePlayerController.TryPunch()
  └─ Physics2D.OverlapCircle → BuildingBlock.TakeDamage(dmg)
       └─ health ≤ 0 → DestructibleBuilding.OnBlockDestroyed(block)
            ├─ BuildingStructure.RemoveCell(coord)  → List<detached cells>   (pure, deterministic)
            └─ for each detached block → BeginFalling()  (Static → Dynamic + gravity + nudge)
```
The only physics touched is the handful of blocks currently falling. The structural decision
("what detached") is discrete integer graph work, run only on a destruction event.

### Design choices worth noting
- **Static-while-supported** is the whole performance/stability win — standing blocks never
  enter the solver (see Appendix A).
- **Punch does not shove supported walls** (they're static); it only knocks loose debris.
  That's intentional and correct — force-based nudging of a standing wall is exactly the
  instability we're avoiding.
- **Component-based hit detection** (`GetComponentInParent<BuildingBlock>`) instead of
  layer/tag coupling, so the module drops into any scene.
- **`maxSimultaneousFalling` cap** bounds the transient spike from a huge collapse; overflow
  cells are removed without a dynamic body.

---

## 3. How to run it

**Manual / PlayMode (one click):**
1. New empty scene → create an empty GameObject → add **`DemoBootstrap`** → press Play.
2. Controls: **A/D** or ◀/▶ move · **Space/W** jump · **J or Left-Mouse** punch.
3. Walk to the building and punch out the base of an **orange Support column**. Watch the
   floors it was holding lose their ground path and collapse. Punch a mid-floor brick instead
   and note that horizontally-tied floors *don't* fall — the graph models redundant load
   paths. Cyan **Glass** dies fast; grey **Metal** foundation is tough.

**Headless unit tests:** Unity → *Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All*.

---

## 4. Test plan

### Automated (EditMode, `Assets/Tests/EditMode/BuildingStructureTests.cs`)
Pure connectivity logic, no scene/physics/play loop. Covers:

| Test | Asserts |
|---|---|
| `RemoveMidColumn_DetachesEverythingAbove` | Cutting a column mid-way drops all cells above. |
| `RemoveTopBlock_DetachesNothing` | Removing an unloaded top block collapses nothing. |
| `RemoveAnchor_DetachesWholeColumn` | Removing the foundation drops the column. |
| `SecondPathToGround_HoldsUpTheSpan` | A redundant load path (portal frame) keeps a span up until *both* legs are cut. |
| `RemoveNonexistentCell_IsNoOp` | Robust to bad coords. |
| `ComputeSupported_ReachesEveryConnectedCellFromAnyAnchor` | Flood-fill correctness on a solid slab. |
| `Cascade_RemovingSupportBaseDropsFloorsAbove` | Floors give lateral support; a fully isolated cell falls. |

> These expectations were cross-checked against an independent reference implementation of the
> same algorithm — all scenarios agree.

### Manual / PlayMode checklist (via `DemoBootstrap`)
- [ ] Player walks, jumps, and stands on the building (static blocks are solid ground).
- [ ] Punching a **supported** wall damages it but does **not** shove it.
- [ ] Destroying a **Support column base** collapses the floors above; debris falls and settles.
- [ ] Destroying a mid-floor brick with intact neighbours does **not** collapse (lateral support).
- [ ] Debris despawns after its lifetime (no unbounded body count).
- [ ] Framerate stays flat while the building stands (confirming static-body cost ≈ 0).

### Future test coverage (with each roadmap step)
- PlayMode test that spawns a `DestructibleBuilding` and asserts detached blocks become
  `Dynamic` and standing ones stay `Static`.
- Stress/load model unit tests (Phase 2).
- Fishnet: a headless test that a client reconstructs the correct standing set from a
  replicated destroyed-set (Phase 3).

---

## 5. Networking plan (Fishnet) — designed in, not yet wired

The reason C is the right base for netcode: **structural state is discrete and tiny.**

- **Server-authoritative grid.** Each `DestructibleBuilding` becomes a `NetworkBehaviour`
  owning a **destroyed-set** (`SyncList<ushort>`/bitset of cell indices). Blocks are grid
  indices, **not** a `NetworkObject` each.
- **Replicate events, not rigidbodies.** Punch → client request → server validates hit →
  applies damage → broadcasts `{destroyedCellId, detachedCellIds, seed}`. Never stream falling
  transforms (Unity Physics2D isn't cross-machine deterministic — see Appendix A).
- **Debris is client-side visual**, spawned deterministically from `seed`. If falling blocks
  must damage players, keep *that* collision authoritative but capped.
- **Late-joiners** receive the destroyed-set and rebuild the standing wall.
- **Local co-op** is one shared authoritative simulation — trivial on top of this.

The current `OnBlockDestroyed` → `RemoveCell` → `BeginFalling` split maps cleanly onto this:
`RemoveCell` is the authoritative discrete step; `BeginFalling` is the cosmetic step clients
run locally.

---

## 6. Roadmap

1. **✅ Prototype (this change):** static support-graph, collapse-on-detach, debris, test
   player with punch, headless unit tests, one-click demo scene.
2. **Content integration:** author real building prefabs on the same grid contract; map the
   existing material sprites (`Brick`, `MetalBeam`, `SupportBeams`, `Glass`, `Wood*`) onto
   `BuildingBlock`; flag `SupportBeams` as anchors. Retire `AddJointScript` and the dead joint
   code in the old `dstrBlock.cs`.
3. **Richer emergence (Phase 2):** column stress/load model — each column bears a max weight;
   `Support` bears a lot, wood/glass little; overload *yields* even while connected → sagging
   and partial collapses before full detachment. Merge large detached islands into a single
   `CompositeCollider2D` body for cheaper, cleaner falls.
4. **Fishnet (Phase 3):** per-building destroyed-set `SyncList` + collapse events with seed;
   client-side debris; late-join reconstruction; authoritative punch validation.

---

## 7. Risks / open questions

- **Collapse spike:** many blocks born dynamic in one frame. Mitigations in place/planned:
  `maxSimultaneousFalling` cap, composite-body merge, re-freeze settled rubble to static,
  stagger large detachments.
- **Do falling blocks hurt players?** If yes, that collision stays authoritative (capped); if
  no, debris is pure visual and cheap. Decide per design intent.
- **Diagonal support:** currently 4-neighbour (orthogonal), which reads as "cut the column,
  top falls." Revisit if designers want diagonal bracing.
- **Rubble persistence:** static rubble as obstacles is a nice touch but adds sync surface —
  decide per level.

---

## Appendix A — Why Option C (perf & stability)

### How Unity 2D physics (Box2D) bills you
1. **Static bodies are nearly free** — static-vs-static and static contacts are never solved.
2. **Cost ≈ (awake dynamic bodies) + (contacts) + (joints) × iterations** (project defaults
   8 velocity / 3 position).
3. **Islands:** bodies connected by contact *or joint* form one island, solved as a unit,
   that **sleeps/wakes together**. This decides the whole evaluation.

### Options
- **A. All-dynamic + joints (the earlier prototype):** joints weld the *entire building into
  one island* that can never sleep partially; long welded chains sag/jitter; high mass-ratio
  supports jitter; breakForce chains are chaotic. **Worst on both axes**, and non-deterministic
  for netcode.
- **D. All-dynamic, no joints:** islands can sleep, and gravity gives collapse for free — but
  cost scales with *every* block on screen, tall dynamic stacks are Box2D's jitter/sink case,
  and it's non-deterministic → unshippable for lockstep networking.
- **B. Debris only:** building static (≈ free), debris cosmetic and stable — but **no
  structural collapse**, so it fails the design goal alone.
- **C. Support-graph:** standing blocks **static** (≈ 0 cost, no stack instability possible);
  only the falling piece is dynamic, briefly; connectivity is cheap deterministic graph work.
  One real cost — a bounded, mitigable spike on a big simultaneous collapse.

### Scorecard (performance & stability only)
| | Standing cost | Collapse spike | Tall-structure stability | Tuning fragility | Determinism |
|---|---|---|---|---|---|
| A joints | ❌ whole-building island awake | ❌ big island + joints | ❌ sag/jitter | ❌ breakForce chaos | ❌ |
| D dynamic, no joints | ⚠️ N bodies, can sleep | ⚠️ local wake | ⚠️ stack jitter/sink | ✅ | ❌ |
| B debris only | ✅ static | ✅ tiny | ✅ | ✅ | ✅ (cosmetic) |
| **C support-graph** | ✅ **static** | ⚠️ bounded, mitigable | ✅ **no stacks simulated** | ✅ | ✅ |

**Ranking: C ≥ B > D ≫ A.** B fails the design goal and D can't be networked, so **C wins** —
static-body cost and no-joint stability for everything standing, physics paid only on the
small fraction actively falling.
