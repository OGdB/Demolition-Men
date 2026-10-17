using System;
using Demolition.Meta;
using UnityEngine;

namespace Demolition.Ads
{
    /// <summary>
    /// The game's one door to ads: decides when an interstitial is due (<see cref="AdPolicy"/>)
    /// and whether a rewarded ad can be offered. Mobile builds use whatever
    /// <see cref="Service"/> a platform bootstrap installs; until an SDK is wired in,
    /// development builds simulate ads and release builds show none.
    /// </summary>
    public static class AdManager
    {
        private static IAdService _service;
        private static float _lastInterstitial = float.NegativeInfinity;

        /// <summary>Simulated ads in the editor too (to test the flow without a phone).</summary>
        public static bool SimulateInEditor = false;

        public static IAdService Service
        {
            get => _service ??= DefaultService();
            set => _service = value;
        }

        private static bool Enabled => (Platform.IsMobile || (Application.isEditor && SimulateInEditor)) && !Profile.Data.adsRemoved;

        private static IAdService DefaultService()
        {
            if (Platform.IsMobile ? Debug.isDebugBuild : Application.isEditor && SimulateInEditor)
                return new SimulatedAdService();
            return new NoAdService();
        }

        /// <summary>A rewarded "double cash" can be offered right now.</summary>
        public static bool CanOfferReward => Enabled && Service.IsRewardedReady;

        public static void ShowRewarded(Action<bool> finished)
        {
            if (!CanOfferReward)
            {
                finished?.Invoke(false);
                return;
            }
            Service.ShowRewarded(finished);
        }

        /// <summary>Between runs: show an interstitial if one is due, then continue.</summary>
        public static void BetweenRuns(Action then)
        {
            SaveData save = Profile.Data;
            // Never while connected: an ad pauses the app and the other device times out (D16).
            bool due = !NetRole.IsOnline && Enabled && Service.IsInterstitialReady && AdPolicy.InterstitialDue(save.runsPlayed, save.runsSinceAd,
                Time.unscaledTime - _lastInterstitial, save.adsRemoved);
            if (!due)
            {
                then?.Invoke();
                return;
            }
            _lastInterstitial = Time.unscaledTime;
            save.runsSinceAd = 0;
            Profile.Save();
            Service.ShowInterstitial(then);
        }
    }
}
