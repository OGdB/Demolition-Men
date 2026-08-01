# Building Destruction & Collapse — Design Evaluation

**Status:** Proposal / for review
**Scope:** How buildings are composed, destroyed, and made to collapse, in a way that
is performant, feels emergent (Duke Nukem / Broforce), and works with **Fishnet**
networked multiplayer + local co-op.

---

## 1. Question being answered

> Should buildings be **blocks connected by hinges**, or **sprites that break apart when
> punched**? What gives the best emergent collapse without wrecking performance or
> networking?

**Answer:** neither in its pure form. Use a **structural support-graph** (blocks that are
*static while supported* and only become dynamic rigidbodies *while falling*), with
break-apart debris sprites layered on top purely for feel. This is Option **C** below.

---

## 2. Where the prototype is today

The current build already implements the "blocks welded by joints" approach:

| Piece | File | Behaviour |
|---|---|---|
| Block prefabs | `Assets/Prefabs/Building Materials/*` (`Brick`, `MetalBeam`, `WoodPlatform`, `Glass`, `SupportBeams`) | Each = `SpriteRenderer` + **dynamic `Rigidbody2D`** (mass 5, gravity on) + `BoxCollider2D` + `dstrBlock` + tag `Destructibles`, layer 8 |
| Auto-welding | `.../Oeds/Singles/AddJointScript.cs` | On spawn, welds touching blocks with `FixedJoint2D` (breakForce 2000), waits 3s, hands off to `dstrBlock` |
| Damage | `Assets/Scripts/Destruction/dstrBlock.cs` | `health` per material; `TakeDamage()` → on 0, `SetActive(false)` + score |
| Attack | `Assets/Scripts/Player/PlayerAttack.cs` | `OverlapCircleAll` → `TakeDamage()` + `AddForce()` |
| Explosion | `Assets/Scripts/Destruction/dynamiteScript.cs` | Radius damage + radial push |

### The joint approach was already tried and abandoned
The dead code in `dstrBlock.cs` documents the exact failure modes, in the team's own words:

> *"When a block is destroyed, the block that had a hinge connected to it does not
> disconnect, making it indestructible by force."*

Also captured there: `OnJointBreak2D` firing regardless of joint count, blocks with 1 vs 2
joints, and 2-second fallback destroy timers. The collapse logic was commented out and
reduced to `SetActive(false)`. **Result: there is currently no real structural collapse —
blocks just vanish and neighbours get shoved.** These are not bugs to fix; they are the
inherent cost of per-block joints.

---

## 3. The three options

### A. Blocks welded by joints *(current approach)*
Every block is a dynamic body; collapse = joints breaking under load.

- ❌ **Performance:** hundreds of dynamic bodies + joints *per building* × several buildings
  on screen. Box2D's solver chokes; welded stacks jitter/sag because `FixedJoint2D` is
  spring-based.
- ❌ **Dangling-joint bug** on destroy (already hit — see §2).
- ❌ **Networking (dealbreaker):** Unity Physics2D is **not deterministic across machines**,
  so clients cannot each simulate independently. The server must stream transform+velocity
  for every moving block. A collapse moves hundreds of bodies at once — worst-case
  bandwidth. This fights Fishnet the hardest.

### B. Break into debris sprites on punch
Building mostly static; on hit, delete the chunk and spawn short-lived flying fragments.

- ✅ Cheap; looks exactly like Duke/Broforce gibs.
- ✅ Debris is **cosmetic** → no tight sync; fire one networked event + seed, simulate gibs
  locally per client.
- ❌ **No structural collapse by itself.** Nothing comes down; it's a hit effect, not a
  physics system.

### C. Structural support-graph ✅ *(recommended)*
Blocks are a grid and stay **static while supported**. Each building tracks connectivity to
its foundation. Destroy a block → flood-fill from the ground over intact blocks → any
block/island that lost its path to ground **converts to a dynamic `Rigidbody2D` and falls**.
Debris sprites (B) layer on top for the punch feel.

This is how Broforce / Terraria / most destructible-building games actually work.

---

## 4. Why C wins on our constraints

- **Emergent collapse for free.** Knock out the lower floor or the support beams and the
  connectivity check cascades: upper floors lose their path to ground and drop as islands.
  That's the demolition fantasy — and it's *literally why `SupportBeams` already exists in
  the project*. Flag those as load-bearing anchors and "destroy the supports to bring it
  down" reads instantly.
- **Performance.** ~99% of blocks are **static** (zero solver cost). Only blocks *currently
  falling* are dynamic. Flood-fill runs only on a destruction event, scoped to one building.
- **Networking — the whole reason to pick C.** Structural state is **discrete and tiny**:
  - Server owns "which cells are destroyed" as a per-building `SyncList`/bitset. Blocks are
    **grid indices, not a NetworkObject each.**
  - Replicate *destruction events* and *collapse events* (detached cell IDs + an RNG seed),
    **never a rigidbody stream**.
  - Falling debris is client-side visual off the seed.
  - Late-joiners receive the destroyed-set and rebuild the wall.
  - Bandwidth: from "hundreds of transforms/frame" → "a few bytes per punch."
  - Local co-op becomes trivial: one shared authoritative simulation.

| | A. Joints (current) | B. Debris only | **C. Support-graph** |
|---|---|---|---|
| Emergent collapse | ⚠️ in theory, buggy | ❌ none | ✅ cascading |
| Performance @ many buildings | ❌ poor | ✅ great | ✅ great |
| Fishnet-friendly | ❌ worst case | ✅ (cosmetic) | ✅ discrete state |
| Punch/gib feel | ⚠️ shove | ✅ great | ✅ great (debris on top) |
| Reuses current assets | — | partial | ✅ high |

---

## 5. Proposed architecture

### Data model
- **`BuildingStructure`** (one per building, a `NetworkBehaviour`):
  - `Cell[,]` grid (or `Dictionary<Vector2Int, Cell>`), each cell = { material, health,
    intact, blockRef }.
  - Foundation row flagged as **anchors** (connected to ground).
  - Networked **destroyed-set** (`SyncList<ushort>` of cell indices, or a bitset).
- **`dstrBlock`** stays as the per-cell health/material component; loses the joint logic.

### Runtime flow
1. **Bake** (spawn): blocks start `RigidbodyType2D.Static`. Neighbour links come from grid
   index (or a one-time `OverlapBox`), **not** `FixedJoint2D`. `AddJointScript` is removed.
2. **Hit:** `PlayerAttack`/`dynamiteScript` → server-validated `TakeDamage()` on cell(s).
   Spawn cosmetic debris + existing `OnDeath Particle Effects`.
3. **On cell destroyed:** BFS/flood-fill from anchors over intact cells (scoped to this
   building, ideally the affected region). Cells not reached = **detached island(s)**.
4. **Collapse:** each detached island → convert its blocks to `Dynamic` (or spawn one
   dynamic composite body per island) and let gravity take them. On settle: despawn, or
   re-freeze as static rubble (rubble = optional, adds a little sync).
5. **Networking:** server broadcasts `{destroyedCellIds, detachedIslandIds, seed}`; clients
   apply the discrete state and play the fall/debris locally.

### Phase 2 (optional, richer emergence)
- **Stress/load model:** each column bears a max weight; `SupportBeams` bear a lot, wood/glass
  little. Overload *yields* even while still connected → sagging and partial collapse before
  full detachment. Turns "cut the last support" into a visible groan-then-drop.

### Where joints still make sense
`HingeJoint2D` earns its place only on *special* pieces — a swinging sign, a hanging balcony,
the wrecking ball — never as the backbone of every wall.

---

## 6. Migration checklist (from current code)

- [ ] Blocks start **Static**; keep `dstrBlock` health/material.
- [ ] **Remove `AddJointScript`** and all `FixedJoint2D` welding; delete the dead
      `OnJointBreak2D`/`FixedUpdate` joint code in `dstrBlock.cs`.
- [ ] Add **`BuildingStructure`** manager: cell map + anchors + connectivity flood-fill.
- [ ] `destroyBlock()` → notify structure → detach + collapse islands (dynamic bodies).
- [ ] Spawn **debris sprites** on hit; reuse `OnDeath Particle Effects`.
- [ ] Flag `SupportBeams` as **load-bearing anchors**.
- [ ] **Networking:** per-building `SyncList` destroyed-set + collapse event with seed;
      blocks are grid indices, not per-block NetworkObjects.
- [ ] *(Phase 2)* column stress/load model.

### Build order
1. Single-player: static grid + connectivity + collapse-on-detach + debris (the feel).
2. Load-bearing supports + stress model (the emergence).
3. Fishnet: authoritative destroyed-set + collapse events + client-side debris.

---

## 7. Risks / open questions

- **Island → dynamic conversion cost** during a big cascade: cap simultaneous dynamic bodies;
  merge an island into a single composite body rather than N loose blocks when large.
- **Do falling blocks damage players?** If yes, that collision must stay authoritative (keep
  it, but cap counts). If no, debris is pure visual and cheap.
- **Rubble persistence:** static rubble as obstacles is a nice touch but adds sync surface —
  decide per level.
- **Flood-fill frequency:** debounce/scope to the affected region so rapid multi-hits don't
  re-scan the whole building every frame.
