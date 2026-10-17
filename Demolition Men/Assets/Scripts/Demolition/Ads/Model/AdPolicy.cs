namespace Demolition.Ads
{
    /// <summary>
    /// When the game may show an ad. Interstitials only between runs, never in the first
    /// runs of a profile, at most every few runs and minutes; nothing once ads are removed.
    /// Rewarded ads are always the player's choice (double the run's cash).
    /// </summary>
    public static class AdPolicy
    {
        public const int RunsBetweenInterstitials = 3;
        public const float MinSecondsBetweenInterstitials = 180f;
        /// <summary>New players get this many ad-free runs first.</summary>
        public const int GraceRuns = 4;

        public static bool InterstitialDue(int runsPlayed, int runsSinceAd, float secondsSinceAd, bool adsRemoved) =>
            !adsRemoved
            && runsPlayed > GraceRuns
            && runsSinceAd >= RunsBetweenInterstitials
            && secondsSinceAd >= MinSecondsBetweenInterstitials;
    }
}
