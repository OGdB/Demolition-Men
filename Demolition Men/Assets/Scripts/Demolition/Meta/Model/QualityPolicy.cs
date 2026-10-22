namespace Demolition.Meta
{
    /// <summary>How hard the game works the device: render resolution and falling debris.</summary>
    public enum QualityTier { Low, High }

    /// <summary>The QUALITY setting: the device decides, or the player picks a tier.</summary>
    public enum QualityChoice { Auto, Low, High }

    /// <summary>
    /// What each quality tier means and which one a device gets on AUTO. Low is for budget
    /// phones (little memory or few cores) and phone browsers: the world renders at most 720
    /// pixels tall and fewer blocks of a collapse fall as physics bodies. High renders phones at
    /// most 1080 pixels tall and everything else at full resolution. The UI is never scaled.
    /// </summary>
    public static class QualityPolicy
    {
        /// <summary>A "4 GB" phone reports a little under 4096 MB.</summary>
        public const int LowMemoryMB = 4500;
        public const int LowCores = 6;
        public const int LowRenderHeight = 720;
        public const int HighPhoneRenderHeight = 1080;
        /// <summary>Blocks per collapse that fall as physics bodies on Low (each building allows 200).</summary>
        public const int LowFallingLimit = 100;

        /// <summary>The tier for AUTO. Memory or cores reported as 0 (unknown) count as enough.</summary>
        public static QualityTier AutoTier(bool mobile, bool browser, int memoryMB, int cores)
        {
            if (!mobile)
                return QualityTier.High;
            if (browser)
                return QualityTier.Low;
            bool weak = (memoryMB > 0 && memoryMB < LowMemoryMB) || (cores > 0 && cores < LowCores);
            return weak ? QualityTier.Low : QualityTier.High;
        }

        public static QualityTier Resolve(QualityChoice choice, QualityTier auto) => choice switch
        {
            QualityChoice.Low => QualityTier.Low,
            QualityChoice.High => QualityTier.High,
            _ => auto,
        };

        /// <summary>The fraction of the screen the world renders at: the short side (the height,
        /// in landscape) capped at the tier's height.</summary>
        public static float RenderScale(QualityTier tier, bool mobile, int shortSide)
        {
            int cap = tier == QualityTier.Low ? LowRenderHeight : mobile ? HighPhoneRenderHeight : int.MaxValue;
            return shortSide <= cap ? 1f : (float)cap / shortSide;
        }

        /// <summary>The tier's cap on blocks that fall as physics bodies in one collapse.</summary>
        public static int FallingLimit(QualityTier tier) => tier == QualityTier.Low ? LowFallingLimit : int.MaxValue;
    }
}
