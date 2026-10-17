using System;
using Demolition.Meta;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demolition.Run
{
    /// <summary>
    /// Pauses the run: Esc (also the Android back button), Start on any gamepad, the
    /// on-screen pause button, or the app losing focus on a phone. Freezes time;
    /// <see cref="PauseView"/> shows the menu.
    /// </summary>
    [RequireComponent(typeof(RunController))]
    public sealed class PauseController : MonoBehaviour
    {
        private RunController _run;

        public static bool IsPaused { get; private set; }
        public static event Action<bool> Changed;

        private void Awake() => _run = GetComponent<RunController>();

        private void OnDestroy()
        {
            if (IsPaused)
                SetPaused(false);
        }

        private void Update()
        {
            if (PausePressed() && !SettingsView.IsOpen)
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
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            Changed?.Invoke(paused);
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
