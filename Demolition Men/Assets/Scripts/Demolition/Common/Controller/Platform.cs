using System.Runtime.InteropServices;
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

        /// <summary>Put text on the system clipboard (a browser's too, through Plugins/WebGL/Clipboard.jslib).</summary>
        public static void Copy(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            DemolitionCopy(text);
#else
            GUIUtility.systemCopyBuffer = text;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void DemolitionCopy(string text);
#endif
    }
}
