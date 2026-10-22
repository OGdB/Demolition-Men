# Monetization: free with ads

**Status:** AdMob wired in on Android (2026-10-22): the adapter, UMP consent and the Privacy
options button are in; the consent message in AdMob waits for a privacy policy URL, and nothing
has run on a phone yet (see *What's left*).

The game ships free. Phones and tablets show ads; PC and TV builds show none. The game
code never talks to an ad SDK directly: everything goes through one interface, so the
network can be picked (or swapped) without touching gameplay.

## What is built

| Piece | Where | What it does |
|---|---|---|
| `AdPolicy` (model) | `Scripts/Demolition/Ads/Model/` | When an interstitial is allowed: not in a new profile's first 2 runs, at most every 3 runs, at least 3 minutes apart, never once ads are removed. Unit-tested. |
| `IAdService` | `Ads/Controller/IAdService.cs` | What the game needs from a network: `IsInterstitialReady`, `IsRewardedReady`, `ShowInterstitial(closed)`, `ShowRewarded(finished(earned))`. |
| `NoAdService` | same file | Default on PC/TV and release phone builds until an SDK adapter is installed. |
| `SimulatedAdService` | `Ads/View/` | Development phone builds (and the editor with `AdManager.SimulateInEditor = true`): a full-screen card that closes after 2.5 s, so the flow can be tested. |
| `AdManager` | `Ads/Controller/` | The single door: `BetweenRuns(then)` shows an interstitial if due, `CanOfferReward` / `ShowRewarded` for the rewarded offer; `Consent` (`IAdConsent`) for the Privacy options button. |
| `AdMobAds`, `AdMobAdService` | `Ads/Controller/AdMob/` (asmdef `Demolition.Ads.AdMob`) | Android only, compiled once `com.google.ads.mobile` is installed. At start: UMP consent (form where required), `MobileAds.Initialize` once `CanRequestAds`, then one interstitial and one rewarded kept loaded (retry 10 s doubling to 5 min, replaced before the 1 h expiry). Development builds load Google's test units. Not installed when ads are removed. |
| PRIVACY button | `Meta/View/SettingsView` | Shown when UMP says privacy options are required (EEA/UK); reopens the consent form. |

Placements:
- **Interstitial**: when the player leaves the results screen (Next, Retry or Menu), if
  `AdPolicy` says it is due. Never during a run, never on the first launch.
- **Rewarded**: the results screen offers **WATCH AD: 2x CASH** when the run earned cash
  and a rewarded ad is loaded. Watching it banks the run's cash a second time. Always
  opt-in.
- **Remove ads**: `SaveData.adsRemoved` turns all ads off. Wire it to a one-off in-app
  purchase (Unity IAP) when there is one; nothing sets it today.

## AdMob setup

Roadmap D23 (2026-10-20): AdMob directly, because its Unity plugin ships Google's UMP consent
form, which is certified for the EEA/UK. One SDK, no separate consent tool; mediation can be
added later through AdMob.

| What | Value |
|---|---|
| Plugin | `com.google.ads.mobile` 11.6.0 from OpenUPM (scoped registry in `Packages/manifest.json`); it pulls in `com.unity.external-dependency-manager` |
| App (Android) | Demolition Men, `ca-app-pub-4210768413755346~5466851538`, in `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset` |
| Interstitial "Between runs" | `ca-app-pub-4210768413755346/3247882308` |
| Rewarded "Double cash" | `ca-app-pub-4210768413755346/3762887629` |
| Test units (development builds) | Google's samples: interstitial `ca-app-pub-3940256099942544/1033173712`, rewarded `…/5224354917` |

The AdMob console shows the app as "Limited ad serving" until it is linked to its Play listing
(Apps ▸ Add store), which can only happen once the app is on Play.

### What's left
1. **Editor:** open the project so Unity installs the plugin. Accept External Dependency
   Manager's prompts (Android Resolver: enable auto-resolution, let it patch the Gradle
   templates), then run Assets ▸ External Dependency Manager ▸ Android Resolver ▸ Force
   Resolve with Android as the build target. Check Assets ▸ Google Mobile Ads ▸ Settings shows
   the app id above.
2. **Privacy policy:** live at `https://relay.oeds.space/privacy` since 2026-10-22, served by
   the relay (`server/relay/Relay/wwwroot/privacy.html`, OG Studio, `OGpriv@proton.me`); the
   game links to it from CREDITS (`GameConfig.privacyPolicyUrl`). Edit the file and redeploy the
   relay to change it. The AdMob consent message ("Demolition Men consent", Privacy &
   messaging ▸ European regulations) is **published** (2026-10-22) with Consent, **Do not
   consent** (every country, so refusing is one tap) and Manage options; ad partners are
   Google's "common ad partners" default. A message for US state regulations is optional.
3. **On a phone** (development build): test ads appear between runs and for 2x cash; with
   `ConsentDebugSettings.DebugGeography = EEA` and the phone's hashed id (logged by UMP)
   the form shows and Settings gets PRIVACY.
4. **After launch:** link the Play listing in AdMob; put an `app-ads.txt` on the developer
   website named in the listing (AdMob ▸ Apps ▸ app-ads.txt shows the line).

Never tap live ads on your own phone: AdMob suspends accounts for invalid clicks. Register it
as a test device (AdMob ▸ Settings ▸ Test devices) before installing a release build.

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
- **EU trader status (Google Play):** every developer declares trader or non-trader under the
  Digital Services Act: a trader acts for the purposes of a trade, business or profession.
  It's a self-declaration, and earning money through ads points towards trader. A trader's
  address, e-mail and phone number appear on the store page (roadmap D24). It is separate from
  registering a business with the KVK, which Dutch law decides on its own criteria.
- **Online:** the privacy policy says the relay sees players' IP addresses
  (`multiplayer.md` O4).

## Ideas for later

- Rewarded **continue** (rejoin once more in solo after going out).
- Rewarded **daily bonus** (extra cash on the daily challenge).
- Cosmetic skins (palette swaps of the demolition man) as IAP or cash unlocks.
