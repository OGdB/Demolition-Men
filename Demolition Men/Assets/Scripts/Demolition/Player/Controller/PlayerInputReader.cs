using UnityEngine;
using UnityEngine.InputSystem;

namespace Demolition
{
    /// <summary>
    /// One player's input, sampled once per frame from an <see cref="IPlayerInputSource"/>
    /// (devices, the touch controls or a bot) and exposed to the motor and combat scripts.
    ///
    /// Faithful to the original feel: <see cref="MoveRaw"/> drives the physics while a
    /// SmoothDamp'd, deadzoned copy (<see cref="MoveSmoothed"/>) drives sprite flip and
    /// the animator's Speed float, so the run cycle eases in and out the way it used to.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        [Tooltip("Which connected gamepad drives this player (0 = first, 1 = second).")]
        [SerializeField] private int gamepadIndex = 0;
        [Tooltip("Let the keyboard/mouse drive this player too (turn off for player 2).")]
        [SerializeField] private bool keyboardEnabled = true;
        [SerializeField] private float joystickDampSpeed = 10f;
        [SerializeField] private float deadzone = 0.1f;

        /// <summary>Which player this is (0 = P1). Set by the run controller; credits damage and score.</summary>
        public int PlayerIndex { get; set; }

        public Vector2 MoveRaw { get; private set; }
        /// <summary>Smoothed + deadzoned move input — for facing and animation blend.</summary>
        public Vector2 MoveSmoothed { get; private set; }
        public bool JumpDown { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool JumpUp { get; private set; }
        public bool AttackHeld { get; private set; }
        public bool DynamiteDown { get; private set; }
        /// <summary>+1 / -1 on the frame the next / previous weapon is requested, otherwise 0.</summary>
        public int WeaponCycle { get; private set; }
        /// <summary>Button glyphs for this player's prompts.</summary>
        public InputStyle Style => Source.Style;

        /// <summary>Where this player's input comes from. Defaults to the gamepad index plus
        /// the keyboard (and the touch controls for player 1 on a touch device).</summary>
        public IPlayerInputSource Source
        {
            get => _source ??= DefaultSource();
            set => _source = value;
        }

        private IPlayerInputSource _source;
        private Vector2 _smoothed;
        private Vector2 _smoothedVelocity;

        /// <summary>Read only this device (keyboard + mouse, one gamepad, or the touch
        /// controls). Null restores the default.</summary>
        public void AssignDevice(InputDevice device) => _source = device != null ? DeviceInputSource.For(device) : null;

        private IPlayerInputSource DefaultSource()
        {
            var devices = new DeviceInputSource(gamepadIndex, keyboardEnabled);
            return keyboardEnabled && Platform.IsTouch
                ? new CombinedInputSource(devices, TouchInputSource.Shared)
                : devices;
        }

        private void Update()
        {
            PlayerInputFrame frame = Source.Read();

            MoveRaw = frame.Move;
            _smoothed = Vector2.SmoothDamp(_smoothed, MoveRaw, ref _smoothedVelocity, 1f / joystickDampSpeed);
            MoveSmoothed = _smoothed.magnitude < deadzone ? Vector2.zero : _smoothed;

            JumpDown = frame.JumpPressed;
            JumpHeld = frame.JumpHeld;
            JumpUp = frame.JumpReleased;
            AttackHeld = frame.AttackHeld;
            DynamiteDown = frame.DynamitePressed;
            WeaponCycle = frame.WeaponCycle;
        }
    }
}
