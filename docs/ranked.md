# Ranked dailies and fair scores

**Status (2026-10-30):** K1–K5 built; `RankedBotTests` passes in Unity (solo and co-op, every
level), `RealLogTests` replays those logs on the server's runtime and every cheat on them is turned
down; the server runs at `ranked.oeds.space`, where a run from the editor over HTTPS and every cheat
sent to it were judged the same. Never tested: online co-op's logs, a phone, a browser.
- **Models** (`Ranked/Model/`, EditMode tests): the run log (`RunLog`), its seal (`Seal`), the
  numbers it's checked with (`RankedRules`), the referee (`RunVerifier`), the day's pick
  (`RankedDay.Pick`), names (`PlayerName`), the server's messages (`RankedApi`).
- **The game:** every damage to a standing block names its `HitCause` (`BuildingBlock.TakeDamage(
  amount, instigator, cause, from)`, online too: the client's damage request carries it);
  `RunRecorder` writes the log on the deciding device; ranked runs play with a fresh save's kit and
  any action (`SaveData.rankedActions`); the actions read their numbers from `RankedRules`. NAME on
  the first screen (`PlayerIdentity`). RANKED in the level select (`Meta/View/RankedView`: today's
  level and board, the action per player, PLAY, report a row by pressing it twice; development
  builds also TEST OFFLINE); `RankedSession` gets an attempt, plays it, seals and sends the log,
  and fetches the ghost (`Ranked/View/GhostRunner`); the results show the place
  (`Ranked/View/RankedResultView`, SEND AGAIN); a ranked RETRY is a new attempt. Development builds
  print the verifier's verdict at the end of every ranked run.
- **The server** (`server/ranked`, `dotnet test`, its README): attempts, sealed submits replayed by
  `RunVerifier`, the boards, ghosts, reports, holds, admin review, daily backups, 180 days kept.
- **Left:** a ranked run from a phone and a browser; the privacy policy (updated
  in `server/relay/Relay/wwwroot/privacy.html`, live once the relay is deployed again) and Play's
  data safety form (§8); a few weeks with rejections only recorded, reading `/admin/runs`, then
  `Ranked__HideRejected=true`; K6.

Ranked dailies give the game a reason to show off to people who aren't your friends: one
level a day for everyone, a board for solo and one for co-op, and later the crew's looks
next to every name (roadmap *Cosmetics shop*). A board is only worth anything if its scores
are real, so this doc starts with the anti-cheat. The goal is **every cheat that breaks the
game's rules is caught**: the only scores left to doubt are bots playing perfectly within
the rules, and those are visible (ghosts, §6).

The game runs on the player's device, so the device can be made to say anything: the APK
and the WebGL build can be taken apart, memory edited, the save rewritten. A secret key in
the build doesn't change that, because it can be taken out of the build. What the device
can't do is make the server believe a run that the game's own rules don't allow.

---

## 1. Decisions

| # | Decision | Why |
|---|---|---|
| K-D1 | The device plays, the server referees: the device sends a log of the run, the server replays it with the game's own rules and computes the score itself. | No lag, no game server, works offline until the submit; the server's work is milliseconds per run. A headless Unity server (live or replaying inputs) costs a round trip on every hit or needs physics that compute the same on every platform, and still lets a perfect bot through. |
| K-D2 | Ranked runs have no upgrades, and every action is available. | The save is the player's own file: the server can't tell earned upgrades or bought actions from edited ones. It's also fairer: skill wins, not grinding. |
| K-D3 | Two boards a day: solo, and co-op (local and online alike). | Asked for (2026-10-28). Co-op scores are the crew's. |
| K-D4 | The server picks the day's level: a campaign template and a seed per UTC day. | One level for everyone whatever their time zone, and the campaign's order in the build doesn't have to match the server's. |
| K-D5 | A rejected run isn't told so: its player sees it on the board, nobody else does. | A cheater can't learn which fake gets through. Off for the first weeks (§5). |
| K-D6 | The log is binary and sealed with a key per attempt (§5). | Not the lock (§1), but it makes the format hard to learn and old or captured logs useless. |
| K-D7 | The server is its own service next to the relay, not part of it. | The relay stays a forwarder with no game code; a verifier bug can't take co-op down. |
| K-D8 | A player is a device: NAME is typed on the first screen, and a device has one name and one entry per board (its best run of the day counts). | Asked for (2026-10-28). The id is a hash of the device's identifier (`PlayerIdentity`); WebGL has none, so a browser keeps a random one. Two phones or a cleared browser make two players: their runs still have to pass §4, and new players per IP are limited. |
| K-D9 | Names are held to `PlayerName` on both sides: A–Z, 0–9, spaces and dashes, 3 to 16, nothing on its list (English and Dutch, look-alikes undone). | Names are public. The list is a start; REPORT (K5) catches the rest, and Play wants a way to report what players write anyway. |
| K-D10 | The server has its own name (`ranked.oeds.space`), not a path under the relay's. | Its Caddy block is added like the relay's and the relay's stays untouched; one DNS record. |
| K-D11 | Runs, attempts, reports and players with no run left are forgotten after 180 days; backups are kept 7 days. | A retention the privacy policy can state; a board older than half a year is history. |
| K-D12 | Online co-op entries carry both names: the joiner's id and name come with the join (NGO's connection data, after the version). | The host sends the run; the server needs to know whose it is too. |

## 2. Flow

1. **Start.** The player picks RANKED (solo or co-op by the crew that's in) and their action. The
   game asks the server for an attempt: `POST /start {player, name, mode, partner, partnerName,
   rules}`. The answer: the attempt id, its key, the day, the template level id and the seed.
2. **Play.** The game builds the level from that template and seed, with the ranked kit,
   and plays it as any run. A `RunRecorder` writes the log (§3) as it goes.
3. **Submit.** At the finish (or a failed run), the game seals the log with the attempt's
   key and sends it: `POST /submit {attempt, log}`. The results screen says SENDING, then the
   place.
4. **Board.** `GET /board?day&mode&player`: the top 50 and the player's own row. `GET /ghost`:
   the best verified run's log, raced as a ghost.

An attempt is good for one run, within its run time plus a few minutes. Attempts per player
and per IP are limited per day.

## 3. The run log

Bits, not text (packed like `NetPacking`), a few KB per run:

- **Header:** format version, rules version (§7), mode, number of players, online or local,
  day, template id, seed, the layout's hash (`LevelLayout.Hash`), each player's action.
- **Pose track:** where each player is, 10 times a second: x and y at 1/32 unit, facing.
- **Events**, each with its time in ms since GO:
  - **Damage** to a standing block: the building and the block (its index in the
    `BuildingSpec`), the amount as the device applied it (exact float), the cause (fist,
    axe, wrecking ball, headbutt, glass touched, dynamite, gas tank, an action by kind,
    overload crumble, a co-op partner's hit online), the player, the point it came from (the
    swing's centre, the blast, the strike) and whether the block broke.
  - **Pickup** taken (its index in the layout), **throw** (where from), **blast** (where,
    what), **action** started and struck (where, what it picked), **out**, **rejoin**,
    **resurrect**, **finish** or **fail**.
- **Summary:** the device's own score, grade and time, only to diagnose a mismatch.

Debris isn't in it: a falling or settled block never scores (`BuildingBlock` credits a block
once), so what the physics does with it can't change the score.

## 4. What the server checks

The server compiles the same models the game does (`LevelGrammar`, `BuildingGrammar`,
`BuildingStructure`, `RunScore`, `ActionMeter`; they already run outside Unity in
`tools/unity-check`), with the campaign's level and archetype JSON. It rebuilds the level
from the template and seed and checks the layout hash. Then it replays the events in order:
it applies each damage to its own copy of the block's health, breaks the block when the
health runs out, lets `BuildingStructure` say what detaches, and scores it with `RunScore`.
The device's claims are only accepted when they match: a block the device says broke must
break on the server, and the score is the server's.

This works because nothing physics decides can change the score. A standing block only
loses health from causes the server can check: a swing, a headbutt, touching glass, a blast,
an action, and overload crumble. Falling debris hurts players and itself, never standing
blocks.

| Cheat | Check |
|---|---|
| A made-up score (a script, an edited memory value) | Ignored: the server's own `RunScore` over the replay. |
| Collapses that can't happen | `BuildingStructure` decides what detaches; the device's word isn't asked. |
| More damage per hit | Each damage against its cause's maximum: the weapon's profile for that material, the headbutt's at full speed, the blast's, the action's; times the buffs active then (coffee, the partner's lean). One hit per block per swing, blast, slam ring, intern's punch and crumble tick. |
| Hitting faster | Swings per player against the weapon's clip (fists every 0.2 s, the axe and the ball every 0.44 s; the coffee's 1.5×); the intern on the punching clip's beat; crumble ticks 0.25 s apart. |
| Hitting from afar or through walls | The swing's centre within reach of the player's pose at that time; the block within the swing's radius of it. |
| Teleporting, flying, a speed hack | The pose track against the run speed (with the coffee), the jump (`JumpProfile`, as `TraversalValidator` uses it: no rising longer than a jump lasts, no higher than a jump above the tallest thing around), knockback only after a blast nearby; never inside a standing block. |
| Finishing too soon | The finish no sooner than running full speed from spawn to finish takes, and the pose there. |
| A better time | The log's time is the finish's: it's what the board sorts ties by and shows. |
| A log made up on the spot | A run can't be longer than the time since its attempt began (the game's clock only runs behind the real one). |
| Dodging a penalty | A pose behind the camera past the out-of-bounds grace, or down a pit, without an **out** event. The edge is the game's own `ScrollEdge`, run from the log's poses: it doesn't depend on the device's screen or zoom, so every player is held to the same line, and a wider screen has no more room behind it (roadmap D27). |
| Extra dynamite | Throws only after dynamite pickups (positions in the layout; they fall straight down from there) were taken within reach; one stick out at a time. |
| Free actions | The server keeps each player's `ActionMeter` from its own replay; an action starts only on a full meter, once at a time, and strikes within reach. |
| Edited upgrades or actions | K-D2: the kit is the same for everyone. |
| Replaying or editing a captured log | §5: sealed with this attempt's key. |
| Sending someone's ghost as your own | The same log, to the byte, already on the board is turned down. An edited copy is in §6. |
| Endless tries, many accounts | Attempts per player and per IP, per day. |

Tolerances: a pose is sampled 10 times a second while hits land between samples, an online
partner's pose lags the host's view by a round trip, and the stress that decides overload
crumble is float maths that an ARM phone may round differently from the server. So the reach
and movement checks allow some slack, and crumble is checked against the server's stress
with a margin, not to the bit. The slack is set from honest logs: the bot's runs of every
level (a PlayMode test records and verifies them) and the closed test's (§5).

*As built (K1):* where the device's timing can't be known exactly, the server's estimate leans
the device's way: the action meter counts every block the player breaks (the game leaves out
what its own action breaks) and never drains while an action plays; crumble may lack
`CrumbleMargin` of overload. Swings are held to their clip two ways: never closer than half a
clip apart (not for an online partner, whose hits arrive in bunches), and no more than the clip
allows in any 2 s. The pose's legs and chest may never be inside a standing wall (glass and the
walk-through supports aside). Every slack is a named constant in `RankedRules`.

## 5. Keeping the log hard to fake

None of these is the lock (§1), they make the first step harder:

1. **Binary.** No text, no field names.
2. **Sealed per attempt.** The server gives each attempt a fresh key. The log is encrypted and
   signed with it (HMAC-SHA256 in counter mode, written in the model so it works the same in
   WebGL, tested against .NET's). A captured log can't be read, changed or sent again, and a
   script without the game can't make one.
3. **Machine code.** Android builds with IL2CPP (and code stripping), WebGL always does: there
   are no C# assemblies to decompile.
4. **The checks live only on the server**, with their tolerances. Nobody can test a fake at home.
5. **Rejections are silent** (K-D5): the game never makes a log the server rejects, unless we
   have a bug, so a rejected run means a changed game. Its player still sees it on the board,
   nobody else does, and their later runs are hidden too.

A bug of ours would hide honest players. So for the first weeks the server only *records*
every rejection with its reason, and every run is shown; we read the reasons, fix the bugs and
the slack, and only then switch the hiding on.

Float maths differ by runtime (the editor's Mono keeps more precision than IL2CPP and the server's
.NET), so nothing the replay compares may depend on the last bit: the level's hash rounds, and a
score a point off passes (the board shows the replay's). `RealLogTests` (`server/ranked`) replays
the logs `RankedBotTests` wrote on the server's runtime, then changes each one the way a careful
cheater would, tidying up everything that follows from the change, and expects every cheat turned
down.

## 6. What it doesn't catch

A bot that plays perfectly within the rules writes a log that is true. That holds for every
game, run on a server or not: a bot sends inputs like a person. The same goes for what physics
decides, which the server doesn't replay:
- **Not dying.** Falling debris and traps hurt players on the device only, so a game changed to
  take no damage writes a log that passes; pits and the camera's edge are checked, the rest isn't.
- **Hovering low.** Standing on rubble can't be told from standing on air, so the track is only
  held to the height of the level as it started.
- **An edited copy of the day's ghost** (one pose moved) is a true log of a run someone played.
- **Slow motion.** The attempt may last 40 minutes, so a run can be played slower than it says.

Against all of these:
- **Ghosts.** The log is also a ghost: anyone can watch a top run play out next to their own.
- **REPORT** on a board row.
- **Review.** A score far above the rest waits for a look before it's shown.

## 7. Versions

The rules the server checks with are the game's numbers (damage profiles, clip timings, the
jump, the actions' areas), mirrored in `RankedRules` with a version number; `RankedRulesTests` reads the
player, dynamite and run prefabs and the swing clips and fails if they drift apart. The actions'
numbers are constants in their controllers (`BurpWave`, `CranePallet`, ...): K2 makes those read
`RankedRules`, so they can't drift. A build that changes them bumps the version. The
server checks with one version at a time and tells an older build "update the game to play
ranked", so the game and the server go out together.

## 8. The server

`server/ranked/` (its README has the API, the settings and the deploy): an ASP.NET Core app
(.NET 10) in its own container next to the relay on the VPS, on its own name
(`ranked.oeds.space`, K-D10). It compiles the game's models from
`Assets/Scripts/Demolition/**/Model/` with `tools/unity-check/UnityShim.cs` and ships the level
and archetype JSON from `Assets/Content`. SQLite on a volume: players (the device's id, K-D8, and
its name, K-D9), attempts, runs (score, time, verdict and reason, the log for its ghost),
reports. Limits: attempts per player and IP per day, new players per IP, upload size, CPU time
per verify.

On the VPS, next to the relay: the container and `/opt/demolition-ranked/data` (the database and
its daily copies: back it up), a Caddy block for its name, all by `server/ranked/deploy/deploy.sh`
as the relay's script does it. Everything is forgotten after 180 days (`RetentionDays`, K-D11).

Before it goes live:
- the privacy policy (`server/relay/Relay/wwwroot/privacy.html`, §4 *Ranked boards*) is updated in
  the repository: deploy the relay again to put it online;
- Play's data safety form: **Device or other IDs** (the player id) and **User IDs** (the
  nickname), and **App activity: other actions** (the run logs), all collected, not shared, for
  app functionality and fraud prevention, sent encrypted, deletable on request; Play's policy on
  user-generated content wants a way to report it (REPORT) and to act on reports (`/admin`).

## 9. Milestones

| | Milestone | Done when |
|---|---|---|
| K1 ✅ | The run log and `RunVerifier` as models | EditMode tests: logs round-trip; honest made-up runs pass and each cheat in §4 fails. |
| K2 ✅ | The game records: the damage cause on every hit, `RunRecorder`, the ranked kit; a co-op partner's cause online | `RankedBotTests` (PlayMode): the bot plays every campaign level ranked, a different action each, and the verifier passes all of them. |
| K3 ✅ | The server: start, submit, verify, store, record-only rejections | `dotnet test`; deployed. |
| K4 (built) | Ranked in the game: RANKED in the level select, sealing, submit, the boards, names | A ranked run from a phone and a browser lands on the board. |
| K5 (built) | Ghosts, REPORT, review of outliers; rejections hidden after the record-only weeks | The switch (`HideRejected`) after a few weeks of `/admin/runs` without false rejections. |
| K6 | Looks next to names and on ghosts | With the cosmetics shop. |
