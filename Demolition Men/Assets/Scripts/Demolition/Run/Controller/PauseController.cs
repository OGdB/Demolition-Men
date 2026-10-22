using System;
using Demolition.Meta;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demolition.Run
{
    /// <summary>
    /// Pauses the run: Esc (also the Android back button), Start on any gamepad, the
    /// on-screen pause button, or the app losing focus on a phone. Freezes time;
    /// <see cref="PauseView"/> shows the menu. Online the host's pause freezes both devices
    /// (the client hears it in the host's run state); a client's own menu is only an overlay
    /// (docs/multiplayer.md D17).
    /// </summary>
    [RequireComponent(typeof(RunController))]
    public sealed class PauseController : MonoBehaviour
    {
        private RunController _run;

        public static bool IsPaused { get; private set; }

        /// <summary>Client: the host has its pause menu open, so the run stands still here too.</summary>
        public static bool HostPaused { get; private set; }

        /// <summary>The pause menu is up: this device's own, or on a client the host's.</summary>
        public static bool MenuOpen => IsPaused || HostPaused;

        /// <summary>Time stands still: paused offline or on the host, or the host paused.</summary>
        public static bool Frozen => HostPaused || IsPaused && !NetRole.IsClient;

        private static int _resumedFrame = -10;

        /// <summary>Players ignore input while paused and on the frame play resumes (the
        /// button press that resumed must not also jump or throw).</summary>
        public static bool BlocksInput => MenuOpen || Time.frameCount - _resumedFrame <= 1;

        /// <summary>The menu opened or closed, or the host paused or resumed: carries <see cref="MenuOpen"/>.</summary>
        public static event Action<bool> Changed;

        private void Awake() => _run = GetComponent<RunController>();

        private void OnDestroy()
        {
            SetHostPaused(false);
            SetPaused(false);
        }

        private void Update()
        {
            if (PausePressed() && !SettingsView.IsOpen && !HostPaused) // only the host resumes its pause
                Toggle();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && Platform.IsMobile)
                SetPaused(true);
        }

        public void Toggle() => SetPaused(!IsPaused);

        public void SetPaused(bool paused)
        {
            if (paused && (_run == null || _run.State == null || _run.State.IsOver))
                return;
            if (paused == IsPaused)
                return;
            IsPaused = paused;
            if (!paused)
                _resumedFrame = Time.frameCount;
            Apply();
        }

        /// <summary>Client: the host paused or resumed (<c>RunNet</c>, from its run state).</summary>
        public static void SetHostPaused(bool paused)
        {
            if (paused == HostPaused)
                return;
            HostPaused = paused;
            Apply();
        }

        private static void Apply()
        {
            Time.timeScale = Frozen ? 0f : 1f;
            AudioListener.pause = Frozen;
            Changed?.Invoke(MenuOpen);
        }

        private static bool PausePressed()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                return true;
            foreach (Gamepad pad in Gamepad.all)
                if (pad.startButton.wasPressedThisFrame)
                    return true;
            return false;
        }
    }
}
