using UnityEngine;
using UnityEngine.InputSystem;

namespace Demolition
{
    public enum TouchControlsMode { Auto, On, Off }

    /// <summary>What the game runs on, as far as controls and screens care.</summary>
    public static class Platform
    {
        /// <summary>From the settings: show the touch controls automatically, always or never.</summary>
        public static TouchControlsMode TouchMode { get; set; } = TouchControlsMode.Auto;

        /// <summary>Show on-screen touch controls: a phone or tablet, unless the settings say otherwise.</summary>
        public static bool IsTouch
        {
            get
            {
                switch (TouchMode)
                {
                    case TouchControlsMode.On: return true;
                    case TouchControlsMode.Off: return false;
                    default: return Application.isMobilePlatform && Touchscreen.current != null;
                }
            }
        }

        /// <summary>A phone or tablet build (ads, no quit button, touch-first menus).</summary>
        public static bool IsMobile => Application.isMobilePlatform;
    }
}
