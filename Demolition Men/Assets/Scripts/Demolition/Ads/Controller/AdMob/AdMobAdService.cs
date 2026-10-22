using System;
using System.Threading.Tasks;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using UnityEngine;

namespace Demolition.Ads.AdMob
{
    /// <summary>
    /// <see cref="IAdService"/> on the Google Mobile Ads SDK: keeps one interstitial and one
    /// rewarded ad loaded, loads the next as soon as one is shown, retries a failed load after
    /// a growing pause and swaps out an ad before it expires. Nothing loads before
    /// <see cref="Start"/>, which <see cref="AdMobAds"/> calls once consent allows ads.
    /// SDK callbacks are moved to the main thread (<see cref="MobileAdsEventExecutor"/>).
    /// </summary>
    public sealed class AdMobAdService : IAdService
    {
        private const float FirstRetrySeconds = 10f;
        private const float MaxRetrySeconds = 300f;
        /// <summary>Loaded ads expire after an hour; replace them a little before.</summary>
        private const float StaleSeconds = 55f * 60f;

        private readonly string _interstitialId;
        private readonly string _rewardedId;
        private InterstitialAd _interstitial;
        private RewardedAd _rewarded;
        private bool _loadingInterstitial;
        private bool _loadingRewarded;
        private int _interstitialFailures;
        private int _rewardedFailures;
        private bool _started;

        public AdMobAdService(string interstitialId, string rewardedId)
        {
            _interstitialId = interstitialId;
            _rewardedId = rewardedId;
        }

        public bool IsInterstitialReady => _interstitial != null && _interstitial.CanShowAd();
        public bool IsRewardedReady => _rewarded != null && _rewarded.CanShowAd();

        /// <summary>Start loading (after consent and SDK initialisation). Main thread.</summary>
        public void Start()
        {
            if (_started)
                return;
            _started = true;
            LoadInterstitial();
            LoadRewarded();
        }

        public void ShowInterstitial(Action closed)
        {
            if (!IsInterstitialReady)
            {
                closed?.Invoke();
                return;
            }
            InterstitialAd ad = _interstitial;
            _interstitial = null;
            bool done = false;
            void Finish()
            {
                if (done)
                    return;
                done = true;
                ad.Destroy();
                LoadInterstitial();
                closed?.Invoke();
            }
            ad.OnAdFullScreenContentClosed += () => MobileAdsEventExecutor.ExecuteInUpdate(Finish);
            ad.OnAdFullScreenContentFailed += error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                Debug.LogWarning($"[Ads] interstitial failed to show: {error}");
                Finish();
            });
            ad.Show();
        }

        public void ShowRewarded(Action<bool> finished)
        {
            if (!IsRewardedReady)
            {
                finished?.Invoke(false);
                return;
            }
            RewardedAd ad = _rewarded;
            _rewarded = null;
            bool earned = false;
            bool done = false;
            void Finish()
            {
                if (done)
                    return;
                done = true;
                ad.Destroy();
                LoadRewarded();
                finished?.Invoke(earned);
            }
            ad.OnAdFullScreenContentClosed += () => MobileAdsEventExecutor.ExecuteInUpdate(Finish);
            ad.OnAdFullScreenContentFailed += error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                Debug.LogWarning($"[Ads] rewarded ad failed to show: {error}");
                Finish();
            });
            // The SDK reports the reward before the ad closes; both are queued in that order.
            ad.Show(_ => MobileAdsEventExecutor.ExecuteInUpdate(() => earned = true));
        }

        private void LoadInterstitial()
        {
            if (_loadingInterstitial || _interstitial != null)
                return;
            _loadingInterstitial = true;
            InterstitialAd.Load(_interstitialId, new AdRequest(), (ad, error) => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                _loadingInterstitial = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning($"[Ads] interstitial failed to load: {error}");
                    After(RetryDelay(++_interstitialFailures), LoadInterstitial);
                    return;
                }
                _interstitialFailures = 0;
                _interstitial = ad;
                After(StaleSeconds, () =>
                {
                    if (_interstitial != ad)
                        return;
                    _interstitial = null;
                    ad.Destroy();
                    LoadInterstitial();
                });
            }));
        }

        private void LoadRewarded()
        {
            if (_loadingRewarded || _rewarded != null)
                return;
            _loadingRewarded = true;
            RewardedAd.Load(_rewardedId, new AdRequest(), (ad, error) => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                _loadingRewarded = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning($"[Ads] rewarded ad failed to load: {error}");
                    After(RetryDelay(++_rewardedFailures), LoadRewarded);
                    return;
                }
                _rewardedFailures = 0;
                _rewarded = ad;
                After(StaleSeconds, () =>
                {
                    if (_rewarded != ad)
                        return;
                    _rewarded = null;
                    ad.Destroy();
                    LoadRewarded();
                });
            }));
        }

        /// <summary>10 s, 20 s, 40 s ... up to 5 minutes: no flood of requests while offline.</summary>
        private static float RetryDelay(int failures) =>
            Mathf.Min(MaxRetrySeconds, FirstRetrySeconds * Mathf.Pow(2f, Mathf.Min(failures - 1, 10)));

        // Started on the main thread, so the continuation comes back to it (Unity's
        // synchronisation context).
        private static async void After(float seconds, Action action)
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            action();
        }
    }
}
