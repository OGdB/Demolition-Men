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

        /// <summary>Hand text to the system's share sheet, which Android has. False where there
        /// is none or it didn't open: the caller copies the text instead.</summary>
        public static bool Share(string text)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND");
                using AndroidJavaObject typed = intent.Call<AndroidJavaObject>("setType", "text/plain");
                using AndroidJavaObject filled = intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text);
                using var intents = new AndroidJavaClass("android.content.Intent");
                using AndroidJavaObject chooser = intents.CallStatic<AndroidJavaObject>("createChooser", intent, Application.productName);
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("startActivity", chooser);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Platform] no share sheet: {e.Message}");
            }
#endif
            return false;
        }

        /// <summary>The browser build on an Android phone or tablet, where the store's page is one tap from the app.</summary>
        public static bool IsAndroidBrowser =>
            Application.platform == RuntimePlatform.WebGLPlayer && SystemInfo.operatingSystem.Contains("Android");
    }
}
