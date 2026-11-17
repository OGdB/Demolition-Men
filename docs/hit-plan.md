# Hit plan: what would make Demolition Men big

**Status:** proposal, 2026-11-12. It amends `marketing.md` and `roadmap.md` §4; nothing here
is decided. Market facts were looked up on 2026-11-12 and carry their source. Anything marked
*estimate* is a judgement, not a measured number.

## 1. Verdict

**The game can be a hit. The launch plan as written cannot make it one.**

`marketing.md` leans on Play's own traffic, two or three posts a week and a €100–250 ad burst.
None of those brings players in volume. The game is strong exactly where hits come from: a
collapse reads in a muted three-second clip, a new level costs nothing, and a browser can join
a phone. So the change is in distribution, not in the game:

1. **Web portals first**, before Play. The portal brings the players and shows the numbers.
2. **An invite link that joins**: a friend taps a link and is in the room, with no install.
3. **Clip volume.** The Clip Lab can make forty clips in one session. Use that.
4. **Steam after Play**, aimed at the June 2027 Next Fest.
5. **Only features that bring players in or get them to the first collapse** (§5.1) until
   the launch. Since 27 Oct the v1.2 work (job sheets, looks, ranked) was built, while the
   repo shows no accounts, no posts and an open name check.

Nobody can plan a hit. This plan buys more chances: four channels where the platform supplies
the audience, each with a number that says carry on or stop (§7).

"Hit" here means 100,000 players across web, Play and Steam within six months of launch, or a
platform feature. "It works" means 10,000 Play installs in 60 days at day-1 retention of 30%
and a rating of 4.3. Both are *estimates*: a target to steer by.

## 2. Assumptions

- One developer with coding agents, part-time, in the Netherlands. Unity 6, a Windows PC, an
  Android phone, a VPS that already runs the relay and the ranked server. No Mac.
- €100–500 in total (roadmap D21). Strong at programming and tooling; no audience yet.
- The goal is reach. Income comes second.
- The repo is the only source for the project's state, as of the last commit (30 Oct). If
  accounts, posts or the closed test have started since, skip those steps.

## 3. Where the game stands

Scored with the usual seven criteria, 1–5 each.

| Criterion | Score | Why |
|---|---|---|
| Simplicity | 2 | It is built, but five systems (relay, ranked server, ads, purchases, three platforms) each still need device tests. |
| Content cost | 5 | Levels and buildings are generated and validated; the bot plays them. |
| Virality | 4 | The collapse sells itself in a clip. Play produces nothing a player can share yet. |
| Monetization | 3 | Interstitials at natural breaks, one rewarded offer, REMOVE ADS. |
| Competition gap | 3 | Play's "demolition" search is held by SayGames, Homa and the Demolition Derby series. The destruction games found are solo sandboxes; no co-op one turned up. |
| Trend timing | 3 | Not verified. Demand exists (Building Destruction: about 11.4M installs, an aggregator's estimate). |
| Creator fit | 3 | Tooling is the strength, marketing the gap; the Clip Lab closes part of it. |
| **Total** | **23/35** | One point short of a clear go. Moves 2 and 3 below add the shareable result, which lifts Virality to 5. |

## 4. The moves

### 4.1 Web portals first (December)

Why: it is the only free channel where the platform sends the players. The WebGL build, touch
controls and browser co-op exist already. Web numbers arrive before the Play launch, so the
first five minutes can be tuned on real data instead of guesses.

- **CrazyGames, Basic Launch.** No SDK needed and no income yet. Limits: first download at most
  50 MB, 50 MB in total without their SDK, at most 1,500 files, content within PEGI 12, no
  outside ads, no outside login. Its dashboard shows players, average playtime, gameplay
  conversion and retention, which makes it the analytics this project lacks. Games they select
  move to a Full Launch: their SDK, ads through it with a revenue share (the percentage is not
  published), straight into gameplay, their invite links
  ([CrazyGames requirements](https://docs.crazygames.com/requirements/intro/), Nov 2026).
- **Poki: wait.** Poki prefers web exclusivity (other portals closed, Steam and mobile stay
  open) and its developer portal is in a closed beta with limited access
  ([Poki deals](https://sdk.poki.com/deals), [Poki SDK](https://sdk.poki.com/), Nov 2026);
  its revenue share is not published.
  Exclusivity is a lock-in. Pitch Poki only with CrazyGames numbers in hand, and compare.
- **Your own copy on the VPS**, for the invite link (4.2). itch.io stays as planned.
- **YouTube Playables:** developer access is by an interest form
  ([Google](https://developers.google.com/youtube/gaming/playables), Nov 2026). Fill it in;
  expect nothing.

Work (agent): build WebGL and measure it against 50 MB; switch compression from Gzip to
Brotli, which CrazyGames recommends
([export tips](https://docs.crazygames.com/resources/export-tips)); try it in a phone browser;
finish the O3 tests in `multiplayer.md`. Before a store link goes into the portal build, read
their quality guidelines: the requirements page does not say whether outside links are allowed.

Two players on one keyboard goes in with this build (§5.1, F3): it is what puts the game on
the portals' two-player shelves.

### 4.2 The invite link, and a result to share (move from v1.1 into v1.0)

This is the one feature worth adding before launch. Everything else waits.

- **SHARE in `OnlineView`**, beside COPY: it sends `https://play.<domain>/?r=ABCDEF` through
  the phone's share sheet (the clipboard elsewhere; `Platform.Copy` exists).
  **Built 17 Nov:** the link is `<playUrl>/<version>/?r=ABCDEF`, for example
  `https://play.example.org/1.0.0/?r=ABCDEF`. The button is there once `GameConfig.playUrl`
  is set; until then the host has COPY alone, as before.
- **The browser build reads the link** (`Application.absoluteURL`), skips the lobby and joins
  the room. The friend plays within seconds, with no store and no account.
  **Built 17 Nov:** it skips the title too, plays on the touchscreen or the keyboard, and lands
  where a guest who typed the code lands. A stale code, a full room, another version or no
  server: the lobby, under a notice that says which. The link is read once per start; reloading
  the page tries again.
- **After the run**, a browser guest on Android sees the Play badge (your own copy only).
  **Built 17 Nov as a plain GET THE APP button**, and only once `GameConfig.storeUrl` holds the
  listing's address. The badge itself is Google's art, which the project doesn't have.
- **SHARE on the results screen**: the rubble card (§5.1, F2), a few lines of text with a
  link that opens the same street in the browser. The card is built; its link is not: it
  waits for the web build's address, and a link to the same street for the build reading it.
- **Mind the version check.** A phone on an older build must still find a browser build that
  matches it. Put the protocol version in the link's path and keep the last two builds on the
  VPS. Redeploying the web build joins the release checklist.
  **Built 17 Nov with the game's version** (Player Settings ▸ Version, now 1.0.0), not the
  relay's protocol number: the version is what a host checks before it lets a guest in, and
  the protocol number has been 1 for every build. So the version has to go up with every
  release that changes online play; the Android build script raises only the version code.

Measure: invites sent per player, guests who finish a run per invite, guests who install.
The counters (§5.1, F0) give the first two: `invite` counts rooms whose link was shared, and
a guest's finished run is a `level_done` from `web` with `online` 2. Installs are not counted.

**Nothing here has run in a browser or on a phone**; the link's text and reading have tests.
Your part, in this order:
1. Host the browser build on the VPS, one folder per version: `https://<play>/1.0.0/`. Keep
   the last two. The address itself serves the newest (the rubble card's `play it:` line goes
   there). It can't be itch.io: its page frames the game from another address, and the room
   in the link never reaches it.
2. Put that address in `playUrl` in `Resources/GameConfig`, and build the browser build and
   the phone build from the same commit with the same version. If the relay's
   `Relay__AllowedOrigins` is set, add the address's origin.
3. Phone invites browser: HOST on the phone, SHARE, send the link to yourself, open it on the
   laptop. The laptop should show JOINING and then CONNECTED with no press. Then the same with
   a second phone's browser as the guest: touch controls, and both finish a level.
4. The risk to try first: SHARE takes the phone out of the game. Stay in the chat app for
   half a minute, come back: is the room's code still on screen, and does the friend get in?
   Then let the friend open the link while you are still in the chat app: after 10 seconds
   they should read THE HOST STOPPED RESPONDING, and get in by reloading once you are back.
   If the room is gone when you come back, the relay must learn to hold it (multiplayer.md §10).
5. Each failure, on the guest: a made-up code (`?r=ZZZZZZ`), a room that already has two
   players, a browser build of another version, the relay stopped. Each should end in the
   lobby under a notice, never a blank screen. Then leave a room you joined by link and go
   back to the menu: it must not rejoin.
6. Redeploy the relay before the `invite` count works: the one on the VPS refuses an event it
   doesn't know. Add "SHARE is pressed for a room" to the privacy policy's list of counts
   (`server/relay/README.md` has the draft).
7. When the Play listing exists: its address in `storeUrl`, then finish a run as a guest in
   Chrome on Android and tap GET THE APP.

### 4.3 Clips: one batch, one a day

Everything is blocked by the name check, because the handles depend on it. Do that first (§6).

- **One Clip Lab session, forty clips.** The agent writes the shot files and cut lists across
  the 12 levels, the weapons and the nine actions. You record and schedule them.
- **One clip a day** on Shorts, TikTok and Reels, from the day the accounts exist through
  launch week. `store-media.md` already has the craft rules: moving in the first second, end
  on the dust, lead with Town and Port.
- **Send viewers to the browser build.** Until pre-registration is live a clip has nowhere to
  send anyone. "Play now, no install" is a better call than "coming in January".
- **Judge by the retention graph**: who is still watching at three seconds and at the end, not
  the view count. Keep the best fifth's format and make more of it. The winners become the
  store video and the ad creatives.
- Three hooks to add to the eight in `store-media.md` §2.4:
  1. **WHERE WOULD YOU HIT IT?** One second of an intact building with spots A, B and C
     marked, then the hit and the collapse. People argue in the comments.
  2. **SEND A LINK. YOUR FRIEND IS IN.** A phone and a browser side by side, ten seconds.
  3. **TODAY'S STREET: 82%. BEAT IT.** A daily series: the day's level at three times speed,
     the score, the link.

Be realistic: sources on short-video reach are weak and disagree. Some report falling TikTok
reach in 2025–26 and steadier Shorts; the success stories are single cases. Treat clips as
cheap lottery tickets that also fill the store page.

**Creators** (`marketing.md` §3.6): send the browser link, not a store link. The pitch is
"play today's street against your viewers", with the ranked board and its ghosts behind it.

### 4.4 Steam: skip February, aim at June

The game already runs on PC with gamepads, couch co-op and online co-op, and PC shows no ads.
Co-op demos did better than most in both 2026 Next Fests
([Cinevva on February 2026](https://app.cinevva.com/news/2026-03-05-steam-next-fest-february-2026),
[GameDiscoverCo on June 2026](https://newsletter.gamediscover.co/p/who-won-june-2026s-steam-next-fest);
secondary analyses).

- **Skip February 2027** (22 Feb – 1 Mar; registration closes 10 Jan 2027; a game may enter one
  Next Fest ever: [Steamworks](https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest/feb_2027)).
  It lands on the Play launch and the game has no audience yet. One analysis says entrants with
  under 1,000 wishlists rarely reach the upper tiers.
- **Steam page live by the end of January** ($100 per app:
  [Steamworks](https://partner.steamgames.com/doc/gettingstarted/appfee)), so clips, the web
  build and the phone game feed wishlists for four months.
- **June 2027 Next Fest** (14–21 June) with a demo, if the page has 2,000 wishlists by its
  registration deadline (*estimate*). Otherwise wait for the autumn one.
- Yours to decide by then: free or a small price. PC has no other income.

### 4.5 Freeze, with a short list

Until 11 Jan, new work is only: what the closed test finds, the web build (4.1), the invite
link (4.2) and the short list in §5.1. Theme 4, more looks, hats, ranked K6, TV and iOS wait.

## 5. Features

More features will not make the hit. A few of them *are* the distribution or the first minute,
and those go in before launch. The rest wait for a number. Sizes as in the roadmap (S: a few
sessions, M: 1–3 weeks part-time). The agent's hours are cheap and yours are not, so each row
says what it needs from you.

### 5.1 Before launch

| | Feature | Why | Built from | Size, and your part |
|---|---|---|---|---|
| F0 | **Counters, not an SDK.** The game posts anonymous counts to your own server: first start, tutorial done, level finished (which one, stars, players), share tapped, and once a day the session's "days since install" (0, 1, 7, 30), which the device works out itself. No id is sent. **Built 17 Nov, and off until you turn it on:** the game counts `first_start`, `day` (only on days 0, 1, 7 and 30 after the first start, by the device's calendar; a session on any other day sends nothing), `tutorial_done`, `level_done` (the level's id, or `daily`, `ranked`, `tutorial`; the stars; one or two players; online as host or guest) `share` (once per run) and `invite` (once per room whose link the host shared, §4.2; the relay on the VPS takes it after its next deploy), each with what it runs on (web, android, pc). The relay takes them at `/count`, adds one to a counter per day and keeps nothing else, and counts rooms and joins (from a browser or the app) by itself; `/admin/counts` reads them. Left out: failed runs, how long a session lasts (so OVERTIME's "under 5 minutes" in 5.2 still needs the portal's dashboard), the game's version, visits from shared links, and which guests install (§4.2): that last one would mean following a person. | Every "build it when" in 5.2 and every row of §7 needs them. Day-1 and day-7 retention fall out of the day counts without knowing who anyone is. The relay can count rooms, joins and the joiner's platform by itself. | `Counters` (what is counted and when), `CounterSender`, `SaveData.counters`; `server/relay`'s `Counters.cs` and SQLite. Its README has the event list, the limits and how to read each number in §7 from the counts. | S. Built; the model and the relay's endpoint have tests, and no build has sent a count yet. Nothing is sent while `GameConfig.countersUrl` is empty, and never from the editor. Your part, in this order: (1) read the draft of the privacy policy's wording in `server/relay/README.md` (under Privacy), settle the question it raises about the first-start day kept on the device, and edit `privacy.html` yourself; (2) redeploy the relay (`deploy.sh`; it now makes `data/` for the database and writes its path and an admin token into `relay.env`), then post one event with curl and read it back from `/admin/counts`; (3) set `countersUrl` to `https://<relay>/count` in `Resources/GameConfig`, and if `Relay__AllowedOrigins` is set, check the web build's origins are in it; (4) in the Play Console's Data safety form, check that App interactions (declared for AdMob) also lists Analytics as a purpose; (5) make a browser build and a phone build, finish a level in each, and see both counts arrive (in the browser: no CORS error in the console). |
| F1 | **Painted weak point.** The first buildings a new player meets (the Tutorial's COLLAPSE stretch, Town 1 and 2) carry a spray-painted X on the block that brings down the most. Never in dailies or ranked. **Built 17 Nov, bar the Tutorial's X:** a level asks for it in its file (`"weakMarks": n`: its first n buildings, props not counted). Town 1 paints its first 4 of 6 (the shop and the terrace of three; the two after the hill are bare), Town 2 its first 3 of 8 (the shop and the two on the islands). Not every building, so that each level shows where to hit and then leaves the player to find it: paint on all of them teaches "hit the X", and Town 3 has none. A daily, a ranked run and a Clip Lab shot never paint; online both devices paint the same blocks. | The pitch is "find the weak point", and today nothing shows a new player that one hit can drop a building. Measure the share of first runs with a 12-block collapse, and tutorial completion. | `BuildingValidator.WeakPoint` (it was the Clip Lab's) through `LevelLayout.WeakMark`, painted by `WeakPointMark`; the X is drawn by `tools/artpipe/marks.py`. The same component by hand in the Tutorial; the Building Lab's red tint is the same idea | S. Built and model-tested; nobody has seen it in the game yet. Your part: (1) judge the look: `content-work/marks.png` shows the X on blocks of all three themes, `content-work/levels/town-01.png` and `town-02.png` where it lands, and then Town 1 on a phone (hazard yellow in a dark outline; on the Town shop it sits on a cross-braced support, itself an X); (2) in the Tutorial scene add `WeakPointMark` to `Environment ▸ Tutorial Big House ▸ WestWall ▸ WoodBigPlatform`, the foot of the wall the player reaches first: by the model its loss brings down 11 of the house's 28 cells, the east wall's foot 12; (3) the numbers: tutorial completion is `tutorial_done` over `first_start` (F0), but nothing counts a collapse yet, so the 12-block share needs one more count, and by the model the X of each of Town 1's three terrace shacks brings down 11 blocks, one short (the shop before them brings down 18). |
| F2 | **Rubble card.** SHARE on the results screen copies a few lines of text: the score, and one mark per building on the street (below). Everyone gets the same daily, so strips compare, as Wordle's grids do. **Built 17 Nov:** after a finished run of a generated level (campaign, daily, ranked) SHARE sits between SHOP and MENU, opens Android's share sheet with the card (`Platform.Share`; in a browser and on PC it copies the card to the clipboard and then reads COPIED). Street props and gates get no mark: every finished run broke the gates. No button after a failed run, in the Tutorial (hand-made buildings, no street to compare) or on the joining device online (the host counts the buildings and has the button). Left out: the streak (5.2), which does not exist yet; and the `play it:` line until `GameConfig.playUrl` holds the web build's address. | It is the shareable result the scorecard in §3 misses. Measure share taps per finished run and visits from shared links (neither is counted before F0). | `RubbleCard` (the text and the marks' thresholds), a count of cells gone per building in `RunController` (a building's own `DestroyedFraction` is gone once it streams out behind the camera), `Platform.Copy` | S. Built; only the text has tests, nobody has pressed the button. You try: finish a campaign level that has a NEXT (five buttons in the row, narrower than before: do they sit well on the plate at 16:9 and on a phone, and is SHARE easy to hit with a thumb); finish a daily (four buttons); paste the card into the apps you would share it in and check the marks against what you left standing; walk the row with a gamepad; press SHARE in a browser build; host an online run. Then put the web build's address in `playUrl`. |
| F3 | **Two players on one keyboard**, for browsers and PC. **Built 17 Nov:** in the lobby Space joins the WASD half and the up arrow the arrows half. WASD half: A/D, Space or W to jump, F hit, R TNT, G action, Q/E swap. Arrows half: the arrows, up to jump, K hit, O TNT, L action, U/I swap. One player on the keyboard plays as before. | Poki calls its two-player shelf "local multiplayer games built for two people on one screen"; CrazyGames says its shelf holds over 200 games, played online or locally. The first screen of both is versus and sports games with a few co-op ones, and no demolition game ([Poki](https://poki.com/en/two-player), [CrazyGames](https://www.crazygames.com/t/2-player), 12 Nov 2026). Online co-op needs a second device and a friend who is awake; a school laptop has one keyboard. | `KeyLayout` (the bindings), `DeviceInputSource`, `Session`, `JoinScreen`, `InputGlyphs` | S/M. Built; only the model has tests (each half's keys, the glyphs), nobody has played it. You try it with a second person at one keyboard: join with Space and the up arrow, both halves through a level, the tutorial signs and the pause card, and whether the laptop's keyboard takes six keys at once. And in a browser build: the arrows and Space must reach the game and not scroll the page. |
| F4 | **Cold open.** A brand-new profile skips the title and the lobby: the crew runs in, one hit on a painted X drops a building, the title slams over the dust, and the Tutorial carries on. **Built 17 Nov, and nobody has seen it run:** a profile with no run banked and no finished Tutorial starts in the Tutorial, at the Big House with the painted X, which sits in the middle of the screen. The player runs in from beside the house before it and stops at the X (under a second). The bottom of the HUD asks `PRESS [Z] TO SMASH THE WEAK POINT`, with `(X)` on a gamepad and `HIT` on a phone. Any key, click, button or tap is the hit, and after 4 s without one the crew hits by itself. The first punch that lands drops the painted block whatever health it has left, and what hangs from it follows (11 of the house's 28 cells by the model). Half a second of dust, then DEMOLITION MEN slams into the middle, holds and fades (1.6 s, and a press skips it), then GO! and the controls. So the first collapse comes about a second after the Tutorial appears plus the player's press (five seconds if nobody presses), and the controls two to three seconds after it. Until GO the player can't be hurt and the Tutorial's signs are hidden; the collapse scores and buzzes as in play. **It starts at the Tutorial's third stretch.** The X is on the third building, so the step and the spike pit (MOVE & JUMP) are out of play behind the camera, and the first house and the axe (SMASH) are behind the player, where they can still be had. After GO the COLLAPSE sign is over the player, SMASH stands behind them, and the bottom line names the move and jump controls until they have gone 3 units. Opening on a generated street instead would keep the Tutorial whole, but costs a second scene load between the collapse and the controls and a level file of its own, and the hand-made X was already there. **The device:** the touchscreen on a phone, as in the lobby. Elsewhere the keyboard to begin with, and through that first run whichever of keyboard, mouse or gamepad is pressed plays, and is the player's from then on. **Afterwards:** nothing is saved for the opening, so a player who quits before the finish gets it again at the next start. In the same session MENU goes to the level select as before, RESTART and RETRY play the Tutorial from its start, and a failed run counts as a run, so the next start is the title. A second player joins as always: MENU, BACK from the level select, the lobby. An invite link still goes to its room first. Counters and ads are untouched. `coldOpen` off in `Resources/GameConfig` gives everyone the old start. | Seconds from start to first collapse (roadmap R2 wants under a minute), and the portal's "gameplay conversion". CrazyGames' Full Launch asks that a game lands in gameplay. It is also the trailer's opening shot. | `ColdOpen` (who gets it; the beats and the time limit of each), `ColdOpenDirector`, `TitleView.PlayOver`, `RunController.Held`, the Tutorial's `WeakPointMark`, `MainMenu` | S/M. Built; who gets it and the timeline have tests, and nothing has been played, in the editor or on a device. Your part. **Editor first:** Test Runner ▸ PlayMode ▸ `ColdOpenRunTests` (never run; ten seconds), then watch it on a fresh profile: does the player stop at the X and not slide on into the pit, does one punch drop it, where does the title land. **Phone** (a release build, fresh install): the seconds from the icon to the collapse; in the EEA the consent form comes up during the opening, so see whether the game waits under it and what is on screen when it closes; whether the bottom line reads above the touch controls (on a 4:3 tablet it may run under HIT); tap HIT, tap anywhere, tap nothing; Android back during the opening (the pause menu, and RESUME goes on); lock the phone in the middle of it. **Browser** (a private window, in itch's frame, and a phone's browser): keys reach the game only after a click on the frame, and that click is the hit; with nothing pressed the crew hits after 4 s. **PC with a gamepad:** the line says `[Z]` until the pad is pressed; any pad button is the hit and the signs switch to pad buttons; then swap to the keyboard and back. **Everywhere:** quit from the pause menu halfway and start again (the opening again); finish the Tutorial and start again (title and lobby); die in the fire pit and RETRY (the whole Tutorial). **To judge:** whether 4 s is the right patience, and whether the move-and-jump line is enough for the two pits right after the X, since a new player now meets them before any jump lesson. |
| F5 | **Rumble and haptics.** A tick per hit and a buzz that grows with the collapse, on gamepads and phones, with a setting. **Built 17 Nov, and no motor has turned yet:** each player's own gamepad, or the phone for the player on its touchscreen, gets a tick when their hit lands (harder with the axe or the ball), a buzz for a collapse they set off or stand near (from one block up to full strength for half a second at 40 blocks; a bigger one gets no more), a thump for a blast nearby and a jolt for damage taken. Nothing adds up: the strongest plays and the hands rest after each, so punches stay ticks and a building that comes down in stages is felt in bursts. Settings ▸ VIBRATION turns it off (on by default; the row is there only where something can buzz). Every motor stops when the run ends, on pause, when the app loses focus and when the scene goes. Online each device buzzes for its own screen and nothing is sent; the Clip Lab and bots buzz nothing. Left out: gamepads in a browser and on Linux (the Input System drives no motors there, so the web build buzzes phones only, and Android's browsers at that: Safari has no vibration), iOS, any buzz in the menus. | Feel. No number will show it; it is cheap enough to do unmeasured. | `Haptics` (every strength and length, and `HapticChannel`: what plays when several ask), `RunHaptics`, `PhoneMotor`, `Plugins/WebGL/Vibrate.jslib`; it listens to `PlayerCombat.Landed` (new), `PlayerHealth.Hurt`, `DestructionEvents.Collapsed` and `Dynamite.Exploded`. `RunJuice` scales the shake the same way | S. Built; the model has tests, and the numbers are guesses nobody has felt. Your part. **Phone** (a release build): (1) first check the build's manifest has `android.permission.VIBRATE`; Unity should add it because the code keeps a call to `Handheld.Vibrate`. Without it nothing buzzes, the VIBRATION row disappears from the settings and logcat says `[PhoneMotor] no vibration`; (2) punch a wall: a light tick per punch that lands, five a second, and none for a swing at the air; a harder one with the axe or the ball; (3) knock out a weak point: clearly more than a tick, short for a few blocks, about half a second for a whole building and never longer; a building that crumbles on in stages comes in bursts, not as one hum; (4) TNT: a thump when it goes off near you, nothing from a third of a screen away; (5) debris on the head, barbed wire: a jolt you cannot miss; (6) pause in the middle of a collapse, and send the app to the background in the middle of one: still at once; (7) VIBRATION off: nothing at all; and look at the settings with ten rows (they sit closer than the nine did): are the − and + still easy to hit; (8) turn the phone's own vibration or touch feedback off and see whether the game still buzzes; I do not know which of Android's switches rules a game; (9) the web build in the phone's Chrome, also inside itch.io's frame: the same, coarser (a browser's motor is only on or off, so weaker means shorter). **Gamepad**, on Windows with an Xbox or PS4 pad, or a pad paired with the phone (not this laptop: no rumble on Linux, in the editor or a build): (1) the same list; ticks sit on the light motor alone, the heavy one joins for weapons, collapses and damage; (2) two players: each pad for its own player's hits; a partner's collapse weaker, and nothing from across the screen; with one player on the keyboard the other's pad still works; (3) the pad must be still after each of these, done in the middle of a rumble: pause, quit to the menu, retry, the finish line, alt-tab, VIBRATION off from the pause menu, stopping play mode. Then tune the constants in `Haptics`: too weak to feel is the likelier fault than too strong. |
| F6 | **Controller flag and Play Games on PC.** Set Android's gamepad support in Player Settings (it is at the default) so the store's controller filter finds the game, and enable the PC form factor in the Play Console. | Free shelves, and this game fits them better than most phone games: it already plays with keyboard, mouse and gamepad. Google asks only that a game is playable with mouse and keyboard to ship on PC ([Play Games on PC](https://developer.android.com/games/playgames/start)), and tells developers to add the controller flag "so that users can find your game when searching for controller-supported games" ([Level Up guidelines](https://developer.android.com/games/guidelines)). | Settings only | S, yours. Check the built manifest for `android.hardware.gamepad`, and whether ads serve on PC. |

The rubble card for a daily, with 💥 flattened (80% of the building down or more), 🏚️ half down
(40% or more) and 🏢 left standing. Those are the shares of the whole street that light its
third and first star. By the validator's count the best single hit flattens three in four of
the campaign's 92 buildings (68) and leaves most of the others half down (18):

```
DEMOLITION MEN · Daily 12 Nov
82% ★★★ · 1:42 · streak 5
💥💥🏚️💥🏢💥💥🏚️
play it: <link>
```

As built the second line has no streak, the stars read ★★☆ with their empty places, a 🌈
follows them for a finish within par, and the last line is missing until there is a link.

Together these are two to three weeks of your testing time. If the closed test turns up real
problems, F4 and F5 go first.

### 5.2 First updates (v1.1, v1.2)

| Feature | Build it when | What it is | Built from | Size |
|---|---|---|---|---|
| **Daily streak** | v1.1, always | Finishing the daily on days in a row pays rising cash up to day 7. The count goes on the rubble card. | `Daily`, `SaveData` | S |
| **Site awards** | v1.1, always | The results hand each player one award from the run: TOP WRECKER, HEADBUTT KING, MOST TNT WASTED, FLATTENED BY YOUR OWN BUILDING. Rivalry inside co-op, and a results screen worth a screenshot. | `RunTally` with a few more counts per player, `ResultsView` | S |
| **REMOVE ADS pays double** | v1.1, with the purchase | Buyers keep the 2x CASH button and it needs no ad. It answers the open question in `monetization.md`, and "double cash for good" sells better than "no ads" (*estimate*). | `Purchases`, `ResultsView` | S |
| **OVERTIME** (the endless mode) | Sessions average under 5 minutes, or the counters show players finishing level 12 | Streets back to back from fresh seeds, each a little faster. Health carries over, one life, and the score is streets cleared: one number to brag with. A rewarded ad buys one more life, the first new rewarded placement. | `LevelSpec.WithSeed` (its comment already says "endless reroll"), `scrollSpeed`, `RunState.TryResurrect` | S/M |
| **Night shift** | Level-12 finishes pass a tenth of tutorial finishes (*estimate*) | Every campaign level again after dark: a new seed, a faster scroll, tougher buildings, the theme's ambient turned down with lamps and headlamps lit. Twelve levels become twenty-four with no new art. Keep it out of clips: dark footage reads badly (`store-media.md` §4.1). | `LevelTheme.ambient`, the sky palettes, `SceneLighting`, `BotRunTests` | S/M |
| **TEAMWORK** | Two-player runs are a fifth of finished runs (*estimate*) | Two supports of one building knocked out by different players within two seconds: the collapse scores 1.5x and stamps TEAMWORK. Plus an archetype with two weak points far apart. Co-op then needs a plan ("three, two, one, hit"), and the co-op clip gets a story. | `RunScore` (whole-number maths, so the ranked server agrees), the instigator on every hit, archetype `weakPoints` | S/M |
| **Name your street** | One finished run in twenty ends in a share (*estimate*) | Type a word and play its street; the word or a link shares it. Viewers can throw words at a streamer. | `LevelSpec.WithSeed`, the NAME field, one more variant of the online level message beside campaign, daily and ranked | S |
| **Crew rank, achievements, cloud save** | v1.2, sooner if reviews mention lost progress | Blocks demolished over a lifetime give a rank (APPRENTICE to DEMOLITION KING) on the results and the boards. On Android the same events feed Play Games achievements (Google asks for ten, four of them within the first hour to qualify for its Quests), and Saved Games keeps the profile across phones. Today the profile is one local file. | `SaveData` (it counts cash and runs only), Play Games Services v2 | M: one more SDK and a Data safety change |
| **Demolition cam** | Any update | A collapse of 40 blocks or more gets half a second of slow motion and a push-in. Solo and one-screen co-op only at first: online and ranked share a clock. | `RunJuice`: its hit-stop is 0.07 s today, and its `BaseTimeScale` already serves the Clip Lab's slow motion | S |
| **Foreman on the radio** | With the trailer's re-cut | A crackling voice calls chains and collapses: "TIMBER!", "THAT'S COMING OUT OF YOUR PAY". The crew have signs for heads and can't talk; the radio can, and its filter hides a cheap microphone. | `ActionAudio`, the site radio's sound path | S/M. You record a dozen lines, or license them. |

### 5.3 Later, on a good number

- **Chaos street:** one street a week with two rule-benders (moon gravity, a glass town, endless
  TNT, double speed). Each is a parameter; none goes in ranked. A weekly reason to post.
- **Seasonal signs:** a sign for the crew's heads per event (King's Day on 27 April, Halloween),
  drawn by `heads.py`, with a themed chaos street.
- **Landmarks:** each theme's last level ends on one huge curated building (clock tower, silo
  yard, casino sign). Mostly JSON and seed-picking in the Building Lab.
- **Callouts online:** a POINT button chalks an X on the block you face for both players, with
  three shouts (WAIT, TNT, NICE). One small message.
- **Beat my ghost:** a shared link opens the daily with the sender's ghost. The ranked boards
  already animate ghosts from logs; it needs the ranked server live.
- **Build a street:** pick the pacing tokens as cards (cluster, tower, gate) and share the
  result. The traversal validator already guarantees it can be played.

### 5.4 Big bets, only on "scale" in §7

- **Four players**, on one screen and online. Players are found by the names `Player_1` and
  `Player_2` today, and the HUD, the camera and the netcode all assume two: L.
- **Overtime as a roguelite:** pick one of three perks between streets.
- **Machines:** a bulldozer or a crane to drive for one segment.

### 5.5 Not building

- Enemies and fighting. The game is the crew against buildings; that is the pitch and the clip.
- Energy, timers, loot boxes, power for money. The roadmap rules them out and reviews punish them.
- Chat and accounts. Moderation and privacy work with no player asking for them.
- **Google's Level Up programme, for now.** It is voluntary and pays mainly in lower service
  fees, with promotion on top; it asks for Play Games sign-in, Sidekick, achievements, game
  stats, rewards, cloud save, large-screen and PC support, controller and keyboard play, 60 fps
  and Vulkan ([programme](https://google.play/business/levelup/),
  [guidelines](https://developer.android.com/games/guidelines), Nov 2026). Lower fees are worth
  nothing to a game with one small purchase. Take the cheap parts only: F6 now, achievements
  and cloud save in v1.2.

## 6. Calendar

| When | You | Agent |
|---|---|---|
| **12–16 Nov** | **Name check** (below). Play account. **Start the closed test with whatever build runs**: the 14 days are the slowest clock. Open the accounts under one handle. | Declarations for the closed test (`store-media.md` §6). |
| 17–30 Nov | Record the clip batch. Post daily. Post the tester call. | WebGL build on the VPS and itch.io. The invite link and both SHARE buttons. F0–F3. Forty shot files and cut lists. |
| 1–14 Dec | Submit to CrazyGames. Apply for production access (day 14 of the test). Pre-registration. | First reading of the web dashboard and the counters; fix the worst step of the first five minutes. F4–F6 if the test is quiet. |
| 15 Dec – 10 Jan | Trailer, press kit, creator list. Steamworks sign-up. | A second and third round on the web numbers. |
| Week of 11 Jan | Play launch, staged. Creator e-mails with the browser link. | Fixes within days. |
| 18–31 Jan | Steam page live. Decide on Poki with the numbers. | CrazyGames SDK if they select the game. |
| Feb – June | An update every 2–4 weeks; wishlists; the June demo. | v1.1, v1.2 as in the roadmap. |

**The closed test.** New personal accounts need 12 testers opted in for 14 days in a row, and
the review "usually takes seven days or less"
([Google Play help](https://support.google.com/googleplay/android-developer/answer/14151465),
Nov 2026). A start on 16 Nov gives access around 7 Dec. A start after 30 Nov leaves no time
for pre-registration before 11 Jan.

**The name.** "Demolition Men" is one letter from *Demolition Man*, a Warner Bros. film with
licensed video games. A name like that draws a complaint at the moment a game gets big, which
is the moment a rename costs most. Play's search shows no app with either exact name in its
first ten results (12 Nov 2026). The trademark registers could not be reached from here:
search TMview and USPTO for DEMOLITION MAN in classes 9 and 41 yourself. If a live mark covers
games, rename now, while it costs nothing.

## 7. Numbers that decide

Benchmarks are for all mobile games, from GameAnalytics' 2026 report (16,262 games): day-1
retention just above 30% puts a game in the top quarter and 40% in the top tenth; day 7 has a
median just under 4%, 6–7% for the top quarter and 11–12% for the top tenth; the top quarter's
sessions last about 5 minutes
([summary by GameDev Reports](https://gamedevreports.substack.com/p/gameanalytics-mobile-and-pc-game),
June 2026). The report has no split by genre, and web players behave differently from app
players, so the web rows are a proxy.

| Where | When | Stop or fix | Carry on | Scale |
|---|---|---|---|---|
| Web (CrazyGames dashboard) | After 1,000 players | Average playtime under 5 min, or most players gone before the first collapse: fix the opening before Play | Playtime of 5 min or more, about two levels | Selected for a Full Launch |
| Play | 2 weeks after launch, 500+ installs | Day 1 under 30% after two rounds of fixes: no ad money, no mobile push; put the effort into web and Steam | Day 1 of 30%, day 7 of 6% | Day 1 of 40%, day 7 of 11%: spend the held budget, write to publishers |
| Listing | Once Play shows the peer benchmark | Below the peer median: change the icon and the first screenshot | Above the median | Above the 75th percentile |
| Invite link | 4 weeks after launch | Under 1 guest run per 20 online hosts (*estimate*): move the button, shorten the path | — | Guests install: put the link in every clip |
| Clips | After 30 posts | A format in the bottom half twice: drop it | Top fifth: make more | A clip that breaks out: all effort there that week |

Two corrections to `marketing.md` §6:
- Its listing target of 25–30% is not a safe yardstick; vendors' figures for games range from
  18% to 35% and count different things. Use the peer median, quartiles included, that the
  Play Console shows for your category
  ([Play Console help](https://support.google.com/googleplay/android-developer/answer/9859173)).
- Check what the Play Console's retention counts. If it counts installs kept instead of days
  played, take day 1 and day 7 from the counters (§5.1, F0).

## 8. Money

| Item | Cost | When |
|---|---|---|
| Play account | $25 (roadmap; not re-checked) | Now |
| Steam Direct | $100 | January |
| Held back | The rest | Only on the Play row's "scale" in §7 |

The launch-week Google Ads burst goes. A few hundred euros can't measure what an install
costs, and money sent to a listing that hasn't proven itself is lost. On income: at three ads
per daily player and US Android prices of $10–16 per thousand views (vendors disagree:
[MAF](https://maf.ad/en/blog/rewarded-ads-stats/),
[MonetizeMore](https://www.monetizemore.com/blog/how-much-ad-revenue-can-apps-generate/)), a
US player earns 3–5 cents a day, and most players are worth less (*estimate*). Ads pay only
at scale, which is why this plan chases reach first.

## 9. Not verified

- The trademark status of DEMOLITION MAN (§6).
- CrazyGames' revenue share and its view on games published elsewhere; guides disagree.
- Reviews of comparable Play games (the pages didn't load), so no list of their complaints.
- The WebGL build's size and its speed in a phone browser: no build is recorded in the repo.
- How many players the portals' two-player shelves bring. Only that the shelves exist.
- What Level Up's promotion is worth to a small game: Google's page shows large ones only.
- Whether Play Games on PC lists a phone game by itself or needs the opt-in: Google's pages
  differ.
- The thresholds marked *estimate* in §5.2: set them again once the counters have a baseline.
- The counters (§5.1, F0) end to end: no build has sent one. Untested: the post from a browser
  build on another site, from a phone, and the redeployed relay with its database folder.
- The painted X (§5.1, F1) in the game: its code compiles and the model is tested, but it has
  not been run. Unseen: the X on a block in Town 1, that it leans with the block and goes with
  it, and the same on a joining device.
- Whether Steam's June 2027 registration deadline is out yet.
- The project's state after 30 Oct.
