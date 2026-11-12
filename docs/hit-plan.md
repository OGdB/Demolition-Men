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
5. **A feature freeze** until the launch. Since 27 Oct the v1.2 work (job sheets, looks,
   ranked) was built, while the repo shows no accounts, no posts and an open name check.

Nobody can plan a hit. This plan buys more chances: four channels where the platform supplies
the audience, each with a number that says carry on or stop (§6).

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

Later, and only if the dashboard shows desktop players leaving at the join screen: two players
on one keyboard (WASD and the arrow keys). Portals have a big "2 player" shelf, but its size
is not verified.

### 4.2 The invite link, and a result to share (move from v1.1 into v1.0)

This is the one feature worth adding before launch. Everything else waits.

- **SHARE in `OnlineView`**, beside COPY: it sends `https://play.<domain>/?r=ABCDEF` through
  the phone's share sheet (the clipboard elsewhere; `Platform.Copy` exists).
- **The browser build reads the link** (`Application.absoluteURL`), skips the lobby and joins
  the room. The friend plays within seconds, with no store and no account.
- **After the run**, a browser guest on Android sees the Play badge (your own copy only).
- **SHARE on the results screen**: one line of text, for example
  `DEMOLITION MEN · Daily 12 Nov · 82% ★★★ · 1:42 · beat me: <link>`. The link opens the daily
  in the browser.
- **Mind the version check.** A phone on an older build must still find a browser build that
  matches it. Put the protocol version in the link's path and keep the last two builds on the
  VPS. Redeploying the web build joins the release checklist.

Measure: invites sent per player, guests who finish a run per invite, guests who install.

### 4.3 Clips: one batch, one a day

Everything is blocked by the name check, because the handles depend on it. Do that first (§5).

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

### 4.5 Freeze

Until 11 Jan, new work is only: what the closed test finds, the invite link (4.2), the web
build (4.1). Theme 4, more looks, hats, ranked K6, TV and iOS wait.

## 5. Calendar

| When | You | Agent |
|---|---|---|
| **12–16 Nov** | **Name check** (below). Play account. **Start the closed test with whatever build runs**: the 14 days are the slowest clock. Open the accounts under one handle. | Declarations for the closed test (`store-media.md` §6). |
| 17–30 Nov | Record the clip batch. Post daily. Post the tester call. | WebGL build on the VPS and itch.io. The invite link and both SHARE buttons. Forty shot files and cut lists. |
| 1–14 Dec | Submit to CrazyGames. Apply for production access (day 14 of the test). Pre-registration. | First reading of the web dashboard; fix the worst step of the first five minutes. |
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

## 6. Numbers that decide

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
  played, the web dashboard is the only real retention figure until the game logs its own.

## 7. Money

| Item | Cost | When |
|---|---|---|
| Play account | $25 (roadmap; not re-checked) | Now |
| Steam Direct | $100 | January |
| Held back | The rest | Only on the Play row's "scale" in §6 |

The launch-week Google Ads burst goes. A few hundred euros can't measure what an install
costs, and money sent to a listing that hasn't proven itself is lost. On income: at three ads
per daily player and US Android prices of $10–16 per thousand views (vendors disagree:
[MAF](https://maf.ad/en/blog/rewarded-ads-stats/),
[MonetizeMore](https://www.monetizemore.com/blog/how-much-ad-revenue-can-apps-generate/)), a
US player earns 3–5 cents a day, and most players are worth less (*estimate*). Ads pay only
at scale, which is why this plan chases reach first.

## 8. Not verified

- The trademark status of DEMOLITION MAN (§5).
- CrazyGames' revenue share and its view on games published elsewhere; guides disagree.
- Reviews of comparable Play games (the pages didn't load), so no list of their complaints.
- The WebGL build's size and its speed in a phone browser: no build is recorded in the repo.
- How big the portals' "2 player" audience is.
- Whether Steam's June 2027 registration deadline is out yet.
- The project's state after 30 Oct.
