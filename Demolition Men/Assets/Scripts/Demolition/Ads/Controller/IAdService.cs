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

    /// <summary>No ads (PC, TV, or ads removed).</summary>
    public sealed class NoAdService : IAdService
    {
        public bool IsInterstitialReady => false;
        public bool IsRewardedReady => false;
        public void ShowInterstitial(Action closed) => closed?.Invoke();
        public void ShowRewarded(Action<bool> finished) => finished?.Invoke(false);
    }
}
