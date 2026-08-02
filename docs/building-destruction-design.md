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
| **Structural core** (pure C#, no physics/MonoBehaviour) | `Core/BuildingStructure.cs` | The support-graph: cells, anchors, `RemoveCell()` → returns detached cells via BFS from anchors. Plus `ComputeStress()` — a per-cell **stress heuristic** (pillared / cantilever / span) used for pre-collapse lean visuals. Unit-testable headlessly. |
| Material data | `BlockMaterial.cs` | Enum (`Brick/Metal/Wood/Glass/Support`) + per-material health and debug tint. |
| Per-cell behaviour | `BuildingBlock.cs` | Health + damage (with hit particles + progressive darkening); **Static** while supported — under stress it **leans, sags and trembles** (sprite on a visual child; the collider never moves); `BeginFalling()` flips it to **Dynamic** debris that **settles into persistent Static rubble** and **deals impact damage** to the player on the way down. |
| Building owner | `DestructibleBuilding.cs` | Holds the `BuildingStructure` + live blocks; on a block's death re-runs support, calls `BeginFalling()` on detached cells, and refreshes per-cell stress targets (minus the intact building's baseline). Exposes `DestroyedFraction` for the HUD. Cap on simultaneous dynamic bodies. |
| Player health | `PlayerHealth.cs` | Takes impact damage from falling blocks; hurt flash + particles; respawns at start so the bench stays usable. |
| Feedback | `Particles.cs`, `SpriteFlash.cs` | Code-configured one-shot particle bursts (hit / destroy / impact) and expanding punch-flash sprites — no imported assets. |
| Test player | `SimplePlayerController.cs` | Legacy-input keyboard controller with a **punch** (OverlapCircle → `TakeDamage`, knockback only on already-loose debris). The punch overlap circle is **always visible** as a faint ring (dims on cooldown) and **flashes** on every swing. |
| Test harness | `DemoBootstrap.cs`, `PrimitiveSprite.cs` | Builds ground + a **realistic hollow building** (walls with windows, interior floor slabs, load-bearing support columns, ground-floor doorway, roof) + player + camera from code, plus a **HUD** (destruction % + health). No prefabs or art needed. |

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
- **Pre-collapse anticipation is cosmetic and deterministic.** `ComputeStress()` works in two
  passes. Pass 1 classifies *carried* cells: *cantilever* (support one side only → leans away
  from it, more with overhang length), *span* (pillars both sides → sags most at mid-span,
  level there), or *hanging* (max stress, straight sag) — and each carried cell **attributes
  its weight** to the pillar cell(s) holding it (split by proximity, lever arm = bending
  moment). Pass 2 makes the *pillars* answer for what they carry: each contiguous column sums
  incoming load and signed moment — heavy load ⇒ stress/tremble even when balanced; net
  moment ⇒ the column **leans toward the mass it carries**, with a height² drift so it
  visibly *bows* like a bending beam. So when one wall is the last load path for a roof, that
  wall saturates, leans over the span, and trembles — exactly where a player expects the
  stress to be. Blocks render all of it on a **visual child transform — colliders never
  move**, so physics and gameplay are untouched. Because it derives purely from the discrete
  structure state, every networked client recomputes near-identical leans locally from the
  destroyed-set: **zero extra sync**. A baseline captured at spawn hides the stress an intact
  building legitimately has (roof over a hollow interior), so only damage-induced stress is
  displayed.
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
1. Open **`Assets/Scenes/DemolitionTestBranch.unity`** (already wired with `DemoBootstrap`)
   and press Play — or, in any empty scene, add `DemoBootstrap` to an empty GameObject.
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
| `ComputeStress_PillaredColumn_IsZero` | An unloaded column carries no stress. |
| `ComputeStress_CantileverArm_GrowsWithDistance_AndLeansAwayFromSupport` | Overhang stress grows with length; the mast carrying the arm is itself stressed and leans/bows toward it. |
| `ComputeStress_SimplySupportedSpan_PeaksAtMidspan_WithMirroredLean` | Span sags most at mid-span (level), mirrored leans either side; both pillars lean inward toward the load. |
| `ComputeStress_HangingCell_GetsMaxStress` | A cell with no sideways load path gets max stress, straight-down sag. |
| `ComputeStress_LoadedColumn_LeansTowardItsLoad` | The "last wall standing" carrying a roof saturates, leans toward the span, and bows with height². |
| `ComputeStress_BalancedLoad_HeavyButLevel` | Equal loads both sides: overloaded (trembles) but stays upright — moments cancel. |

> These expectations were cross-checked against an independent reference implementation of the
> same algorithm — all scenarios agree.

### Manual / PlayMode checklist (via `DemoBootstrap`)
- [ ] Player walks, jumps, and stands on the building floors (static blocks are solid ground).
- [ ] Punching a block shows hit particles and progressive darkening; a **supported** wall takes damage but is **not** shoved.
- [ ] Destroying a **Support column** collapses whatever loses its path to the foundation; debris falls, tumbles, and **settles into rubble that stays**.
- [ ] Destroying a mid-floor block with intact neighbours does **not** collapse (lateral support / redundant load path).
- [ ] Falling blocks that land on the player **reduce the health bar** (impact scaled by speed); death respawns the player.
- [ ] Weakened-but-still-supported sections visibly **lean/sag toward the missing support**, and **tremble** when close to giving way (colliders stay put — walking on a leaning floor is unchanged).
- [ ] When a single wall/column is the **last load path** for the roof, that column itself **leans and bows toward the span it carries** and trembles — the stress shows where a player physically expects it.
- [ ] The **punch ring** is always visible in front of the player, dims during cooldown, and every swing **flashes** the actual overlap circle.
- [ ] The **HUD destruction %** climbs as the building comes down.
- [ ] Framerate stays flat while the building stands, and settled rubble re-freezes to Static (bounded body count).

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

1. **✅ Prototype:** static support-graph, collapse-on-detach, persistent rubble, hit/impact
   particles, player impact damage + HUD, pre-collapse **stress lean/sag/tremble**
   anticipation, always-visible punch-range indicator, a realistic procedural building, test
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
- **Rubble persistence:** implemented as settle-to-Static (rubble stays, costs nothing). It
  currently adds no network sync surface because it's client-side cosmetic; if rubble must be
  a shared gameplay obstacle, that state has to be replicated — decide per level.

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
