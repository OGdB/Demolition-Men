using UnityEngine;
using UnityEngine.InputSystem;

namespace Demolition
{
    /// <summary>
    /// Merged gamepad + keyboard input for one player, replacing the old
    /// PlayerInput/StupidController/ControllerManager chain with a single component.
    ///
    /// Faithful to the original feel: <see cref="MoveRaw"/> drives the physics while a
    /// SmoothDamp'd, deadzoned copy (<see cref="MoveSmoothed"/>) drives sprite flip and
    /// the animator's Speed float, so the run cycle eases in and out the way it used to.
    ///
    /// Gamepad: left stick move · A jump · X attack (hold) · B dynamite · LB/RB weapon.
    /// Keyboard: A/D or ◀/▶ move · Space/W jump · Z, F or L-Mouse attack · X, R or
    /// R-Mouse dynamite · Q/E weapon.
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
        /// <summary>The last input came from a gamepad (for button prompts).</summary>
        public bool UsingGamepad { get; private set; }

        private Vector2 _smoothed;
        private Vector2 _smoothedVelocity;
        private InputDevice _assigned;

        /// <summary>Read only this device (keyboard + mouse, or one gamepad) instead of the
        /// default gamepad index + keyboard. Null restores the default.</summary>
        public void AssignDevice(InputDevice device)
        {
            _assigned = device;
            UsingGamepad = device != null ? device is Gamepad : !keyboardEnabled;
        }

        private void Awake() => UsingGamepad = !keyboardEnabled;

        private void Update()
        {
            Gamepad pad;
            Keyboard kb;
            if (_assigned != null)
            {
                pad = _assigned as Gamepad;
                kb = _assigned as Keyboard;
            }
            else
            {
                pad = Gamepad.all.Count > gamepadIndex ? Gamepad.all[gamepadIndex] : null;
                kb = keyboardEnabled ? Keyboard.current : null;
            }
            Mouse mouse = kb != null ? Mouse.current : null;

            Vector2 raw = Vector2.zero;
            bool jumpDown = false, jumpHeld = false, jumpUp = false;
            bool attack = false, dynamiteDown = false;
            int cycle = 0;

            if (pad != null)
            {
                raw += pad.leftStick.ReadValue();
                jumpDown |= pad.aButton.wasPressedThisFrame;
                jumpHeld |= pad.aButton.isPressed;
                jumpUp |= pad.aButton.wasReleasedThisFrame;
                attack |= pad.xButton.isPressed;
                dynamiteDown |= pad.bButton.wasPressedThisFrame;
                if (pad.rightShoulder.wasPressedThisFrame) cycle = 1;
                else if (pad.leftShoulder.wasPressedThisFrame) cycle = -1;
                if (pad.wasUpdatedThisFrame && (pad.leftStick.ReadValue().sqrMagnitude > 0.25f || AnyButton(pad)))
                    UsingGamepad = true;
            }

            if (kb != null)
            {
                float x = 0f, y = 0f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
                raw += new Vector2(x, y);

                jumpDown |= kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame
                            || kb.upArrowKey.wasPressedThisFrame;
                jumpHeld |= kb.spaceKey.isPressed || kb.wKey.isPressed || kb.upArrowKey.isPressed;
                jumpUp |= kb.spaceKey.wasReleasedThisFrame || kb.wKey.wasReleasedThisFrame
                          || kb.upArrowKey.wasReleasedThisFrame;

                attack |= kb.zKey.isPressed || kb.fKey.isPressed;
                dynamiteDown |= kb.xKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame;

                if (kb.eKey.wasPressedThisFrame) cycle = 1;
                else if (kb.qKey.wasPressedThisFrame) cycle = -1;
                if (kb.anyKey.wasPressedThisFrame)
                    UsingGamepad = false;
            }

            if (mouse != null)
            {
                attack |= mouse.leftButton.isPressed;
                dynamiteDown |= mouse.rightButton.wasPressedThisFrame;
                if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
                    UsingGamepad = false;
            }

            MoveRaw = Vector2.ClampMagnitude(raw, 1f);

            _smoothed = Vector2.SmoothDamp(_smoothed, MoveRaw, ref _smoothedVelocity, 1f / joystickDampSpeed);
            MoveSmoothed = _smoothed.magnitude < deadzone ? Vector2.zero : _smoothed;

            JumpDown = jumpDown;
            JumpHeld = jumpHeld;
            JumpUp = jumpUp;
            AttackHeld = attack;
            DynamiteDown = dynamiteDown;
            WeaponCycle = cycle;
        }

        private static bool AnyButton(Gamepad pad) =>
            pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame
            || pad.buttonWest.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame
            || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame
            || pad.startButton.wasPressedThisFrame;
    }
}
