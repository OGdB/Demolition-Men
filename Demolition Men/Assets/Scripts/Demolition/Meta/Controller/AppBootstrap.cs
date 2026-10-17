using Demolition.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Demolition.Meta
{
    /// <summary>
    /// Runs once before the first scene: frame rate and screen setup for the platform,
    /// then applies the saved settings, now and whenever they change.
    /// </summary>
    public static class AppBootstrap
    {
        /// <summary>Scene objects with this name play the music (volume follows the settings).</summary>
        public const string MusicObjectName = "BackGroundMusic";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            if (Platform.IsMobile)
            {
                Application.targetFrameRate = 60;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
                Screen.autorotateToPortrait = false;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = true;
                Screen.autorotateToLandscapeRight = true;
                Screen.orientation = ScreenOrientation.AutoRotation;
            }

            Profile.SettingsChanged -= Apply;
            Profile.SettingsChanged += Apply;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

        public static void Apply()
        {
            GameSettings s = Profile.Data.settings;
            AudioListener.volume = Mathf.Clamp01(s.masterVolume);
            Platform.TouchMode = (TouchControlsMode)s.touchControls;
            TouchControls.Scale = Mathf.Clamp(s.touchScale, 0.6f, 1.6f);
            TouchControls.Opacity = Mathf.Clamp(s.touchOpacity, 0.15f, 1f);
            CameraShake.Enabled = s.screenShake;

            GameObject music = GameObject.Find(MusicObjectName);
            if (music != null && music.TryGetComponent(out AudioSource source))
                source.volume = Mathf.Clamp01(s.musicVolume);
        }
    }
}
