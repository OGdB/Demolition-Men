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

        /// <summary>Master gain at VOLUME 100%: the music is mastered at -8 to -10 LUFS and the
        /// player sounds play at full scale, so without it a run mixes to about -10.5 LUFS and clips.</summary>
        private const float Headroom = 0.5f;

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

            TextAsset looks = Resources.Load<TextAsset>("Looks");
            LookCatalog.Load(looks != null ? looks.text : null);

            Profile.SettingsChanged -= Apply;
            Profile.SettingsChanged += Apply;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.focusChanged -= OnFocusChanged;
            Application.focusChanged += OnFocusChanged;
            Apply();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

        // Android kills a backgrounded app without quitting it.
        private static void OnFocusChanged(bool focused)
        {
            if (!focused)
                Profile.Save();
        }

        public static void Apply()
        {
            GameSettings s = Profile.Data.settings;
            AudioListener.volume = Mathf.Clamp01(s.masterVolume) * Headroom;
            Platform.TouchMode = (TouchControlsMode)s.touchControls;
            TouchControls.Scale = Mathf.Clamp(s.touchScale, 0.6f, 1.6f);
            TouchControls.Opacity = Mathf.Clamp(s.touchOpacity, 0.15f, 1f);
            CameraShake.Enabled = s.screenShake;
            Quality.Apply(s);

            GameObject music = GameObject.Find(MusicObjectName);
            if (music != null && music.TryGetComponent(out AudioSource source))
                source.volume = Mathf.Clamp01(s.musicVolume);
        }
    }
}
