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

## Wiring a real network (recommended: AdMob with UMP consent)

Roadmap D23 (2026-10-20): AdMob directly, because its Unity plugin ships Google's UMP consent
form, which is certified for the EEA/UK. That gives one SDK and no separate consent tool.
LevelPlay mediates more networks but needs a consent tool on top; mediation can be added
later through AdMob.

1. Import the Google Mobile Ads Unity plugin (it brings UMP). Create the app and the
   interstitial and rewarded ad units in the AdMob console; set the app id in the plugin's
   settings.
2. Add an adapter next to the others, e.g. `Ads/Controller/AdMobAdService.cs`, implementing
   `IAdService` on top of the SDK's interstitial and rewarded ad objects (load on start,
   reload after each show, report `IsReady` from the SDK). Add the SDK's assembly to the
   `Demolition.asmdef` references.
3. At startup on phones only (e.g. in `Meta/Controller/AppBootstrap.Init`), request consent
   info through UMP and show the form if required. Then initialise the SDK and install the
   adapter:
   ```csharp
   #if UNITY_ANDROID || UNITY_IOS
   if (!Debug.isDebugBuild) Ads.AdManager.Service = new Ads.AdMobAdService();
   #endif
   ```
4. Add a **Privacy options** button to `SettingsView` that reopens the UMP form. Show it
   when UMP says it is required (EEA/UK players must be able to change their choice).
5. Test with Google's test ad units and a registered test device. Never tap live ads on your
   own phone: AdMob suspends accounts for invalid clicks.

LevelPlay (`com.unity.services.levelplay`) works the same way: one adapter class.

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
- **EU trader status (Google Play):** an app that earns money through ads makes the
  developer a trader under the Digital Services Act. Play then shows the developer's
  address, e-mail and phone number on the store page (roadmap D24).
- **Online:** the privacy policy says the relay sees players' IP addresses
  (`multiplayer.md` O4).

## Ideas for later

- Rewarded **continue** (rejoin once more in solo after going out).
- Rewarded **daily bonus** (extra cash on the daily challenge).
- Cosmetic skins (palette swaps of the demolition man) as IAP or cash unlocks.
