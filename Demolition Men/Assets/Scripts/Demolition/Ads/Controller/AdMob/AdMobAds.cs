using System;
using Demolition.Meta;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace Demolition.Ads.AdMob
{
    /// <summary>
    /// Wires Google Mobile Ads into <see cref="AdManager"/> on Android: asks for consent with
    /// Google's UMP form where the law requires it (EEA, UK, Switzerland), initialises the SDK
    /// only once ads may be requested, and gives Settings the privacy options form.
    /// Development builds load Google's test ad units, release builds the game's own. The
    /// app id lives in <c>Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset</c>.
    /// </summary>
    public sealed class AdMobAds : IAdConsent
    {
        private const string InterstitialId = "ca-app-pub-4210768413755346/3247882308"; // "Between runs"
        private const string RewardedId = "ca-app-pub-4210768413755346/3762887629"; // "Double cash"
        // Google's sample units always serve test ads: safe to load and tap on any device.
        private const string TestInterstitialId = "ca-app-pub-3940256099942544/1033173712";
        private const string TestRewardedId = "ca-app-pub-3940256099942544/5224354917";

        private readonly AdMobAdService _service;
        private bool _initialising;

        private AdMobAds(AdMobAdService service) => _service = service;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Application.isEditor || Application.platform != RuntimePlatform.Android || Profile.Data.adsRemoved)
                return;
            bool test = Debug.isDebugBuild;
            var service = new AdMobAdService(test ? TestInterstitialId : InterstitialId, test ? TestRewardedId : RewardedId);
            var ads = new AdMobAds(service);
            AdManager.Service = service;
            AdManager.Consent = ads;
            ads.GatherConsent();
        }

        public bool PrivacyOptionsRequired =>
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        public void ShowPrivacyOptions(Action closed)
        {
            ConsentForm.ShowPrivacyOptionsForm(error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (error != null)
                    Debug.LogWarning($"[Ads] privacy options form: {error.Message}");
                // A player who first declined may have just allowed ads.
                StartAdsIfAllowed();
                closed?.Invoke();
            }));
        }

        private void GatherConsent()
        {
            // Consent given in an earlier session: start loading while the update runs.
            StartAdsIfAllowed();
            ConsentInformation.Update(new ConsentRequestParameters(), updateError => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (updateError != null)
                {
                    // Offline or misconfigured: an earlier session's consent still counts.
                    Debug.LogWarning($"[Ads] consent update failed: {updateError.Message}");
                    StartAdsIfAllowed();
                    return;
                }
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    if (formError != null)
                        Debug.LogWarning($"[Ads] consent form: {formError.Message}");
                    StartAdsIfAllowed();
                }));
            }));
        }

        /// <summary>Initialise the SDK and start loading once consent allows ad requests. Main thread.</summary>
        private void StartAdsIfAllowed()
        {
            if (_initialising || !ConsentInformation.CanRequestAds())
                return;
            _initialising = true;
            MobileAds.Initialize(status => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (status == null)
                {
                    Debug.LogWarning("[Ads] Google Mobile Ads failed to initialise");
                    _initialising = false;
                    return;
                }
                _service.Start();
            }));
        }
    }
}
