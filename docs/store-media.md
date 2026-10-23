# Store media and clips: what we show and how we record it

**Status:** plan 2026-10-22; the recording tools built 2026-10-23 (§3), compile-checked and
model-tested without the editor, not yet run in Unity. It covers the
listing graphics of roadmap R4, the clip mode of R2 step 6, and the first marketing clips
(`marketing.md` §2–3). Section 6 lists what else Play and the first ads still need.

The approach: **record from the editor by script** where we can, and record on the phone
only what has to be real (touch controls, two devices playing online). A shot is a small
JSON file. The editor plays it and records it, and Python cuts, crops and captions the
result. When the art, the lighting or the HUD changes, we re-run the batch instead of
re-recording by hand.

---

## 1. What we need

| Asset | Spec | Used for | Needed by |
|---|---|---|---|
| Store screenshots | 4–8 landscape images, 16:9. Record them at 3840×2160 and upload them at 1920×1080 or larger, as PNG without alpha. Three or more at 1920×1080 or larger make a game eligible for Play's large-format promotion. Each needs alt text of 140 characters or less. | Phone, 7" and 10" tablet listings (the same set) | The basic listing, week of 9 Nov (R3) |
| Feature graphic | 1024×500, no alpha | Listing header | Done (`tools/artpipe/brand.py`) |
| Icon | 512×512 | Listing | Done (`brand.py`) |
| Preview video | A YouTube URL, public or unlisted. It must be embeddable, not age-restricted, with no monetization and no copyright claims. Only the first 30 s autoplay, muted, so the text on screen carries the message. Show gameplay in the first 10 s. | Listing (games are strongly advised to have one) | R4, December |
| Trailer | 30–40 s, 1920×1080, 60 fps. The same cut serves as the preview video. | Listing, press kit, YouTube | R4 |
| Short clips | 1080×1920, 7–15 s, 60 fps | YouTube Shorts, TikTok, Reels: 2–3 a week from now | Now |
| GIFs | 640–800 px wide, 10 s or less | Reddit, the press kit, the tester call | Mid-November |
| Ads videos (later) | Landscape, portrait and square, on YouTube, 10 s or more | Google Ads App campaign | Pre-registration or launch |
| Ads images (later) | 1200×1200, 1200×628, 1200×1500; plus 5 headlines (30 characters or less) and 5 descriptions (90 or less) | Google Ads | Pre-registration or launch |

Play's rules for listing graphics, including the video:
- Show the real game. Gameplay should be at least 80% of the video.
- A caption takes at most 20% of a screenshot.
- No ranking or promotion words: best, #1, top, new, free, sale.
- No calls to action (download now, play now), no device frames, no store badges.
- Don't repeat the same message across the short description, the screenshots and the video.

Ads aren't the store listing, so an ad version may end on a "Get it on Google Play" badge.

---

## 2. What we show

Pitch: **"Find the weak point. Bring the whole building down, with a friend."**

The hooks, strongest first:
1. A whole building falls from one well-placed hit (the support graph).
2. Blasts: dynamite at the supports, a row of gas tanks setting each other off.
3. Co-op, and online co-op between a phone and a PC or browser, which few phone games offer.
4. Big set pieces: a tower dropping, a gate turning to dust, a wrecking ball through glass.
5. Three cities that look different (Town by day, Port in rust, Neon at night with lights),
   plus a fresh street every day.

Rules for every clip:
- Something is already moving in the first second. Open on the hit, not on a walk-up.
- One idea per clip. End on the dust settling, so the clip loops well.
- No logo intro. The logo appears only on the trailer's end card.
- Hook text goes in the upper third. On 9:16, keep it out of the bottom 20% and the right
  edge, where TikTok and Shorts draw their buttons.
- Only real gameplay. Slow motion is fine; staged outcomes are not.

### 2.1 Directed shots

Each shot is a JSON file in `content-work/shots/` (section 3.2). The agent picks the exact
building and its weak point headless, from the level layouts that
`tools/unity-check/check.sh levels` writes. The places below are first picks, chosen from
the level previews.

| ID | What happens | Where | Camera | Slow motion | Used for |
|---|---|---|---|---|---|
| `one-hit` | P1 walks in and hits the building's weak-point support; the building comes down | `town-04` rowhouse (#0) | Frame the building | 0.3× for 1.5 s from the collapse | Short 1, trailer opening, screenshot 1, ads |
| `night-tnt` | P1 throws dynamite at a support and backs off; a flash, then the floors drop | `neon-02` arcade (#10) | Frame | 0.4× from the blast | Screenshot 3, a Short, trailer |
| `tower` | Dynamite at the base of the tower; it falls | `port-04` crane tower (#1), 1080×1920 | Tall frame (made for 9:16) | 0.5× | A Short, trailer climax, screenshot 4 |
| `gas-chain` | One hit on the first gas tank; the row sets itself off | `neon-04` yard (#16–19) | Pan along the row | None, then 0.5× on the last blast | A Short, trailer |
| `wrecking-ball` | Swings through a glass front | `neon-04` arcade (#3) | Frame | 0.5× on the first impact | Screenshot 5, a Short |
| `co-op` | P1 takes the weak point, P2 (the bot) smashes the rest | `town-04` rowhouse (#0) | Frame | 0.3× | Screenshot 2, trailer |
| `gate` | Knock out the wooden footing; the wall turns to dust | `town-02` gate (#12) | Frame | None | A Short |
| `headbutt` | Inside, jumps head-first into the ceiling | `town-01` shop (#1) | Tight follow | 0.5× | A Short (the funny one) |
| `keep-up` | P1 falls behind, the out-of-bounds sign counts down, P1 jumps the pit and it clears | `town-02` islands | The run's own camera, with the HUD on | None | Trailer, a Short |
| `cities-*` | The bot runs along one street per theme | `town-02` terrace, `port-04` silos, `neon-04` hotels | Follow | — | Trailer montage |

**Bot footage** runs alongside these: the `BotPilot` plays whole levels (one per theme, plus
`town-04`) while the recorder runs, with the HUD on. It is raw material for the trailer, and
a cutter turns its biggest collapses into short clips (section 3.4).

### 2.2 Store screenshots (order matters: the first three are seen most)

Screenshots are taken with the HUD on, because the HUD shows that this is a game. The touch
controls appear in one of them only (Settings ▸ touch controls ON in the editor). The
captions are drafts.

| # | Image | Caption | Alt text (draft) |
|---|---|---|---|
| 1 | `one-hit`, mid-collapse, Town | FIND THE WEAK POINT | A muscled demolition man punches a support column and a red brick row house collapses into dust. |
| 2 | `co-op`, both players and falling glass | WRECK IT WITH A FRIEND | Two demolition men smash a building from both sides while bricks and glass rain down. |
| 3 | `night-tnt`, the flash, Neon | BRING DYNAMITE | A stick of dynamite explodes against a neon-lit building at night and the floors above start to fall. |
| 4 | `tower`, leaning, Port | TOPPLE THE TOWER | A tall dockside crane tower topples onto the street. |
| 5 | `wrecking-ball`, with the touch controls | SWING THE WRECKING BALL | A wrecking ball swings through the glass front of a neon arcade. |
| 6 | Level select | 3 CITIES + A DAILY STREET | The level select with three cities, Town, Port and Neon, each with four streets and a daily street. |
| 7 | Results, three rainbow stars | BEAT PAR FOR RAINBOW STARS | The results screen: three rainbow stars for a finish within par, score and cash counting up. |
| 8 | Shop | UPGRADE YOUR CREW | The skill shop: upgrades for power, health, speed, dynamite and tools. |

The caption sits on a hazard-stripe bar in Black Ops One, the in-game title's look (gold with
an ink outline), no more than 15% of the height. Screenshot 7 must come from a real run:
played by you, or by the bot with the upgrades maxed, on a level it can finish within par.

### 2.3 Trailer (35 s, landscape; it is also the Play preview video)

| Time | Shot | Text on screen |
|---|---|---|
| 0:00–0:03 | `one-hit` | FIND THE WEAK POINT |
| 0:03–0:09 | `night-tnt`, `gas-chain` | — |
| 0:09–0:15 | `co-op`, then the online pair side by side (phone + PC, section 2.5) | WRECK IT WITH A FRIEND · ONLINE CO-OP |
| 0:15–0:20 | `wrecking-ball`, `headbutt`, `gate`, about 1.5 s each | — |
| 0:20–0:24 | `keep-up` | KEEP UP |
| 0:24–0:28 | `tower`, in slow motion (the biggest moment comes late) | — |
| 0:28–0:31 | `cities-*` (3 × 1 s), then the rainbow stars | 3 CITIES + A DAILY STREET |
| 0:31–0:35 | End card: the logo from `brand.py` | Store version: ONLINE CO-OP. Ad version: the Google Play badge. |

Music: one of the game's own tracks, cut on the beat ("Summon the Rawk" or "Rock_Theme",
CC BY 4.0). The attribution goes in the video description. The game's sound effects play
on top.

### 2.4 First Shorts (about 3 weeks of posts, up to the tester call)

| # | Clip | Hook text |
|---|---|---|
| 1 | `one-hit` | ONE HIT. WHOLE BUILDING. |
| 2 | `gas-chain` | GAS TANKS WERE A MISTAKE |
| 3 | `tower` | TOWER VS ONE STICK OF DYNAMITE |
| 4 | `co-op` (or the real online pair) | TWO PHONES. ONE WRECKING CREW. |
| 5 | `gate` | THE GATE TURNED TO DUST |
| 6 | `headbutt` | NO HELMET, NO PROBLEM |
| 7 | `night-tnt` | NIGHT SHIFT |
| 8 | Dev clip: the Building Lab showing weak points in red, cut to the same building falling in game | HOW THE GAME KNOWS WHERE TO HIT |

Clip 8 is for r/gamedev, r/Unity2D and Bluesky, where how-it-works posts do well.

### 2.5 Takes that need you

- **Online co-op on two devices:** the phone hosts and the editor joins (as in the
  2026-10-22 test). Record both screens at once: the phone with scrcpy and the editor with the
  recorder. They are cut side by side for the trailer and Short 4. This is the strongest
  proof of the co-op hook, so it is worth doing well: one building, both players on it.
- **Touch play on the phone:** one level played with the touch controls visible, recorded
  with scrcpy. It shows that the game plays well on a phone, and it is the real performance
  check (the dust, the worst collapse).
- **Local co-op with two gamepads** in the editor, recorded: bot-free footage that looks
  human, for the trailer.
- A run for screenshot 7, unless the bot manages one within par.

---

## 3. How we record

### 3.1 What is built (2026-10-23)

| Piece | What it does | Where |
|---|---|---|
| Unity Recorder | `com.unity.recorder` 5.1.7 in `Packages/manifest.json` (it brings Timeline). It records the Game view at a constant frame rate and sets `Time.captureFramerate`, so a heavy collapse never drops a frame in the video, even if the editor slows down. | Package |
| `ShotSpec`, `ShotPlanning` | Models: a shot file, its checks (`Errors()`), its lanes, the take's event log (`ShotLog`), the framing maths and the weak-point finder. Unit-tested (`ShotTests`), including every file in `content-work/shots`. | `Scripts/Demolition/Clips/Model/` |
| `ClipDirector` | Plays a shot in the Run scene (editor only): a temporary profile with the shot's upgrades and no music, the players placed at the building with their kit and driven through `ScriptedInputSource`s (walks go through the `BotPilot`), the camera framed, the HUD hidden, the time scale owned. Logs every collapse, break and blast at its video time to `take<n>.json`, plus any step that timed out. | `Scripts/Demolition/Clips/Controller/` |
| `ClipLab` | Tools ▸ Demolition ▸ Clip Lab: every shot file with Preview and Record, Record all, Stop, Still (the Game view at 2x, for menus), Open media folder. Takes run one after another, each in its own play mode. For agents through the Unity MCP: `ClipLab.Record("one-hit")`, `ClipLab.Record("one-hit", 1)`, `ClipLab.RecordAll()`, `ClipLab.Preview(id)`, `ClipLab.Status()`, `ClipLab.Stop()`, `ClipLab.Still()`. Its own asmdef (`Demolition.Clips.Editor`), compiled only when the Recorder is installed. | `Assets/Editor/Clips/` |
| Game hooks | `BotPilot.StopX` / `ThrowsDynamite` / `Arrived`, `LevelBootstrap.Players`, `RunController.IgnoreBounds`, `RunJuice.BaseTimeScale`. Nothing changes in normal play. | |
| `ShotRunTests` | PlayMode: every shot except bot footage plays to its end without recording, and no step timed out. Run it after changing a shot or the levels. | `Tests/PlayMode/` |
| `tools/media/media.py` | Python, Pillow and ffmpeg. `crop` (9:16, 16:9, 1:1), `cut` (clips around the biggest collapses in a take's log), `contact` (a sheet of frames from every take, timeouts in red), `gif`, `store` (captioned screenshots, ads images, alt texts; refuses words Play bans), `edit` (a Short or the trailer from a cut list, text in the title's look, music laid under). Tested on generated videos. | `tools/media/` |
| Shot files | 21: the clips of §2.1, `cities-*`, `broll-*` (whole levels with the bot) and `store-1…5` (16:9 with the HUD, stills at 3840×2160). Buildings and weak points picked from the level dump (`check.sh levels` now writes each building's `weak`: x, y, w, h, share it brings down). | `content-work/shots/` |
| Cut lists | `short-1…7` (9:16), `trailer.json` (16:9, store version), `store.json` (the 8 screenshots with captions and alt texts). They point at `take1`; change the take number after picking. | `content-work/edits/` |

The roadmap's "clip mode in development builds" became this editor tool. A clip mode on the
phone is skipped: footage from the phone is there to show the touch controls, so it keeps
the HUD.

### 3.2 A shot file

```json
{
  "id": "one-hit",
  "level": "town-04",
  "building": 0,
  "offset": -5,
  "upgrades": 5,
  "camera": {"mode": "frame", "pad": 2},
  "width": 1920,
  "height": 1920,
  "length": 35,
  "steps": [
    {"p": 1, "do": "walk", "to": "weakpoint"},
    {"p": 1, "do": "hit", "until": "collapse", "cells": 15, "seconds": 12},
    {"p": 0, "do": "wait", "until": "collapse", "cells": 15, "seconds": 25},
    {"p": 0, "do": "slowmo", "scale": 0.3, "seconds": 1.5},
    {"p": 0, "do": "wait", "seconds": 5}
  ]
}
```

- **Where:** `level` (a campaign id), `seed` (0: the level's own), `building` (index in the
  level's buildings, props included; -1: the spawn), `offset` (players start this far from
  its left edge; the take nudges it by 0.37 per take so takes differ), `players` (1 or 2).
- **Kit:** `upgrades` (every shop upgrade at this level), `dynamite`, `weapon` (`axe`,
  `wreckingball`) with `weaponHits`.
- **Lanes:** `p` 0 is the director, 1 and 2 the players; write `p` on every step. Each lane
  runs its steps in order, side by side. The shot ends when lane 0 runs out (or at
  `length`). Seconds are video seconds.
- **Player steps:** `walk` (`to`: `weakpoint`, `building` (its left edge), `end` (past it), or
  `x` from the left edge; the bot breaks what's in the way and stops where a swing would
  reach the target), `hit` (for `seconds`, or `until` an event), `tnt`, `jump` (`x` + or -
  moves while jumping), `back` (walk left for `seconds`), `swap`, `wait`, `bot` (the
  `BotPilot` plays, dynamite included, for `seconds` or `until`).
- **Director steps:** `wait` (`seconds`, or `until` an event with `seconds` as the
  timeout), `slowmo` (`scale` for `seconds`; the lane goes on meanwhile), `still` (`name`;
  `stillScale` times the frame), `end`.
- **Events:** `collapse` (of at least `cells`), `break`, `blast`, `over` (the run ended).
- `weakpoint` is the ground-level block whose loss brings down the most of the building,
  replayed the way `BuildingValidator` judges weak points.
- **Camera:** `frame` (the building and the players' start, plus `pad`, standing on the
  ground), `pan` (`from` to `to` over `seconds`, at `size`), `follow` (the front player
  plus `lead`, at `size`), `run` (the run's own camera, scrolling and out-of-bounds
  included).
- **Frame:** `width` × `height`, `fps`, `takes`. Square frames keep the subject in the
  middle 9/16, so every crop holds it.
- **What the director takes care of:** a temporary profile (your save is untouched) with the
  shot's upgrades, quality HIGH and no music; the level's pickups between the start and the
  building are removed, so the kit is exactly the shot's; recording again adds takes after
  the ones on disk (take4, take5…) instead of overwriting them.

### 3.3 What clip mode changes while a shot plays

- **No letterbox:** the camera rect goes full screen, with no bars, whatever is decided for
  the game (section 5, decision 1).
- **HUD, touch controls and countdown hidden** unless the shot asks for them. Scrolling and
  out-of-bounds are off, except with the `run` camera.
- **Smooth slow motion:** physics runs at a fixed 50 Hz (`Fixed Timestep` 0.02) and the
  falling blocks don't interpolate. At 60 fps the debris would judder, and badly so in slow
  motion. The director sets `Time.fixedDeltaTime = Time.timeScale / 60`, one physics step
  per recorded frame. `RunJuice`'s hit-stop puts the time scale back to 1 afterwards; in a
  shot, it returns to the director's scale instead (`RunJuice.BaseTimeScale`). Hit-stop and
  shake run on video time while a recorder fixes the frame rate (`CameraShake.Clock`), so a
  slow editor frame doesn't cut them short in the video. The same judder shows in normal play
  on a 60 Hz phone, players included: every `Rigidbody2D` in the prefabs and the generated
  blocks has interpolation off. Fix it for the game in R2, with Interpolate on the players
  and the falling blocks, or a 1/60 s timestep.
- **Music off; sound effects recorded.** The music is laid over in the edit, so cuts don't
  jump inside the track.

### 3.4 One recording, three formats

Clips are recorded at **1920×1920** with the subject framed in the central 1080×1080 square.
One take then gives three formats by cropping: 1080×1920 (the middle strip), 1920×1080 (the
middle band) and 1080×1080 (scaled down). It is the same take in every format, with nothing
to re-run (`media.py edit` crops from the master by itself). The ads images (1:1, 1.91:1,
4:5) come from the stills in the same way. A tall subject gets its own frame instead:
`tower` records at 1080×1920, because a square frame of a 30-storey tower is mostly sky.
Levels have 12 rows of ground below their lowest street (`LevelLayout.GroundDepth`), so a
square frame never sees under it; `ShotTests` checks that, and that no frame starts left of
the level, for every `frame` and `pan` shot.

Store screenshots are their own shots (`store-*`): 1920×1080 with the HUD, stills at
`stillScale` 2, so 3840×2160.

**Highlights:** the director's sidecar lists every collapse with its time, its size in cells
and its position. `media.py cut` takes the N biggest and cuts from 2 s before the hit to
3 s after it. On bot footage this mines clips without anyone watching the whole recording.

**Takes and picking:** physics varies a little between takes, so each shot records 3 takes.
`media.py contact` puts frames from every take on one sheet. The agent proposes the best
take and you confirm.

Media files stay out of git (`content-work/media/`, gitignored). Back them up to a drive.

### 3.5 Recording the phone

[scrcpy](https://github.com/Genymobile/scrcpy) (Android 11 and later for audio) shows the
phone on the PC and records it, sound included:

```
scrcpy --record=content-work/media/phone/take1.mp4 --max-fps=60 --video-bit-rate=16M --no-window
```

The game runs full screen, so the status bar never shows. Android's own screen recorder
(with "media sound") is the fallback.

---

## 4. Session plan, 2026-10-23

**Setup (you, about 20 minutes):**
- Open the project: Unity installs the Recorder (and Timeline). Commit the
  `Packages/packages-lock.json` change it makes.
- `winget install Gyan.FFmpeg Genymobile.scrcpy`, and Python with Pillow for `media.py`.
- Run Claude Code locally in the project, with the Unity MCP connected, so the agent can
  start shots (`ClipLab.Record(...)`) and look at the contact sheets.

**The session:**
1. **Decisions** (section 5): the letterbox, and the name check. Both change what we record.
2. **Smoke test:** Clip Lab ▸ `one-hit` ▸ Preview, then Record with 1 take. Check that the
   video is smooth at 60 fps, that the sound is in sync, that the weak point is the one
   that falls, and that the crops work
   (`python tools/media/media.py crop content-work/media/raw/one-hit/take1.mp4`). Then run
   `ShotRunTests` (PlayMode): any shot whose steps time out is listed and gets tuned first.
3. **Batch:** Record all (the clips × 3 takes, the store shots × 2, the bot footage once:
   roughly 30 minutes, unattended).
4. **Pick takes:** `python tools/media/media.py contact content-work/media/raw`; the agent
   proposes a take per shot, you confirm, and the take numbers go into
   `content-work/edits/*.json`.
5. **Takes with you:** online co-op (the phone with scrcpy into
   `content-work/media/phone/online-host.mp4`, the editor client with Window ▸ General ▸
   Recorder into `content-work/media/raw/online-client.mp4`), touch play on the phone,
   local co-op with gamepads, and a run for screenshot 7.
6. **Stills:** level select, results and shop with Clip Lab ▸ Still, saved as
   `content-work/media/stills/level-select.png`, `results.png`, `shop.png`; then
   `python tools/media/media.py store content-work/edits/store.json`.
7. **Edit:** `media.py edit` on `short-1…3` and on `trailer.json` (run `brand.py` first for
   `brand/logo.png`).
8. **Upload:** the trailer to YouTube as unlisted. Check YouTube Studio ▸ Copyright for
   claims on the music before the video goes anywhere.

What the session should produce: 8 screenshots, 8 Shorts ready to post, a 35 s trailer
draft, 3 GIFs, and the material for the tester call.

## 5. Decisions for the start of the session

| # | Question | Recommendation |
|---|---|---|
| 1 | The letterbox: `Main Camera 1` draws into 75% of the screen's height (bars top and bottom). Keep it on phones? (`overnight-2026-10-17.md`, question 2) | **Full screen.** On a 20:9 phone the bars leave a strip about 3:1. Store screenshots would carry 25% black, or show a game that isn't the one shipped. Decide it before recording the screenshots. |
| 2 | The name (roadmap D25). "Demolition Men" is one letter away from *Demolition Man*, a Warner Bros. film from 1993 that had licensed games. | Search Play, EUIPO TMview and USPTO **before** opening the marketing accounts and handles. A rename after clips are out loses the audience. Clips carry the name only on the end card, so they are cheap to redo; handles and the listing are not. |
| 3 | The look being recorded: are the URP editor steps (`rendering.md`) done? | If not, do them first. Neon's lights, bloom and blast flashes change every night shot. |
| 4 | Upgrades in shots: `one-hit` with maxed power is still real (the shop sells it). | Fine. The upgrades are part of the game, and the screenshots show the shop. |

Re-recording is cheap. When the crack overlays (R2) or the audio change, run the batch again.

---

## 6. The rest of publishing and advertising

Besides the media, the release needs the following. The status of items marked
*check* isn't recorded in the repo; tick them off tomorrow.

| Item | Who | Status | When |
|---|---|---|---|
| Name check (D25) | You | Open | First, before handles and the listing |
| Play Console account, identity verification ($25) | You | *check* | R1 |
| Upload keystore, with two backups | You | Open (roadmap §2.2) | R1 |
| First AAB on the internal testing track | You | *check* | R1 |
| Listing text: title (30), short description (80), full description (4000), screenshot alt texts | Agent drafts, you paste | Drafts below | With the screenshots |
| Category (Game ▸ Action or Arcade), tags, contact e-mail `OGpriv@proton.me` | You | Open | Basic listing |
| App content: privacy policy URL (done), contains ads (yes), app access (everything without login), content rating (IARC questionnaire: cartoon violence, no blood), target audience 13+, Data safety (AdMob: advertising ID, approximate location, app interactions, diagnostics; the relay sees IP addresses), advertising ID declaration (the AdMob SDK adds `AD_ID`) | Agent drafts the answers, you submit | Open | Week of 9 Nov, before the closed test |
| EU trader status and which address is public (D24) | You | Open | R4 |
| Closed test, 12+ testers × 14 days | You | Open | From 16 Nov |
| Tester call: a post with 1 GIF and 1 screenshot | Agent drafts, you post | Media from this session | Mid-November |
| Accounts: YouTube (needed anyway for the preview video), TikTok, Instagram, Bluesky, itch.io, Discord, with one handle across them | You | Open | After the name check |
| Paid ads | — | Not yet | See below |

**Advertising starts organic.** The app must be on Play before money can buy anything.
Google Ads runs **pre-registration campaigns** for Android: the app needs an AAB on a track
and pre-registration set up in the Play Console, and it must launch within 90 days. That
fits December, once production access is granted. `marketing.md`'s rule still holds: spend
only once the listing converts, and use the clips that did best for free as the ads.

### Listing text drafts

- **Title** (30 characters at most): `Demolition Men` (14). With a search subtitle:
  `Demolition Men: Wreck It Co-op` (30). Settle it after the name check.
- **Short description** (80 at most): *Find the weak point. Bring the whole building down,
  with a friend.* (66)
- **Ads headlines** (30 at most): *One hit. Whole building.* (24) · *Two phones. One wrecking
  crew.* (30) · *Find the weak point. Bring it down.* is 35, too long; *Wreck streets with a
  friend* (27).

---

## 7. Risks

| Risk | Mitigation |
|---|---|
| The recorder's sound drifts from the picture when the editor runs below 60 fps | Smoke test first. The music is added in the edit anyway; if the effects drift, record them in a second pass at real time. |
| A Content ID claim on a CC BY track (third parties sometimes claim Kevin MacLeod's music) | Upload unlisted and check for claims before linking. If a claim appears, swap the track; Play refuses preview videos with claims. |
| Physics plays differently on each take | Three takes per shot; pick one. |
| Footage goes stale after art, lighting or HUD changes | Shots are files; re-run the batch. |
| The editor shows effects a slow phone has off (Settings ▸ EFFECTS) | Record with the defaults the game ships with. The phone takes show the phone. |
