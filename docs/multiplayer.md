# Online co-op plan

**Status:** plan only, nothing wired (2026-10-17). Online stays post-v1 (roadmap D4), but
milestone O1 is offline-only and can land any time.

Two players on two devices play one run together, over the internet or the same Wi-Fi. This
doc picks the topology, says exactly what crosses the wire and who decides what, and splits
the work into milestones. The two goals are **no game server** and **as few bytes as
possible**. It builds on the support-graph design (`building-destruction-design.md` §5),
which is what makes the destruction cheap to sync.

---

## 1. Topology: a player hosts, the relay only forwards

```
  Device A: NGO host                                  Device B: NGO client
  ┌─────────────────────────────┐                     ┌───────────────────────────┐
  │ Player_1 (local)            │                     │ Player_2 (local)          │
  │ Player_2 (proxy)            │ ◄── Unity Relay ──► │ Player_1 (proxy)          │
  │ decides: blocks, pickups,   │   (forwards UDP,    │ decides: its own player   │
  │ run phase, score            │    runs no game)    │ (movement, hits, damage)  │
  └─────────────────────────────┘                     └───────────────────────────┘
```

- **Listen server.** One player's device runs Netcode for GameObjects (NGO) as host: server
  plus its own player. The other device joins as a client. Transport: Unity Transport (UTP).
- **No dedicated server.** A headless Unity server is one Unity process per match (hundreds
  of MB of RAM, a share of a CPU core), and it adds a round trip for both players. In co-op
  nobody gains by cheating, so a neutral referee buys nothing. A small VPS could hold a
  handful of matches. With player hosting it holds none, and the number of matches has no
  limit.
- **No lockstep or rollback.** Physics2D is not deterministic across machines (design doc,
  Appendix A), so player and debris physics would drift apart. Only the discrete structure
  model is deterministic, and that is all this plan relies on.
- **A relay for the internet.** Phones on mobile data sit behind carrier NAT, so a direct
  connection to them fails. Traffic goes through **Unity Relay**, which UTP supports out of
  the box: no server code. Same-network play can skip it (O5).

## 2. Who decides what

Two rules:
- **The host decides what both screens must agree on:** the standing structure, pickups, the
  run phase and timers, the score.
- **Each player's own device decides what the player feels:** its movement, which blocks its
  swing hits, and the damage it takes. That player never waits a round trip to move or to
  see its swing land.

| State | Decided by | How it travels |
|---|---|---|
| Which level | host | start message: level id or daily date, seed, `LevelLayout.Hash()` |
| Ground, buildings, pickup spots | nobody: generated | rebuilt on each device from `{spec, seed}` by `LevelGrammar` (Pcg32) |
| Player position | owner | `NetworkTransform` with owner authority, X/Y only, half floats, interpolated |
| Player pose (facing, grounded, weapon, swinging) | owner | one byte, `NetworkVariable`, sent on change; drives the proxy's animator |
| Player health | owner | one byte, `NetworkVariable`, sent on change |
| Damage to a standing block | owner finds the hit, host applies it | `DamageRequest` to host; host answers with `CellHealth` to all |
| Detached cells, falling debris, rubble | each device | derived from `RemoveCells` (deterministic); debris uses local physics and local random |
| Stress lean, tremble, demolition meter | each device | derived from the same structure state: 0 bytes |
| Overload crumble | host | `CellHealth`, like any other damage |
| Pickups | host | client asks (`Claim`), host grants first come (`Taken`) |
| Dynamite flight | owner | `Thrown {pos, vel}` and `Exploded {pos}`; the other device flies a cosmetic copy |
| Run phase, countdown, timers | host | phase byte + the server tick it started on; timers come from NGO's shared clock |
| Score, multiplier, grade | host | `NetworkVariable`s, sent on change; final result in one message |
| Out of bounds, rejoin, resurrect | host | `RunState` on the host; a command to the owner (`Kill`, `Revive(pos)`) |
| Cash, records, unlocks | each player's own profile | each device banks the host's final result into its own `profile.json` |

## 3. What crosses the wire

### Efficiency rules

1. **Send decisions, not simulation.** Cell health and events, never block transforms,
   debris or rubble. Everything the models can derive is derived on each device.
2. **Few NetworkObjects.** One per player and one `RunNet` per run. Never one per building,
   block, pickup or stick of dynamite. NGO checks every spawned object for changes each
   tick, so this saves CPU as well as spawn messages.
3. **Change-only.** `NetworkVariable`s send only when they change. Nothing is polled.
4. **One batch per tick.** The host gathers every cell that changed during a tick and sends
   them in one message.
5. **Quantize.** Positions as half floats; health as 7 bits; buildings, cells and pickups as
   small varint indices (`BytePacker.WriteValueBitPacked`).
6. **Tick rate 20 Hz** (NGO's default is 30), with interpolation smoothing the remote
   player. Packet headers cost more than the payload, so fewer packets per second is the
   biggest saving available.
7. **No garbage on the hot path.** The per-tick messages (`CellHealth`, `DamageRequest`) are
   NGO named messages written with `FastBufferWriter` on `Allocator.Temp`. Rare events
   (pickups, run commands, start, results) can be plain RPCs.
8. **No `NetworkAnimator`, no `NetworkRigidbody2D`.** The pose byte drives the proxy's
   animator. Proxies are kinematic bodies, so local debris still bounces off them.

### Messages

| Message | Direction | Payload | When |
|---|---|---|---|
| Player transform | owner → others | X, Y half floats | ≤ 20/s while moving |
| Pose, health | owner → others | 1 byte each | on change |
| `DamageRequest` | client → host | building (varint), cell (varint), amount (half float) ≈ 5 B | per block a client hits |
| `CellHealth` batch | host → client | per cell: building, cell, health 7 bits + instigator 1 bit ≈ 4 B | once per tick, only if something changed |
| `Claim` / `Taken` | client → host / host → all | pickup index (+ player) | per pickup |
| `Thrown` / `Exploded` | owner → others | position (+ velocity) | per stick of dynamite |
| Phase | host → client | phase byte + start tick | ≈ 5 per run |
| Score | host → client | points per player, multiplier | on change |
| `Kill` / `Revive` | host → owner | position | rare |
| Start, results | host → client | level, seed, hash / final score, grade, time | once per run |
| Desync check (dev builds) | host → client | hash of the standing cells | every 5 s and at the finish |

A punch that breaks a wall costs about 10 bytes. A dynamite blast that damages 15 blocks
costs about 60 bytes, once. The steady cost is the two player transforms.

### Budget

Per device, each way: about 20 packets/s × (≈ 60 B of IP/UDP/UTP/Relay headers + ≈ 20 B of
payload) ≈ **1.6 KB/s**. Both players through the relay: about **6 KB/s, or 20–25 MB per
match-hour**. Host CPU: the same game as offline, plus a few hundred bytes of serialization
per tick.

## 4. How destruction stays in sync

1. On the client, a swing's `applyDamage` event finds blocks with `OverlapCircleAll` on its
   own copy of the level, as it does today. It plays chips and the hit sound right away, and
   sends one `DamageRequest` per block. It does not change any health itself. Headbutts,
   glass shattered by touch and dynamite blasts take the same path.
2. The host applies `TakeDamage(amount, sender's player index)`, the normal path: health,
   `OnBlockDestroyed`, scoring with the right instigator. The instigator comes from the
   sender's connection, never from the payload.
3. Each tick the host sends one `CellHealth` batch: every cell whose health changed (its own
   hits, the client's hits, overload crumble).
4. The client sets each block's health (damage tint), and at 0 runs the normal destroy path.
   `BuildingStructure.RemoveCells` is deterministic, so the client detaches exactly the same
   cells. Its debris falls with local physics. `DestructionEvents` fire as usual, so shake,
   dust and sound work on the client for free; only the score ignores them there.
5. Stress lean and the demolition meter come from the same structure state on both devices.

A client sees its own collapses one round trip late (50–150 ms on the internet). The chips
and sound in step 1 are instant, which hides most of that.

**Same level on both sides.** The start message carries `LevelLayout.Hash()`. The client
builds the level from the id and seed, compares the hash, and refuses with a clear message
on a mismatch. One known risk: `TraversalValidator` and the building validator use float
math, and IL2CPP on ARM64 may fuse multiply-adds. A borderline seed could pass validation on
a PC and fail on a phone, and the two devices would then build different levels. O2 tests
for this. If it ever happens, the start message also carries the accepted derived seeds, so
the client skips the validators.

**Joining mid-run** is not supported: players join in the lobby. If it is ever needed, a
`CellHealth` batch with every damaged cell of the loaded buildings rebuilds the state.

## 5. Code shape

`Scripts/Demolition/Online/`:
- `Model/` — `NetPacking`: health quantization, pose byte, cell addressing. Pure C#, so it
  is in the EditMode test build.
- `Controller/` — its own asmdef **`Demolition.Online`** (references `Demolition`,
  `Unity.Netcode.Runtime`, `Unity.Networking.Transport`, `Unity.Services.Multiplayer`). The
  core assembly never references netcode. Exclude this folder from the `Demolition` glob in
  `tools/unity-check/check.sh` (or add the packages to `setup.sh` later).
  - `OnlineSession` — the NetworkManager (tick rate 20, connection approval carrying
    `Application.version`), host and join, Relay and Sessions.
  - `RunNet` — one NetworkBehaviour per run, spawned by the host: start message, cell
    batches, damage requests, pickups, phase and score, player commands, desync check.
  - `NetPlayer` — on both player prefabs: owner-authority transform, pose and health
    variables, turns a non-owned player into a proxy.
- Menu: `OnlineView` in `Scripts/Menu/View/`, built through `UiFactory`.

Seams in the existing code (all in O1; offline nothing changes):

| Where | Change |
|---|---|
| new `Common/Controller/NetRole` | `Offline / Host / Client`, plus the hooks `Demolition.Online` fills in. Offline is today's behaviour. |
| `BuildingBlock.TakeDamage` | Client and the block is standing: chips, then forward instead of applying. Host: apply, then report the changed cell. Falling-block self-damage stays local. |
| `BuildingBlock` | `ApplyNetHealth(health, instigator)`: sets health, tint, destroys at 0. |
| `DestructibleBuilding` | `NetIndex` (its index in `LevelLayout.Buildings`, set by `LevelBuilder`), `TryGetBlock(cell)`. Overload crumble runs only when not a client. |
| `Pickup` | client: send a claim instead of collecting. Pickup index = index in `LevelLayout.Pickups`. |
| `Dynamite` | a copy flown for the other player is cosmetic: no damage. |
| `PlayerCombat`, `PlayerMotor`, `PlayerHealth` | off on a proxy; a proxy ignores damage. Guard `applyDamage` explicitly: animation events reach disabled components. |
| `RunController` | client mode: no `RunState` tick and no scoring; phase, timers, score and player status come from `RunNet`; banks the host's result. |
| `LevelBootstrap` | online: the host network-spawns the players; the client spawns none. Every device names them `Player_1` / `Player_2` (the name contract). |
| `PauseController` | online: the menu is an overlay, `Time.timeScale` stays 1. |
| `AdManager.BetweenRuns` | no ads while connected (D16). |

## 6. Flow

Main menu → join screen (local devices, as today) → **ONLINE** → `OnlineView`:
- **HOST** creates a Session with Relay and shows a short join code, big enough to read on a
  TV.
- **JOIN** takes the code from an on-screen keypad (touch, gamepad and keyboard).

Then the host's level select opens: the host's unlocks decide. The client sees "host is
choosing" and can open its shop. The host picks a level; `RunNet` sends the start message;
NGO scene management loads the Run scene on both; the countdown starts on a host tick.
On the results screen the host picks NEXT, RETRY or MENU, and the client follows. MENU on the
client leaves the session. Each player banks the run into its own profile.

Online: campaign and daily levels. Tutorial and training stay local. One player per device.

## 7. Milestones

| | Milestone | Done when |
|---|---|---|
| **O1** | **Seams**: §5 table, `NetPacking`, no packages installed | Offline play unchanged; all tests pass; new tests: `NetPacking` round trips, and two structures fed the same `CellHealth` stream end with the same standing cells. |
| **O2** | **Direct connect**: install NGO, UTP and Multiplayer Play Mode; host and join by IP; `RunNet`, `NetPlayer`, the client mode of `RunController` | Two Multiplayer Play Mode players finish a generated level together and the desync check passes. It still plays at 150 ms delay and 3 % loss (the UTP debug simulator). Every campaign level and 365 daily seeds give the same hash on PC and on an Android phone. Pin the NGO version. |
| **O3** | **Join codes**: Multiplayer Services (Sessions + Relay), anonymous sign-in, the UGS project link, `OnlineView` | A phone on mobile data joins a PC on Wi-Fi with a code and finishes a level. |
| **O4** | **Hardening** | The host leaves: the client gets a message and the menu. The client leaves: the host plays on, P2 is out. An app sent to the background, timeouts, a version mismatch message. A Relay/Authentication entry in the privacy disclosures (`monetization.md` checklist). A soak test with `BotPilot` driving both players. |
| **O5** | **Optional** | LAN without internet through `com.easysee.networkdiscovery` (already NGO + UTP). iOS needs the multicast entitlement and a local network usage string; Android needs a multicast lock. The VPS services from §8. |

## 8. The VPS

The VPS stays **out of the critical path** for O1–O4. Uses, in order of value:

1. **Small HTTP services, later:** a daily-seed leaderboard (roadmap post-v1) and a
   minimum-version check. A small service with SQLite runs in tens of MB of RAM.
2. **A fallback relay**, only if Unity Relay stops being cheap: a .NET UDP relay (LiteNetLib)
   plus an NGO transport for it. That is two pieces of custom code. You also own the uptime,
   DDoS exposure, and the latency of a single region. Trigger: the Relay bill passes the VPS
   cost.

Unity Relay price list when checked (2026-10-17, confirm on
<https://unity.com/products/gaming-services/pricing>): the first 50 average monthly CCU are
free, and 3 GiB of bandwidth per CCU up to 150 GiB a month. After that $0.09/GiB (US/EU).
At the budget in §3 that is about 6,000–7,000 free match-hours a month, and about $2 per
1,000 match-hours beyond.

A Unity headless server on the VPS (a dedicated, server-authoritative game) was rejected in
§1.

## 9. Decisions

| # | Question | Recommendation |
|---|---|---|
| D11 | Players online | 2, one per device (matches `Session.MaxPlayers`). Mixing local and online players: later, if ever. |
| D12 | Internet path | Unity Relay + Sessions. Self-hosted relay on the VPS only past the §8 trigger. |
| D13 | Rubble shared between devices? | No. Each device has its own rubble, so the other player can look like it stands on air or clips into rubble. Accepted. |
| D14 | Who decides damage to a player | The player's own device, from the debris it sees. |
| D15 | Out of bounds | The host decides with its own camera. Each device frames its own camera from both players, so the two views differ by about the latency; the 3 s grace absorbs that. Sync the camera only if playtests show it matters. |
| D16 | Ads online | None while connected: an ad pauses the app, and a paused host times out the client. A due interstitial shows after the session ends. |
| D17 | Pause online | An overlay only; the run keeps going. |
| D18 | When | O1 any time (offline, low risk). O2 onwards post-v1, per D4. |

## 10. Risks

| Risk | Mitigation |
|---|---|
| Float math in the generators differs between CPUs, so the devices build different levels | The hash check refuses the run; the O2 test; send the accepted seeds if it ever fires (§4). |
| A phone host goes to the background or gets a call | O4: the client gets a clear message; the run is short, so the loss is small. |
| The client sees its collapses one round trip late | Instant local chips and sound (§4 step 1). |
| A player's own device decides the damage it takes, which co-op allows | Fine without leaderboards. Daily-seed leaderboards need the host to check damage requests against reach (cell distance from the player) first. |
| NGO moves fast (2.13 as of July 2026) | Pin a version in O2 and upgrade on purpose. |
