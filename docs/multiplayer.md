# Online co-op plan

**Status (2026-10-17):** O1 (seams) is in. The relay (§8) runs on the VPS at
`wss://relay.oeds.space/ws`. O2 code is in: NGO 2.13.3, NativeWebSocket and Multiplayer Play
Mode installed, `RelayTransport`, `OnlineSession`, `NetPlayer`, `RunNet`, `OnlineView`. It
compiles against the real packages, but it has not run in the editor yet: O2's "done when"
(§7) is still open. What was built differs from the first plan where §2, §3, §5 and §6 say
"as built". Online co-op ships with the Android v1.0 (roadmap D4, decided 2026-10-20).

Two players on two devices play one run together: PC, phone or a browser (WebGL), in any
mix. This doc picks the topology, says exactly what crosses the wire and who decides what,
and splits the work into milestones. The goals: **no paid services** (the only server is
the VPS we already have), **no game simulation on that server**, **WebGL works**, and **as
few bytes as possible**. It builds on the support-graph design
(`building-destruction-design.md` §5), which is what makes the destruction cheap to sync.

---

## 1. Topology: a player hosts, the VPS only forwards

```
  Device A: NGO host                                  Device B: NGO client
  ┌─────────────────────────────┐                     ┌───────────────────────────┐
  │ Player_1 (local)            │                     │ Player_2 (local)          │
  │ Player_2 (proxy)            │ ◄─── VPS relay ───► │ Player_1 (proxy)          │
  │ decides: blocks, pickups,   │   (wss, forwards,   │ decides: its own player   │
  │ run phase, score            │    runs no game)    │ (movement, hits, damage)  │
  └─────────────────────────────┘                     └───────────────────────────┘
```

- **Listen server.** One player's device runs Netcode for GameObjects (NGO) as host: server
  plus its own player. The other device joins as a client.
- **WebSockets everywhere.** A browser can't send UDP or accept incoming connections. It can
  only open outgoing WebSockets (`wss://` from an HTTPS page). So every device, PC and phone
  too, connects out to a small relay on the VPS over WebSocket. The relay pairs them by join
  code and forwards their bytes (§8). Because nobody listens, a browser tab can host as well
  as join, and carrier NAT on phones stops mattering. Client side: NativeWebSocket (MIT, one
  API for WebGL and native) behind a small NGO transport.
- **No dedicated server.** A headless Unity server is one Unity process per match (hundreds
  of MB of RAM, a share of a CPU core), and it adds a round trip for both players. In co-op
  nobody gains by cheating, so a neutral referee buys nothing. A small VPS could run a
  handful of matches. As a relay that only forwards bytes, it carries thousands.
- **No lockstep or rollback.** Physics2D is not deterministic across machines (design doc,
  Appendix A), so player and debris physics would drift apart. Only the discrete structure
  model is deterministic, and that is all this plan relies on.
- **The cost of TCP.** WebSockets run on TCP, so one lost packet holds back the ones behind
  it until it is resent. Under this design that only delays what the *other* device sends:
  the remote player's movement and, for the client, its own collapses. A player's own
  movement never waits on the network. If playtests on bad mobile links show hitches, native
  builds get a UDP leg later (O5).

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
| Weapon, its hits left, dynamite | owner | one int (`NetPacking.PackKit`), `NetworkVariable`, sent on change; the proxy's HUD line shows it (as built) |
| Damage to a standing block | owner finds the hit, host applies it | `DamageRequest` to host; host answers with `CellHealth` to all |
| Detached cells, falling debris, rubble | each device | derived from `RemoveCells` (deterministic); debris uses local physics and local random |
| Stress lean, tremble, demolition meter | each device | derived from the same structure state: 0 bytes |
| Overload crumble | host | `CellHealth`, like any other damage |
| Pickups | host | client asks (`Claim`), host grants first come (`Taken`) |
| Dynamite flight | owner | `Thrown {pos, vel}` and `Exploded {pos}`; the other device flies a cosmetic copy |
| Run phase, countdown, timers | host | `State` snapshot, 10 per second (as built: no tick-stamped phase) |
| Score, multiplier, grade | host | points per player in the `State` snapshot; the client grades its own copy of the score and banks it |
| Job sheet (bonus objectives) | host | the crew's tally (weapon hits, dynamite thrown, damage taken, biggest collapse; each player's weapon hits reach the host in its kit) in the `State` snapshot; the client judges the sheet from it and banks its own objectives |
| Out of bounds, rejoin, resurrect | host | `RunState` on the host; its snapshot replays the same `PlayerOut` / `PlayerRejoined` events in the client's `RunController`, which kills and revives as offline (as built: no `Kill` / `Revive` commands) |
| Cash, records, unlocks | each player's own profile | each device banks the host's final result into its own `profile.json` |

## 3. What crosses the wire

### Efficiency rules

1. **Send decisions, not simulation.** Cell health and events, never block transforms,
   debris or rubble. Everything the models can derive is derived on each device.
2. **Few NetworkObjects.** One per player. `RunNet` uses named messages only, so it is not a
   NetworkObject at all (as built). Never one per building, block, pickup or stick of dynamite. NGO checks every spawned object for changes each
   tick, so this saves CPU as well as spawn messages.
3. **Change-only.** `NetworkVariable`s send only when they change. Nothing is polled.
4. **One batch per tick.** The host gathers every cell that changed during a tick and sends
   them in one message. NGO already batches each tick's messages, so the transport sends
   one WebSocket message per tick at most.
5. **Quantize.** Positions as half floats; health as 7 bits; buildings, cells and pickups as
   small varint indices (`BytePacker.WriteValueBitPacked`).
6. **Tick rate 20 Hz** (NGO's default is 30), with interpolation smoothing the remote
   player. Headers (TCP/IP, TLS, WebSocket) cost more than the payload, so fewer messages
   per second is the biggest saving available.
7. **No garbage on the hot path.** The per-tick messages (`CellHealth`, `DamageRequest`) are
   NGO named messages written with `FastBufferWriter` on `Allocator.Temp`. Rare events
   (pickups, run commands, start, results) can be plain RPCs. The transport and the relay
   reuse their buffers.
8. **No `NetworkAnimator`, no `NetworkRigidbody2D`.** The pose byte drives the proxy's
   animator. Proxies are kinematic bodies, so local debris still bounces off them.

### Messages

| Message | Direction | Payload | When |
|---|---|---|---|
| Player transform | owner → others | X, Y half floats | ≤ 20/s while moving |
| Pose, health | owner → others | 1 byte each | on change |
| Kit (weapon, hits, dynamite) | owner → others | 4 B | on change |
| `Score` | host → client | per award: player, points, position ≈ 11 B | with the cell batch, only if points were awarded |
| `DamageRequest` | client → host | building (varint), cell (varint), amount (half float) ≈ 5 B | per block a client hits |
| `CellHealth` batch | host → client | per cell: building, cell, health 7 bits + instigator 1 bit ≈ 4 B | once per tick, only if something changed |
| `Claim` / `Taken` | client → host / host → all | pickup index (+ player) | per pickup |
| `Thrown` / `Exploded` | owner → others | position (+ velocity) | per stick of dynamite |
| `Ready` | client → host | nothing | once per run, when the client's Level scene is up |
| `State` | host → client | layout hash, phase, countdown, elapsed, last call, destroyed fraction, per player status bits + timers + points, unattributed points, host paused, the job sheet's tally ≈ 42 B | 10 per second |
| Start | host → client | campaign level index or daily date | once per run, before NGO loads the scene |
| `Check` (dev builds) | host → client | hash of the standing cells per loaded building | every 5 s |

A punch that breaks a wall costs about 10 bytes. A dynamite blast that damages 15 blocks
costs about 60 bytes, once. The steady cost is the two player transforms and the `State`
snapshot (about 400 B/s; as built it replaces the phase, score and command messages, so a
lost or late message never leaves the client's run in the wrong phase).

As built, the named messages are `DM.Ready`, `DM.Cells`, `DM.Score`, `DM.Damage`, `DM.Claim`,
`DM.Taken`, `DM.Thrown`, `DM.Blast`, `DM.State`, `DM.Check`, all sent reliable and sequenced.
The host caps a client's `DM.Damage` at 1000 per hit and always credits it to player 2.
`DM.Score` follows the cells it scored, so the client's popup shows as the block goes.

Each device streams level segments by its own camera, so a building can reach the client a
moment after the host started damaging it: the client keeps the latest health of each cell of
a building it hasn't loaded and applies it once the building has started. Pickups taken on
either device are remembered for the level (`Pickup.WasTaken`), so streaming never brings one
back. A client claims a pickup only if its player can use it now (dynamite not full, someone
to resurrect) and asks again a second later if the host didn't answer.

### Budget

Per device, each way: about 20 messages/s × (≈ 75 B of TCP/IP, TLS and WebSocket headers
+ ≈ 20 B of payload), plus TCP acknowledgements ≈ **2 KB/s**. Through the VPS, both players
together: about **8 KB/s in + out, or 30 MB per match-hour**. Host CPU: the same game as
offline, plus a few hundred bytes of serialization per tick.

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
  `Unity.Netcode.Runtime` and NativeWebSocket). The core assembly never references netcode.
  Exclude this folder from the `Demolition` glob in `tools/unity-check/check.sh` (or add the
  packages to `setup.sh` later).
  - `RelayTransport` — an NGO `NetworkTransport` that only ever connects out: to the relay
    as host or as joiner. It sends each NGO batch as one binary WebSocket message. Every NGO
    delivery mode becomes reliable and ordered (TCP). shortcut: unreliable sends are never
    dropped when the connection backs up; add that if the remote player lags on bad links.
    The relay closes a paired room after 60 s without binary data (WebSocket pings don't
    count). As built, every binary message starts with a kind byte (data, ping, pong, drop):
    a ping every second measures the round trip for NGO and keeps the room open, and drop
    is how the host kicks the client. Relay text messages start with a letter, so they never
    collide with a kind byte. The relay's close code becomes the reason the menu shows.
    `LinkWatch` (`Online/Model/`, tested) judges the link on each device's own clock: no
    answer from the relay within 10 s is "can't reach the server"; paired, 10 s without a
    word from the peer (a backgrounded phone, a hidden tab, a dead network) ends the session
    (the host drops a frozen client and plays on, a client shows "the host stopped
    responding"); a 10 s gap in this device's own frames means it was the frozen one, and it
    ends the session itself on waking. Shorter hitches of its own are forgiven. The
    protocol and close codes are in `server/relay/README.md`.
  - `OnlineSession` — the NetworkManager (tick rate 20, connection approval carrying
    `Application.version`, one client), host and join; added at runtime, so no scene holds
    it. It fills the `NetRole` hooks, sends the start message before NGO loads the Run
    scene, spawns the network players with ownership and ends the session (back to the menu
    with the reason) when either side leaves.
  - `RunNet` — as built a plain MonoBehaviour per run, created when the level is built, that
    speaks NGO named messages (see §3): cell batches, damage requests, pickups, dynamite, the
    `State` snapshot, the desync check.
  - `NetPlayer` — on the network player prefabs `Prefabs/Online/NetPlayer_1/2` (variants of
    the player prefabs, made by Tools ▸ Demolition ▸ Online ▸ Build Network Prefabs, which
    also fills `GameConfig.netPlayerPrefabs`): owner-authority `NetworkTransform`, pose and
    health variables, turns a non-owned player into a proxy and names both `Player_1` /
    `Player_2`.
- Menu: `OnlineView` in `Scripts/Menu/View/`, built through `UiFactory`.
- NativeWebSocket: installed by UPM git URL, pinned to commit `c612a4f` (2.0.7).
- The relay URL is `GameConfig.relayUrl` (`wss://relay.oeds.space/ws`; `ws://localhost:5080/ws`
  for a relay started with `dotnet run`).

`server/relay/` at the repo root, outside the Unity project: the relay (§8).

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
| `RunController` | client mode: no `RunState` tick and no scoring; phase, timers, score and player status come from `RunNet` (`RunState.ApplySnapshot`, `RunScore.Restore`, `NetFraction`); banks the host's result. |
| `LevelBootstrap` | online: the host network-spawns the players; the client spawns none. Every device names them `Player_1` / `Player_2` (the name contract). |
| `PauseController` | online: the host's pause freezes both devices (the client reads it from the `State` snapshot and shows "the host paused the game"); a client's own menu is an overlay, `Time.timeScale` stays 1. |
| `AdManager.BetweenRuns` | no ads while connected (D16). |

O1 put in the guards: a client doesn't tick `RunState`, doesn't score and doesn't report
depletion. O2 added `RunState.Snapshot` / `ApplySnapshot` (the client fires the same events
as the host), `RunScore.Restore`, the `NetRole` hooks `LoadScene`, `Leave`, `LevelBuilt`,
`PickupTaken`, `DynamiteThrown`, `DynamiteExploded` and `LocalPlayer`, and
`DestructibleBuilding.TryGetByNetIndex`. Online, `LevelBootstrap` waits for both network
players before it starts the run. Cell indices use `BuildingSpec.Width` from the layout.

## 6. Flow

Main menu → join screen (local devices, as today) → **ONLINE** → `OnlineView`:
- **HOST** opens a room on the relay and shows its join code, big enough to read on a TV.
- **JOIN** takes the code from an on-screen keypad (touch, gamepad and keyboard).

Then the host's level select opens: the host's unlocks decide. The client sees "host is
choosing" and can open its shop. The host picks a level; `OnlineSession` sends the start
message; NGO scene management loads the Level scene on both; the host spawns both players;
the countdown runs on the host and reaches the client in the `State` snapshot.
On the results screen the host picks NEXT, RETRY or MENU, and the client follows (it sees
"the host picks what's next", SHOP and LEAVE). LEAVE on the client, or MENU's leave in the
pause menu, ends the session. Each player banks the run into its own profile.

Player 2 leaving mid-run lets the host play on: its players stay (spawned with
`DontDestroyWithOwner`, then despawned without destroying), the session ends, and
`RunState.Leave` takes player 2 out for good (no rejoin, no Resurrect, "LEFT THE GAME" on the
HUD). The rest of the run and anything after it is offline. Outside a run, or when the host
leaves, the session ends for both with the reason on screen. A joiner the host turns away
(another version) gets the reason and leaves the host's room open.

Online: campaign and daily levels. Tutorial and training stay local. One player per device.

## 7. Milestones

| | Milestone | Done when |
|---|---|---|
| **O1** | **Seams**: §5 table, `NetPacking`, no packages installed | Offline play unchanged; all tests pass; new tests: `NetPacking` round trips, and two structures fed the same `CellHealth` stream end with the same standing cells. |
| **O2** | **Netcode against a local relay**: install NGO, Multiplayer Play Mode and NativeWebSocket; write `RelayTransport` and the relay (`server/relay/`, run with `dotnet run` on the dev machine); `RunNet`, `NetPlayer`, the client mode of `RunController`. *Code in and pinned (NGO 2.13.3, NativeWebSocket c612a4f); the play tests below are open.* | Two Multiplayer Play Mode players finish a generated level together through the local relay, and the desync check passes. It still plays with 150 ms delay and 3 % loss added by a network simulator on the socket (e.g. clumsy on Windows). Every campaign level and 365 daily seeds give the same hash on PC, on an Android phone and in WebGL. Pin NGO and NativeWebSocket. |
| **O3** | **Deploy and join codes**: the relay on the VPS behind TLS with the §8 limits, `OnlineView`, a WebGL build on a free HTTPS host (e.g. itch.io) | A phone on mobile data, a PC and a browser tab can each host and join each other through the VPS with a code, and finish a level. |
| **O4** | **Hardening** | The host leaves: the client gets a message and the menu. The client leaves: the host plays on, P2 is out *(both in since O2)*. The relay is down: a clear message, local play unaffected. A phone app or a browser tab sent to the background (browsers throttle hidden tabs, so a hidden host stalls), timeouts, a version mismatch message. The privacy policy says the relay sees players' IP addresses (`monetization.md` checklist). A soak test with `BotPilot` driving both players. *Built 2026-10-22: the connect timeout, the background/stall handling (`LinkWatch`), the version message (connection approval), the privacy policy. Tried the same day with a Pixel and the editor through the VPS relay, each side sent to the background mid-run: Android drops a backgrounded app's socket within about 6 s, so the other side hears it from the relay first (client: THE HOST LEFT; host: plays on alone); the woken phone ends its own session (THE GAME WAS IN THE BACKGROUND TOO LONG). Left: the relay-down and version-mismatch messages on a device, a hidden browser tab (needs the WebGL build), the soak test.* |
| **O5** | **Optional** | A UDP leg for native builds if TCP hitches show up in playtests: LiteNetLib in the transport, and the relay bridges UDP and WebSocket peers. LAN without internet for native builds only (a browser can't host a LAN game): `com.easysee.networkdiscovery` with UnityTransport. |

Open in O2:
- Run Tools ▸ Demolition ▸ Online ▸ Build Network Prefabs once after the packages import.
- A client's hit on a building the host hasn't streamed in yet is dropped (the host's camera
  would have to trail the client's by the 30-unit look-ahead).

## 8. The relay on the VPS

An ASP.NET Core minimal app (current .NET LTS) in `server/relay/` with one WebSocket
endpoint. It runs as a Docker container behind the Caddy container that already fronts the
VPS's other sites. It never runs game code or reads game data.

**TLS is required.** A WebGL page served over HTTPS may only open `wss://`. Caddy gets and
renews the Let's Encrypt certificate for the relay's name (`relay.oeds.space`, its own A
record) and proxies WebSockets to the container over the shared Docker network. The relay
trusts `X-Forwarded-For` from that network only, so the per-IP limits see players, not
Caddy. The game connects by the hostname, so moving the VPS is only a DNS change. Deploy:
`server/relay/README.md`.

1. **HOST** connects to `wss://<host>/ws?v=<protocol>&role=host`. The relay makes a room and
   replies with a 6-character code from an alphabet without look-alikes (no 0/O, 1/I).
2. **JOIN** connects with `role=join&code=<code>`. The relay pairs it with the host, or
   closes it with a reason the game shows: unknown code, room full, old protocol version.
3. From then on it copies each binary message to the other peer. Control messages (code,
   peer joined, peer left) are text messages, so they never mix with game data.
4. The room closes when the host leaves, and the client gets a disconnect.

shortcut: 2 peers per room, so data needs no client id; a room for more players prefixes
one byte of client id.

**Load.** About 8 KB/s and 30 MB per match-hour (§3). RAM: the relay and Caddy together
need on the order of 100 MB. One CPU core forwards far more messages than a small VPS's
network port carries, so the limits are the VPS's **monthly traffic quota and port speed**.
Check both. 1 TB a month is about 30,000 match-hours.

**Hardening.** It is a public endpoint, so none of this is optional:
- Caps: a maximum number of rooms, 2 peers per room, a maximum message size (larger ones
  close the connection), handshake and idle timeouts, WebSocket keep-alive pings.
- Connection and join attempts are rate-limited per IP, so codes can't be brute-forced.
- Nagle off (`NoDelay`) on the relay's sockets, so small messages aren't held back.
- The container runs as a non-root user on a read-only filesystem with every capability
  dropped and a memory cap, publishes no ports, and restarts by itself.
- It logs room counts and bytes per second, not IP addresses.
- Optional: an `Origin` allowlist (the domains that serve the WebGL build). That stops other
  websites from using the relay from a browser, not native clients.

**Free alternatives considered:**

| Option | Why not |
|---|---|
| Unity Relay | Supports WebGL (wss), and the free tier is generous, but usage past it is billed. There is no budget for surprises. |
| Epic Online Services relay | No WebGL support, no maintained NGO transport, a native SDK per platform and an Epic login flow. |
| Steam Datagram Relay | PC only, and needs a Steam store app (Steam Direct fee). |
| WebRTC with a TURN server on the VPS | UDP-like in browsers, but Unity's WebRTC package doesn't build for WebGL. That means a second, JavaScript implementation plus signalling. |
| UDP only (LiteNetLib) | No browsers. Kept as the optional O5 leg for native builds. |
| A dedicated Unity server on the VPS | Rejected in §1. |

## 9. Decisions

| # | Question | Recommendation |
|---|---|---|
| D11 | Players online | 2, one per device (matches `Session.MaxPlayers`). Mixing local and online players: later, if ever. |
| D12 | Internet path | **Decided (2026-10-17):** a self-hosted WebSocket relay on the VPS. No paid services: there is no budget. WebSockets because WebGL can't use UDP. |
| D13 | Rubble shared between devices? | No. Each device has its own rubble, so the other player can look like it stands on air or clips into rubble. Accepted. |
| D14 | Who decides damage to a player | The player's own device, from the debris it sees. |
| D15 | Out of bounds | The host decides with its own camera. Each device frames its own camera from both players, so the two views differ by about the latency; the 3 s grace absorbs that. Sync the camera only if playtests show it matters. |
| D16 | Ads online | None while connected: an ad pauses the app, and a paused host times out the client. A due interstitial shows after the session ends. |
| D17 | Pause online | **Changed (2026-10-22):** the host's pause freezes the run on both devices; only the host resumes it. A client's own pause is an overlay only; the run keeps going. |
| D18 | When | O1 any time (offline, low risk). O2–O4 before the Android launch (roadmap R2, D4 since 2026-10-20); O5 only if playtests ask for it. |
| D19 | Platforms online | **Decided (2026-10-17):** PC, phones and WebGL, in any mix; each can host. |

## 10. Risks

| Risk | Mitigation |
|---|---|
| Float math in the generators differs between CPUs, so the devices build different levels | The hash check refuses the run; the O2 test; send the accepted seeds if it ever fires (§4). |
| TCP holds back messages after a loss, so the remote player hitches on bad mobile links | Only the other player and the client's own collapses lag, never your own movement. Interpolation absorbs small gaps. The O5 UDP leg for native builds if playtests demand it. |
| The VPS is down, or its traffic quota runs out | Online is unavailable with a clear message; local play is unaffected. Docker restarts the relay. |
| One VPS region adds latency for players far from it | Same as the TCP risk: only remote things lag. Accepted for a free service. |
| Abuse of the relay's public endpoint | The §8 hardening list. |
| A phone host goes to the background, or a browser host's tab is hidden | O4: the client gets a clear message; the run is short, so the loss is small. |
| The client sees its collapses one round trip late | Instant local chips and sound (§4 step 1). |
| A player's own device decides the damage it takes, which co-op allows | Fine without leaderboards. Daily-seed leaderboards need the host to check damage requests against reach (cell distance from the player) first. |
| The transport is ours, and NativeWebSocket is a small community library | Pin NGO and NativeWebSocket; the relay checks a protocol version; upgrade on purpose. |
| WebGL as a platform touches more than netcode (saves, audio unlock, memory, input) | Out of this doc; roadmap D9 lists WebGL now, and the rest needs its own check before a web release. |
