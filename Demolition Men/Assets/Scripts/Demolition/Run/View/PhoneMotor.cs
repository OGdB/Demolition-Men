using UnityEngine;

namespace Demolition.Run
{
    /// <summary>
    /// The phone's own motor, for the player on its touchscreen: Android's vibrator, with the
    /// pulse's strength where the phone can set it, and in a browser <c>navigator.vibrate</c>
    /// (Plugins/WebGL/Vibrate.jslib). A motor that is only on or off gets a shorter buzz for
    /// a weaker pulse. Nothing on any other platform.
    /// </summary>
    public static class PhoneMotor
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;
        private static AndroidJavaClass _effect;
        private static bool _looked, _amplitude;

        /// <summary>This device has a motor to buzz.</summary>
        public static bool Supported => Vibrator != null;

        // VibrationEffect and hasAmplitudeControl are API 26, the oldest Android the game runs on.
        private static AndroidJavaObject Vibrator
        {
            get
            {
                if (_looked)
                    return _vibrator;
                _looked = true;
                try
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    AndroidJavaObject vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    if (vibrator != null && vibrator.Call<bool>("hasVibrator"))
                    {
                        _amplitude = vibrator.Call<bool>("hasAmplitudeControl");
                        _effect = new AndroidJavaClass("android.os.VibrationEffect");
                        _vibrator = vibrator;
                    }
                }
                catch (System.Exception e)
                {
                    GiveUp(e);
                }
                return _vibrator;
            }
        }

        public static void Play(Pulse pulse)
        {
            AndroidJavaObject vibrator = Vibrator;
            if (vibrator == null)
                return;
            try
            {
                // -1 is VibrationEffect.DEFAULT_AMPLITUDE, for a motor that is only on or off.
                using AndroidJavaObject effect = _effect.CallStatic<AndroidJavaObject>("createOneShot",
                    (long)(_amplitude ? pulse.Milliseconds : pulse.OnOffMilliseconds), _amplitude ? pulse.Amplitude : -1);
                vibrator.Call("vibrate", effect);
            }
            catch (System.Exception e)
            {
                GiveUp(e);
            }
        }

        public static void Stop()
        {
            if (_vibrator == null)
                return;
            try
            {
                _vibrator.Call("cancel");
            }
            catch (System.Exception e)
            {
                GiveUp(e);
            }
        }

        // Said once, not per hit: without the permission below every call throws.
        private static void GiveUp(System.Exception e)
        {
            Debug.LogWarning($"[PhoneMotor] no vibration: {e.Message}");
            _vibrator = null;
        }

        // Never called. Unity puts android.permission.VIBRATE in the manifest for a script that
        // calls Handheld.Vibrate, and the project keeps no manifest of its own to ask in.
        [UnityEngine.Scripting.Preserve]
        private static void DeclareVibratePermission() => Handheld.Vibrate();
#elif UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern int DemolitionVibrate(int milliseconds);

        /// <summary>This device has a motor to buzz: a phone's browser that can vibrate (a
        /// desktop's has the call too, and no motor).</summary>
        public static bool Supported => Platform.IsMobile && DemolitionVibrate(0) != 0;

        public static void Play(Pulse pulse) => DemolitionVibrate(pulse.OnOffMilliseconds);

        public static void Stop() => DemolitionVibrate(0);
#else
        /// <summary>This device has a motor to buzz.</summary>
        public static bool Supported => false;

        public static void Play(Pulse pulse) { }

        public static void Stop() { }
#endif
    }
}
