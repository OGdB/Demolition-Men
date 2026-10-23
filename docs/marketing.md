# Marketing: reaching players on a small budget

**Status:** plan, 2026-10-20. Budget €100–500 for launch and the first months (roadmap D21).
Launch: Google Play, week of 11 Jan 2027 (roadmap §4 R1–R5).

The aim is players who enjoy the game, not a return on ad spend. The plan leans on what the
game already does well on camera, whole buildings coming down, and spends money only where a
free channel has already shown what works.

## 1. Who it's for

- People who love watching things fall: fans of physics and destruction games and of
  "satisfying" clips.
- Pairs who play together: friends, couples, a parent and a teenager. Online co-op between
  two phones, or a phone and a browser, is a hook few mobile games have.
- Retro pixel-art fans.
- Short-session players: a level takes 2–4 minutes, and the daily street is new every day.

Pitch line for the store, posts and trailer: **"Find the weak point. Bring the whole building
down, with a friend."**

## 2. What to make (€0, in-house)

| Asset | How |
|---|---|
| Clips (5–15 s) | One collapse per clip, already moving in the first second. A **clip mode** (hide the HUD, 9:16 vertical framing, slow motion; roadmap R2) records them by script from the editor; shot list and plan in `store-media.md`. The phone, recorded with scrcpy, for the touch controls and online co-op. |
| Trailer (30–60 s) | The best clips cut together: a weak-point collapse, a TNT chain, online co-op, the three themes, the store badge at the end. |
| Store graphics | Icon and feature graphic from the art pipeline; screenshots with one caption each, one feature per screenshot. |
| Press kit | One page (on the VPS or itch.io): a short description, the facts (platform, price, release date, co-op), the trailer, 5 screenshots, 3 GIFs, the logo, a contact address. |

## 3. Free channels

Weekly rhythm from now to launch (about 12 weeks): 2–3 clips, one Screenshot Saturday post,
one community post.

1. **Short video:** YouTube Shorts, TikTok and Instagram Reels, the same clip on all three.
   The collapse first, a text hook, no logo intro. Pin the pre-registration or store link.
2. **Reddit**, at most one post per community per month. Read each community's
   self-promotion rules first.
   - r/IndieGaming, r/indiegames;
   - r/playmygame, with the itch.io browser link;
   - r/AndroidGaming (dev posts are usually limited to a weekly thread or a flair);
   - r/Unity2D, r/PixelArt (art only), the Screenshot Saturday thread on r/gamedev.

   Post a GIF of the best collapse and reply to every comment.
3. **Bluesky, X, Mastodon:** #screenshotsaturday, #indiedev, #pixelart.
4. **itch.io:** the WebGL build (multiplayer O3) as the free browser version, plus a devlog. A
   browser player can join a phone host, so every itch visitor is a possible co-op partner
   for someone with the app.
5. **Discord:** a small server for the closed testers, then for players: builds, patch notes,
   daily-street scores (posted by hand until there is a leaderboard).
6. **Press and creators:** a list of ~30 Android and mobile game sites and small YouTubers or
   streamers (10k–100k subscribers, mobile or indie). One personal e-mail each, a week before
   launch, with the press kit and the store link.
7. **The Play Store itself:**
   - pre-registration, and a sharp listing;
   - replies to reviews;
   - store listing experiments (A/B tests of the icon and screenshots) once there is
     traffic;
   - the listing text translated for the top languages.

**The tester call** (roadmap R3) is the first marketing post, in mid-November: "looking for
20 Android testers for a co-op demolition game". Post it on Reddit, the Discord and to
friends. Testers who enjoy it become the first community and the first reviews.

**Never** buy installs or reviews, reward players for ratings, or use tester services that
fake activity. All of these break Play policy.

## 4. Paid (€100–500)

| Item | € | When |
|---|---|---|
| Play developer account | ~25 ($25, once) | R1 |
| Music or sound licences, only where CC0 falls short | 0–75 | R2 |
| Google Ads App campaign, launch burst | 100–250 | launch week, 7–10 days |
| A second burst, or a Reddit ad test with the best clip | 0–150 | with v1.1 or v1.2 |

Rules for the paid part:
- **Spend only once the listing converts.** Wait until 25–30% or more of listing visitors
  install (Play Console ▸ Store performance). Ads to a weak listing burn the money.
- **Use proven creatives.** The clips that did best for free become the ads.
- **Look for a new-advertiser credit** in Google Ads before spending. Many countries get one;
  the terms vary.
- **Expect seeding, not scale.** A few hundred euros buys a few hundred extra installs, which
  seed reviews and rankings and show which clip converts.
- **Cut losers early.** Stop a campaign after 3–4 days if its installs cost more than you
  accept.

## 5. Calendar

| Weeks | Marketing | Release (roadmap) |
|---|---|---|
| 20 Oct – 15 Nov | Accounts (YouTube, TikTok, Instagram, Bluesky, itch.io, Discord); the first clips; the clip mode | R1, start of R2 |
| 16 Nov – 13 Dec | The tester call; weekly clips | R3 closed test, R2 |
| 14 Dec – 10 Jan | Pre-registration live; the trailer; the press kit; the outreach list | R4 |
| Week of 11 Jan | Launch posts everywhere, press e-mails, the paid burst | R5 |
| Feb – Mar | Clips of each update's new content; a second paid test if the first one worked | v1.1, v1.2 |

## 6. Measuring

- **Play Console:** store listing visitors, conversion, installs by source, retained
  installers, ratings, Android vitals.
- **Tagged store links:** each channel gets its own link (`&referrer=utm_source%3D...`), so
  the Play Console shows which one brings installs.
- **Video analytics:** which clips get watched to the end.
- **Targets for the first release:** listing conversion ≥ 25%, day-1 retention ≥ 30%, rating
  ≥ 4.3.
- **No analytics SDK at launch.** The Play Console covers these numbers, and the Data safety
  form stays short.
