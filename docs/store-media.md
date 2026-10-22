# Store media and clips: what we show and how we record it

**Status:** plan, 2026-10-22, for the first recording session on 2026-10-23. It covers the
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
| `one-hit` | P1 walks in and hits the building's weak-point support; the building comes down | `town-01` terrace | Frame the building | 0.3× for 1.5 s from the collapse | Short 1, trailer opening, screenshot 1, ads |
| `night-tnt` | P1 throws dynamite at a support and backs off; a flash, then the floors drop | `neon-02` cluster | Frame | 0.4× from the blast | Screenshot 3, a Short, trailer |
| `tower` | Dynamite at the base of the tower; it falls | `neon-02` or `port-04` tower | Tall frame (made for 9:16) | 0.5× | A Short, trailer climax, screenshot 4 |
| `gas-chain` | One hit on the first gas tank; the row sets itself off | `port-02` yard:5 | Pan along the row | None, then 0.5× on the last blast | A Short, trailer |
| `wrecking-ball` | Swings through a glass front | A Neon building with glass | Frame | 0.5× on the first impact | Screenshot 5, a Short |
| `co-op` | P1 and P2 hit two supports from both sides | `town-03` cluster | Frame | 0.3× | Screenshot 2, trailer |
| `gate` | Knock out the wooden footing; the wall turns to dust | `town-02` gate | Frame | None | A Short |
| `headbutt` | A jump head-first into a block | Any Town building | Tight follow | 0.5× | A Short (the funny one) |
| `keep-up` | P1 falls behind, the out-of-bounds sign counts down, P1 jumps the pit and it clears | `town-02` islands | The run's own camera, with the HUD on | None | Trailer, a Short |
| `cities` | No action: a slow pan along one street per theme | `town-04`, `port-04`, `neon-04` | Pan | — | Trailer montage |

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
| 3 | `night-tnt`, the flash, Neon | BRING DYNAMITE | A stick of dynamite explodes against a neon-lit tower at night and the floors above start to fall. |
| 4 | `tower`, leaning, Port | TOPPLE THE TOWER | A ten-storey dockside tower topples sideways onto the street. |
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
| 0:28–0:31 | `cities` (3 × 1 s), then the rainbow stars | 3 CITIES · A DAILY STREET |
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

### 3.1 Pieces to build (agent, compile-checked without the editor)

| Piece | What it does | Where |
|---|---|---|
| Unity Recorder | `com.unity.recorder` (5.x) in `Packages/manifest.json`. It records the Game view at a constant 60 fps and sets `Time.captureFramerate`, so a heavy collapse never drops a frame in the video, even if the editor slows down. | Package |
| `ShotSpec` | Model: parses a shot file and turns it into a list of timed steps. Unit-tested. | `Scripts/Demolition/Clips/Model/` |
| `ClipDirector` | Plays a shot in a run: places the players, gives them their kit, drives them through a `ScriptedInputSource`, owns the camera and the time scale, and logs every collapse and blast to a JSON sidecar. Editor only. | `Scripts/Demolition/Clips/Controller/` |
| `ClipLab` | Tools ▸ Demolition ▸ Clip Lab: a list of shots, with Preview, Record (N takes) and Record all. It hands the shot over through `PlayerPrefs`, like the Level Lab's play request, starts the Recorder, and stops at the end of the shot. Static `RecordCli(id, takes)` and `RecordAllCli()` so the Unity MCP (`RunCommand`) can drive it. Its own editor asmdef, compiled only when the Recorder package is present (as `Demolition.Lighting` does with URP). | `Assets/Editor/Clips/` |
| `tools/media/media.py` | Python and ffmpeg. `crop` makes the 9:16, 16:9 and 1:1 versions; `cut` makes highlight clips from a sidecar; `contact` makes a contact sheet of frames from every take; `gif`; `store` captions the screenshots and makes the ads images; `edit` renders a Short or the trailer from a JSON cut list, with text overlays in Black Ops One and a music track. | `tools/media/` |

The roadmap's "clip mode in development builds" becomes this editor tool. A clip mode on the
phone is skipped: footage from the phone is there to show the touch controls, so it keeps
the HUD.

### 3.2 A shot file

```json
{
  "id": "one-hit",
  "level": "town-01", "seed": 0,
  "players": 1,
  "start": { "building": 2, "offset": -5 },
  "kit": { "power": 5, "dynamite": 0, "weapon": "none" },
  "camera": { "mode": "frame", "building": 2, "pad": 2 },
  "hud": false,
  "steps": [
    { "p": 1, "do": "walk", "to": "weakpoint" },
    { "p": 1, "do": "hit", "until": "collapse" },
    { "on": "collapse", "do": "slowmo", "scale": 0.3, "for": 1.5 },
    { "do": "wait", "for": 4 }
  ],
  "stills": ["collapse+0.5"],
  "frame": [1920, 1920],
  "takes": 3
}
```

- **Steps:** `walk` (to an x, a building or its weak point), `hit` (for some seconds, or
  until a block breaks or the building collapses), `tnt`, `jump`, `swap`, `wait`, `slowmo`.
  `weakpoint` is the ground-level block whose loss detaches the most cells. The
  `BuildingValidator` already replays every ground-level block, so the director asks it.
- **Camera:** `frame` (a building's bounds plus padding, the ground at the bottom), `pan`
  (from one x to another over some time), `follow` (the run's own `ScrollCamera` without
  scrolling), and `run` (the run as it plays, scrolling and out-of-bounds included, for
  `keep-up` and the screenshots).
- **Profile:** a temporary profile, as `BotRunTests` uses, so your save is untouched. `kit`
  sets the upgrades and pickups the shot needs.

### 3.3 What clip mode changes while a shot plays

- **No letterbox:** the camera rect goes full screen, with no bars, whatever is decided for
  the game (section 5, decision 1).
- **HUD, touch controls and countdown hidden** unless the shot asks for them. Scrolling and
  out-of-bounds are off, except with the `run` camera.
- **Smooth slow motion:** physics runs at a fixed 50 Hz (`Fixed Timestep` 0.02) and the
  falling blocks don't interpolate. At 60 fps the debris would judder, and badly so in slow
  motion. The director sets `Time.fixedDeltaTime = Time.timeScale / 60`, one physics step
  per recorded frame. `RunJuice`'s hit-stop puts the time scale back to 1 afterwards; in a
  shot, it must return to the director's scale instead. The same judder shows in normal play
  on a 60 Hz phone, players included: every `Rigidbody2D` in the prefabs and the generated
  blocks has interpolation off. Fix it for the game in R2, with Interpolate on the players
  and the falling blocks, or a 1/60 s timestep.
- **Music off; sound effects recorded.** The music is laid over in the edit, so cuts don't
  jump inside the track.

### 3.4 One recording, three formats

Clips are recorded at **1920×1920** with the subject framed in the central 1080×1080 square.
One take then gives three formats by cropping: 1080×1920 (the middle strip), 1920×1080 (the
middle band) and 1080×1080 (scaled down). It is the same take in every format, with nothing
to re-run. The ads images (1:1, 1.91:1, 4:5) come from the stills in the same way. If a crop
looks cramped, the shot file can ask for its own frame size instead.

Store screenshots are recorded separately at 3840×2160, with the HUD laid out for 16:9.

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

**Before the session (agent, tonight or at the start):** build section 3.1, write the shot
files and pick their buildings and weak points headless.

**Setup (you, about 20 minutes):**
- `winget install Gyan.FFmpeg Genymobile.scrcpy`, and Python with Pillow for `media.py`.
- Run Claude Code locally in the project, with the Unity MCP connected, so the agent can
  start shots and look at the contact sheets.

**The session:**
1. **Decisions** (section 5): the letterbox, and the name check. Both change what we record.
2. **Smoke test:** record `one-hit` once. Check that the video is smooth at 60 fps, that the
   sound is in sync, and that the three crops work. Fix anything before recording the batch.
3. **Batch:** every directed shot × 3 takes (about 15 minutes of recording, unattended).
   Meanwhile, the bot plays four levels and the recorder runs.
4. **Pick takes** from the contact sheets.
5. **Takes with you:** online co-op (phone + editor), touch play on the phone, local co-op
   with gamepads, and the run for screenshot 7.
6. **Stills:** the screenshots in section 2.2, captioned by `media.py store`.
7. **Edit:** the first three Shorts and a rough cut of the trailer (`media.py edit`).
8. **Upload:** the trailer to YouTube as unlisted. Check YouTube Studio ▸ Copyright for
   claims on the music before the video goes anywhere.

What the session should produce: 8 screenshots, 8 Shorts ready to post, a 35 s trailer
draft, 3 GIFs, and the material for the tester call.

---

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
