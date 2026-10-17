# Monetization: free with ads

**Status:** code seam in place, no ad SDK installed yet (2026-10-17).

The game ships free. Phones and tablets show ads; PC and TV builds show none. The game
code never talks to an ad SDK directly: everything goes through one interface, so the
network can be picked (or swapped) without touching gameplay.

## What is built

| Piece | Where | What it does |
|---|---|---|
| `AdPolicy` (model) | `Scripts/Demolition/Ads/Model/` | When an interstitial is allowed: not in a new profile's first 4 runs, at most every 3 runs, at least 3 minutes apart, never once ads are removed. Unit-tested. |
| `IAdService` | `Ads/Controller/IAdService.cs` | What the game needs from a network: `IsInterstitialReady`, `IsRewardedReady`, `ShowInterstitial(closed)`, `ShowRewarded(finished(earned))`. |
| `NoAdService` | same file | Default on PC/TV and release phone builds until an SDK adapter is installed. |
| `SimulatedAdService` | `Ads/View/` | Development phone builds (and the editor with `AdManager.SimulateInEditor = true`): a full-screen card that closes after 2.5 s, so the flow can be tested. |
| `AdManager` | `Ads/Controller/` | The single door: `BetweenRuns(then)` shows an interstitial if due, `CanOfferReward` / `ShowRewarded` for the rewarded offer. |

Placements:
- **Interstitial**: when the player leaves the results screen (Next, Retry or Menu), if
  `AdPolicy` says it is due. Never during a run, never on the first launch.
- **Rewarded**: the results screen offers **WATCH AD: 2x CASH** when the run earned cash
  and a rewarded ad is loaded. Watching it banks the run's cash a second time. Always
  opt-in.
- **Remove ads**: `SaveData.adsRemoved` turns all ads off. Wire it to a one-off in-app
  purchase (Unity IAP) when there is one; nothing sets it today.

## Wiring a real network (recommended: Unity LevelPlay)

1. Install the package in the editor (Package Manager): `com.unity.services.levelplay`
   (LevelPlay mediation; it can serve Unity Ads, AdMob and others). Configure the app keys
   and ad unit ids in the LevelPlay dashboard.
2. Add an adapter next to the others, e.g. `Ads/Controller/LevelPlayAdService.cs`,
   implementing `IAdService` on top of the SDK's interstitial and rewarded ad objects
   (load on start, reload after each show, report `IsReady` from the SDK).
   Add `Unity.LevelPlay` (or the SDK's assembly) to `Demolition.asmdef` references.
3. Install it at startup on phones only, e.g. in `Meta/Controller/AppBootstrap.Init`:
   ```csharp
   #if UNITY_ANDROID || UNITY_IOS
   if (!Debug.isDebugBuild) Ads.AdManager.Service = new Ads.LevelPlayAdService(appKey);
   #endif
   ```
4. Test with the network's test ads before release.

AdMob directly (Google Mobile Ads Unity plugin) works the same way: one adapter class.

## Legal and store checklist (needed before release)

- **Consent (EU/UK, GDPR):** show a consent form before requesting personalised ads —
  Google's UMP SDK or the LevelPlay consent API. Ads must not load until consent is
  resolved.
- **iOS App Tracking Transparency:** request ATT permission before personalised ads; add
  `NSUserTrackingUsageDescription` and the network's SKAdNetwork ids to Info.plist.
- **Privacy policy URL** on both store pages, and the Google Play *Data safety* /
  App Store *privacy nutrition label* forms (ads SDKs collect device ids).
- **Audience:** the game is cartoon violence against buildings; declare it as not
  primarily for children unless you want the stricter child-directed ad rules (COPPA /
  Families policy), which also limit ad networks.
- **Ratings:** IARC questionnaire on Google Play, age rating on the App Store.

## Ideas for later

- Rewarded **continue** (rejoin once more in solo after going out).
- Rewarded **daily bonus** (extra cash on the daily challenge).
- Cosmetic skins (palette swaps of the demolition man) as IAP or cash unlocks.
