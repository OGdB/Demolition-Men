# Building Destruction & Collapse — Design Evaluation

**Status:** Proposal / for review
**Scope:** How buildings are composed, destroyed, and made to collapse, in a way that is
**performant and stable** first, feels emergent (Duke Nukem / Broforce), and works with
**Fishnet** networked multiplayer + local co-op.

---

## 1. Question being answered

> Should buildings be **blocks connected by joints/hinges**, or **sprites that break apart
> when punched**? Which is best **performance- and stability-wise**, while still giving
> emergent collapse?

**Answer:** neither in its pure form. Use a **structural support-graph** — blocks that are
*static while supported* and become dynamic rigidbodies *only while falling* — with
break-apart debris sprites layered on top for feel. This is Option **C** below. The decision
is driven by how Unity's 2D physics actually costs performance and stability (§4), not by any
prior experiment.

---

## 2. Where the prototype is today *(context, not the deciding factor)*

The current build happens to implement the "blocks welded by joints" approach (Option A):

| Piece | File | Behaviour |
|---|---|---|
| Block prefabs | `Assets/Prefabs/Building Materials/*` (`Brick`, `MetalBeam`, `WoodPlatform`, `Glass`, `SupportBeams`) | Each = `SpriteRenderer` + **dynamic `Rigidbody2D`** (mass 5, gravity on) + `BoxCollider2D` + `dstrBlock` + tag `Destructibles`, layer 8 |
| Auto-welding | `.../Oeds/Singles/AddJointScript.cs` | On spawn, welds touching blocks with `FixedJoint2D` (breakForce 2000), waits 3s, hands off to `dstrBlock` |
| Damage | `Assets/Scripts/Destruction/dstrBlock.cs` | `health` per material; `TakeDamage()` → on 0, `SetActive(false)` + score |
| Attack | `Assets/Scripts/Player/PlayerAttack.cs` | `OverlapCircleAll` → `TakeDamage()` + `AddForce()` |
| Explosion | `Assets/Scripts/Destruction/dynamiteScript.cs` | Radius damage + radial push |

The commented-out `OnJointBreak2D`/`FixedUpdate` code in `dstrBlock.cs` is a useful record of
where the joint path led in practice (dangling joints on destroy, joint-count ambiguity,
fallback timers), but the choice below is made on the physics cost model in §4 — the existing
experiment is only corroboration.

---

## 3. The options

- **A. All-dynamic + joints** *(current)* — every block a dynamic body, welded to neighbours;
  collapse = joints breaking under load.
- **B. Debris only** — building is static; on hit, delete the chunk and spawn short-lived
  flying fragment sprites. No structural collapse.
- **C. Support-graph** *(recommended)* — blocks static while supported; a connectivity
  flood-fill from the foundation decides what is detached; detached islands convert to dynamic
  bodies and fall; debris sprites layer on top for feel.
- **D. All-dynamic, no joints** — blocks are dynamic but unwelded, held only by gravity +
  friction stacking; collapse is "natural."

---

## 4. Performance & stability analysis (the deciding section)

### How Unity 2D physics (Box2D) bills you
1. **Static bodies are nearly free.** Static-vs-static and static contacts are never solved;
   a static body is just an AABB in the broadphase tree. A standing wall of *static* blocks
   costs ~nothing in the solver.
2. **Cost ≈ (awake dynamic bodies) + (contacts) + (joints) × solver iterations.** Unity
   defaults are 8 velocity / 3 position iterations per `FixedUpdate`; each contact and joint
   adds constraint rows solved on every iteration.
3. **Islands.** Box2D groups bodies connected **by contact or by joint** into an *island*
   solved as one unit. An island **sleeps only when every body in it is below the sleep
   threshold**, and **wakes as a unit**. This single fact decides the evaluation.

### A — all-dynamic + joints
- **Perf:** joints connect the *entire building into one island*. Disturb one block and the
  whole building is awake and fully solved every step until all of it settles — it can never
  sleep partially. Worst steady-state cost, and the collapse case is a giant awake island plus
  joint constraints.
- **Stability:** rigid constraints solved iteratively → long welded chains **sag/jitter**
  (8 iterations can't converge a tall stack; raising iterations costs more). A support loaded
  by many blocks is a **high mass-ratio** case — Box2D's classic jitter/sink weakness.
  `breakForce` chain reactions are chaotic to tune.
- **Verdict: worst on both axes.**

### D — all-dynamic, no joints (friction stacking)
- **Perf:** better than A — no joints, so islands split at contacts and a *settled* building
  can sleep and go cheap. But cost still scales with *total blocks across every on-screen
  building*, and any disturbance wakes the local contact island.
- **Stability:** tall stacks of dynamic boxes are precisely Box2D's jitter/sink stress case;
  stability degrades with height and load.
- **Emergence:** genuinely good — gravity collapse with zero graph code.
- **Blocker:** **non-deterministic** across machines → cannot be lockstepped for netcode; and
  stability-at-scale is shaky. Good feel, wrong tool for a networked scroller with many
  buildings.

### B — debris only
- **Perf:** building static (≈ free). Debris = a handful of short-lived, **unconstrained**,
  low-mass-ratio dynamic bodies that sleep/despawn fast. **Excellent.**
- **Stability:** loose bodies, no constraints, low mass ratio → rock-solid.
- **Blocker:** no structural collapse — fails the design goal by itself.

### C — support-graph
- **Perf:** the 99% of blocks that are standing are **static** → solver cost ≈ zero. Only the
  piece *currently falling* is dynamic, briefly. Merge each detached island into **one**
  `Rigidbody2D` via `CompositeCollider2D` → a few bodies, not hundreds. Settled rubble
  re-freezes to static and leaves the solver entirely. Peak dynamic count = size of the
  collapsing chunk, **not** the city.
- **Stability:** none of Box2D's stack problems can occur, because standing blocks never
  simulate. Falling pieces are unconstrained/composite → no joint convergence issues, low
  mass ratio → clean settle.
- **Structural logic:** the connectivity flood-fill that decides what falls is integer graph
  work run only on destruction events — trivial next to the solver, and deterministic.
- **Only real cost — transient spike:** a huge simultaneous detachment births many bodies in
  one frame. Bounded and mitigable: cap concurrent dynamic bodies, merge islands into
  composites, re-freeze settled rubble to static, stagger large detachments.

### Scorecard (performance & stability only)

| | Standing cost | Collapse-spike cost | Tall-structure stability | Tuning fragility | Determinism |
|---|---|---|---|---|---|
| A joints | ❌ whole-building island awake | ❌ big awake island + joints | ❌ sag/jitter | ❌ breakForce chaos | ❌ |
| D dynamic, no joints | ⚠️ N bodies, can sleep | ⚠️ local wake | ⚠️ stack jitter/sink | ✅ | ❌ |
| B debris only | ✅ static | ✅ tiny | ✅ | ✅ | ✅ (cosmetic) |
| **C support-graph** | ✅ **static** | ⚠️ bounded, mitigable | ✅ **no stacks simulated** | ✅ | ✅ |

**Ranking on perf/stability: C ≥ B > D ≫ A.** B fails the design goal (no collapse) and D
fails stability-at-scale + determinism, so **C is the decisive choice** — static-body cost and
no-joint stability for everything standing, physics paid only on the small fraction actively
falling.

---

## 5. Why C also wins the rest (bonus, not the basis of the decision)

- **Emergent collapse for free.** Flood-fill cascades: knock out the lower floor or the
  `SupportBeams` (flag them as load-bearing anchors) and upper floors lose their path to ground
  and drop as islands — the demolition fantasy, and why `SupportBeams` already exists.
- **Networking.** Structural state is discrete and tiny: a per-building destroyed-set
  `SyncList`/bitset (blocks are **grid indices, not a NetworkObject each**). Replicate
  *destruction events* and *collapse events* (detached cell IDs + RNG seed) — never a rigidbody
  stream. Debris is client-side visual off the seed; late-joiners get the destroyed-set and
  rebuild. Bandwidth: "hundreds of transforms/frame" → "a few bytes per punch." (Contrast: A/D
  are non-deterministic, forcing the server to stream every moving body.)
- **Local co-op:** one shared authoritative simulation — trivial.

---

## 6. Proposed architecture

### Data model
- **`BuildingStructure`** (one per building, a `NetworkBehaviour`): `Cell[,]` grid (or
  `Dictionary<Vector2Int, Cell>`), each cell = { material, health, intact, blockRef };
  foundation row flagged as **anchors**; networked destroyed-set (`SyncList<ushort>` / bitset).
- **`dstrBlock`** stays as the per-cell health/material component; loses the joint logic.

### Runtime flow
1. **Bake (spawn):** blocks start `RigidbodyType2D.Static`. Neighbour links from grid index
   (or a one-time `OverlapBox`), **not** `FixedJoint2D`. `AddJointScript` removed.
2. **Hit:** `PlayerAttack`/`dynamiteScript` → server-validated `TakeDamage()`. Spawn cosmetic
   debris + existing `OnDeath Particle Effects`.
3. **On cell destroyed:** BFS flood-fill from anchors over intact cells (scoped to this
   building / affected region). Cells not reached = **detached island(s)**.
4. **Collapse:** each island → one dynamic `CompositeCollider2D` body, gravity takes it; on
   settle → despawn or re-freeze to static rubble.
5. **Networking:** server broadcasts `{destroyedCellIds, detachedIslandIds, seed}`; clients
   apply discrete state and play the fall/debris locally.

### Phase 2 (optional, richer emergence)
- **Stress/load model:** each column bears a max weight; `SupportBeams` bear a lot, wood/glass
  little. Overload *yields* even while still connected → sagging and partial collapse before
  full detachment.

### Where joints still make sense
`HingeJoint2D` only on *special* pieces — a swinging sign, a hanging balcony, the wrecking
ball — never as the backbone of every wall.

---

## 7. Migration checklist (from current code)

- [ ] Blocks start **Static**; keep `dstrBlock` health/material.
- [ ] **Remove `AddJointScript`** and all `FixedJoint2D` welding; delete the dead
      `OnJointBreak2D`/`FixedUpdate` joint code in `dstrBlock.cs`.
- [ ] Add **`BuildingStructure`** manager: cell map + anchors + connectivity flood-fill.
- [ ] `destroyBlock()` → notify structure → detach + collapse islands (composite dynamic body).
- [ ] Spawn **debris sprites** on hit; reuse `OnDeath Particle Effects`.
- [ ] Flag `SupportBeams` as **load-bearing anchors**.
- [ ] Cap concurrent dynamic bodies; re-freeze settled rubble to static.
- [ ] **Networking:** per-building `SyncList` destroyed-set + collapse event with seed.
- [ ] *(Phase 2)* column stress/load model.

### Build order
1. Single-player: static grid + connectivity + collapse-on-detach + debris (the feel).
2. Load-bearing supports + stress model (the emergence).
3. Fishnet: authoritative destroyed-set + collapse events + client-side debris.

---

## 8. Risks / open questions

- **Collapse spike** (§4 C): cap simultaneous dynamic bodies, merge islands into composites,
  re-freeze rubble, stagger large detachments.
- **Do falling blocks damage players?** If yes, keep that collision authoritative but cap
  counts; if no, debris is pure visual and cheap.
- **Rubble persistence:** static rubble as obstacles is nice but adds sync surface — decide per
  level.
- **Flood-fill frequency:** debounce/scope to the affected region so rapid multi-hits don't
  re-scan the whole building every frame.
