using System;

namespace Demolition.Ads
{
    /// <summary>
    /// An ad network as the game sees it. Implementations wrap an SDK (Unity LevelPlay,
    /// AdMob, ...); see docs/monetization.md. Callbacks run on the main thread.
    /// </summary>
    public interface IAdService
    {
        bool IsInterstitialReady { get; }
        bool IsRewardedReady { get; }

        /// <summary>Show a full-screen ad; <paramref name="closed"/> runs when it is gone (or failed).</summary>
        void ShowInterstitial(Action closed);

        /// <summary>Show a rewarded ad; <paramref name="finished"/> gets true if the reward was earned.</summary>
        void ShowRewarded(Action<bool> finished);
    }

    /// <summary>
    /// The ad SDK's consent side, for Settings: players where the law requires it (EEA, UK)
    /// must be able to reopen the consent form and change their choice.
    /// </summary>
    public interface IAdConsent
    {
        /// <summary>Settings must offer the privacy options form.</summary>
        bool PrivacyOptionsRequired { get; }

        /// <summary>Reopen the consent choices; <paramref name="closed"/> runs when the form is gone.</summary>
        void ShowPrivacyOptions(Action closed);
    }

    /// <summary>No ads (PC, TV, or ads removed).</summary>
    public sealed class NoAdService : IAdService
    {
        public bool IsInterstitialReady => false;
        public bool IsRewardedReady => false;
        public void ShowInterstitial(Action closed) => closed?.Invoke();
        public void ShowRewarded(Action<bool> finished) => finished?.Invoke(false);
    }
}
