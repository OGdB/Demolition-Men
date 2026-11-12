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
- **The browser build reads the link** (`Application.absoluteURL`), skips the lobby and joins
  the room. The friend plays within seconds, with no store and no account.
- **After the run**, a browser guest on Android sees the Play badge (your own copy only).
- **SHARE on the results screen**: the rubble card (§5.1, F2), a few lines of text with a
  link that opens the same street in the browser.
- **Mind the version check.** A phone on an older build must still find a browser build that
  matches it. Put the protocol version in the link's path and keep the last two builds on the
  VPS. Redeploying the web build joins the release checklist.

Measure: invites sent per player, guests who finish a run per invite, guests who install.
The relay and the counters (§5.1, F0) give all three.

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
| F0 | **Counters, not an SDK.** The game posts anonymous counts to your own server: first start, tutorial done, level finished (which one, stars, players), share tapped, and once a day the session's "days since install" (0, 1, 7, 30), which the device works out itself. No id is sent. | Every "build it when" in 5.2 and every row of §7 needs them. Day-1 and day-7 retention fall out of the day counts without knowing who anyone is. The relay can count rooms, joins and the joiner's platform by itself. | The relay's ASP.NET app and SQLite; the privacy policy already says the server sees IP addresses | S. You redeploy the relay and check the Data safety answers (app interactions are declared for AdMob already). |
| F1 | **Painted weak point.** The first buildings a new player meets (the Tutorial's COLLAPSE stretch, Town 1 and 2) carry a spray-painted X on the block that brings down the most. Never in dailies or ranked. | The pitch is "find the weak point", and today nothing shows a new player that one hit can drop a building. Measure the share of first runs with a 12-block collapse, and tutorial completion. | `ShotPlanning.WeakPoint` for generated buildings, by hand in the Tutorial; the Building Lab's red tint is the same idea | S. You judge the look. |
| F2 | **Rubble card.** SHARE on the results screen copies a few lines of text: the score, and one mark per building on the street (below). Everyone gets the same daily, so strips compare, as Wordle's grids do. | It is the shareable result the scorecard in §3 misses. Measure share taps per finished run and visits from shared links. | `DestructibleBuilding.DestroyedFraction`, `Platform.Copy`, the share sheet from 4.2 | S |
| F3 | **Two players on one keyboard**, for browsers and PC: for example P1 on WASD with F, G and R, P2 on the arrows with K, L and O. | Poki calls its two-player shelf "local multiplayer games built for two people on one screen"; CrazyGames says its shelf holds over 200 games, played online or locally. The first screen of both is versus and sports games with a few co-op ones, and no demolition game ([Poki](https://poki.com/en/two-player), [CrazyGames](https://www.crazygames.com/t/2-player), 12 Nov 2026). Online co-op needs a second device and a friend who is awake; a school laptop has one keyboard. | `DeviceInputSource` (today WASD and the arrows both move the same player), `JoinScreen`, `InputGlyphs` | S/M. You try it with a second person. |
| F4 | **Cold open.** A brand-new profile skips the title and the lobby: the crew runs in, one hit on a painted X drops a building, the title slams over the dust, and the Tutorial carries on. | Seconds from start to first collapse (roadmap R2 wants under a minute), and the portal's "gameplay conversion". CrazyGames' Full Launch asks that a game lands in gameplay. It is also the trailer's opening shot. | `TitleView`, `RunIn`, the new-profile branch in `JoinScreen.Continue` | S/M. It changes the first screen, so test it on every device. |
| F5 | **Rumble and haptics.** A tick per hit and a buzz that grows with the collapse, on gamepads and phones, with a setting. Neither exists today. | Feel. No number will show it; it is cheap enough to do unmeasured. | `RunJuice` already scales shake by collapse size | S. You feel it on the phone. |
| F6 | **Controller flag and Play Games on PC.** Set Android's gamepad support in Player Settings (it is at the default) so the store's controller filter finds the game, and enable the PC form factor in the Play Console. | Free shelves, and this game fits them better than most phone games: it already plays with keyboard, mouse and gamepad. Google asks only that a game is playable with mouse and keyboard to ship on PC ([Play Games on PC](https://developer.android.com/games/playgames/start)), and tells developers to add the controller flag "so that users can find your game when searching for controller-supported games" ([Level Up guidelines](https://developer.android.com/games/guidelines)). | Settings only | S, yours. Check the built manifest for `android.hardware.gamepad`, and whether ads serve on PC. |

The rubble card for a daily, with 💥 flattened, 🏚️ half down and 🏢 left standing:

```
DEMOLITION MEN · Daily 12 Nov
82% ★★★ · 1:42 · streak 5
💥💥🏚️💥🏢💥💥🏚️
play it: <link>
```

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
- Whether Steam's June 2027 registration deadline is out yet.
- The project's state after 30 Oct.
