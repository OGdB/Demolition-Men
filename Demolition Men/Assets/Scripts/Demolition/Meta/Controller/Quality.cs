using System;
using UnityEngine;

namespace Demolition.Meta
{
    /// <summary>
    /// The quality tier in effect: the QUALITY setting, or on AUTO the device's own
    /// (<see cref="QualityPolicy"/>). <see cref="AppBootstrap"/> applies it with the other
    /// settings: it caps falling debris here, and the Demolition.Lighting assembly hands
    /// <see cref="RenderScale"/> to URP when <see cref="Changed"/> fires.
    /// </summary>
    public static class Quality
    {
        private static QualityTier? _auto;
        private static bool _applied;

        /// <summary>What AUTO picks on this device.</summary>
        public static QualityTier Auto => _auto ??= QualityPolicy.AutoTier(Platform.IsMobile,
            Application.platform == RuntimePlatform.WebGLPlayer, SystemInfo.systemMemorySize, SystemInfo.processorCount);

        public static QualityTier Current { get; private set; } = QualityTier.High;

        /// <summary>The fraction of the screen resolution the world renders at.</summary>
        public static float RenderScale { get; private set; } = 1f;

        public static event Action Changed;

        public static void Apply(GameSettings settings)
        {
            QualityTier tier = QualityPolicy.Resolve((QualityChoice)settings.quality, Auto);
            // The short side: right after launch a phone may not have turned to landscape yet.
            float scale = QualityPolicy.RenderScale(tier, Platform.IsMobile, Mathf.Min(Screen.width, Screen.height));
            DestructibleBuilding.FallingLimit = QualityPolicy.FallingLimit(tier);
            if (_applied && tier == Current && Mathf.Approximately(scale, RenderScale))
                return;
            _applied = true;
            Current = tier;
            RenderScale = scale;
            Changed?.Invoke();
        }
    }
}
