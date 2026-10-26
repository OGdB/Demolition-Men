using UnityEngine;
using UnityEngine.InputSystem;

namespace Demolition
{
    /// <summary>
    /// Keyboard + mouse and/or one gamepad, read straight from the Input System.
    ///
    /// Gamepad: left stick move · A jump · X attack (hold) · B dynamite · Y action · LB/RB weapon ·
    /// right stick up/down zoom in/out.
    /// Keyboard: A/D or ◀/▶ move · Space/W jump · Z, F or L-Mouse attack · X, R or
    /// R-Mouse dynamite · C or M-Mouse action · Q/E weapon · mouse wheel zoom.
    /// </summary>
    public sealed class DeviceInputSource : IPlayerInputSource
    {
        private const float StickZoomSteps = 3f; // zoom steps per second, stick held all the way
        private const float StickZoomDeadzone = 0.35f;

        private readonly InputDevice _assigned;
        private readonly int _gamepadIndex;
        private readonly bool _keyboard;

        /// <summary>Read only this device (keyboard + mouse, or one gamepad).</summary>
        public DeviceInputSource(InputDevice device)
        {
            _assigned = device;
            Style = device is Gamepad ? InputStyle.Gamepad : InputStyle.Keyboard;
        }

        /// <summary>Default input when nobody joined: gamepad <paramref name="gamepadIndex"/>,
        /// plus the keyboard and mouse if <paramref name="keyboard"/>.</summary>
        public DeviceInputSource(int gamepadIndex, bool keyboard)
        {
            _gamepadIndex = gamepadIndex;
            _keyboard = keyboard;
            Style = keyboard ? InputStyle.Keyboard : InputStyle.Gamepad;
        }

        public InputStyle Style { get; private set; }

        /// <summary>The source for a joined device: touch controls for the touchscreen.</summary>
        public static IPlayerInputSource For(InputDevice device) =>
            device is Touchscreen ? TouchInputSource.Shared : new DeviceInputSource(device);

        public PlayerInputFrame Read()
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
                pad = Gamepad.all.Count > _gamepadIndex ? Gamepad.all[_gamepadIndex] : null;
                kb = _keyboard ? Keyboard.current : null;
            }
            Mouse mouse = kb != null ? Mouse.current : null;

            var frame = new PlayerInputFrame();
            Vector2 move = Vector2.zero;

            if (pad != null)
            {
                move += pad.leftStick.ReadValue();
                frame.JumpPressed |= pad.aButton.wasPressedThisFrame;
                frame.JumpHeld |= pad.aButton.isPressed;
                frame.JumpReleased |= pad.aButton.wasReleasedThisFrame;
                frame.AttackHeld |= pad.xButton.isPressed;
                frame.DynamitePressed |= pad.bButton.wasPressedThisFrame;
                frame.ActionPressed |= pad.yButton.wasPressedThisFrame;
                if (pad.rightShoulder.wasPressedThisFrame) frame.WeaponCycle = 1;
                else if (pad.leftShoulder.wasPressedThisFrame) frame.WeaponCycle = -1;
                float zoom = pad.rightStick.ReadValue().y; // pushed up: closer
                if (Mathf.Abs(zoom) > StickZoomDeadzone)
                    frame.Zoom -= zoom * StickZoomSteps * Time.unscaledDeltaTime;
                if (pad.wasUpdatedThisFrame && (pad.leftStick.ReadValue().sqrMagnitude > 0.25f || AnyButton(pad)))
                    Style = InputStyle.Gamepad;
            }

            if (kb != null)
            {
                float x = 0f, y = 0f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
                move += new Vector2(x, y);

                frame.JumpPressed |= kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame
                                     || kb.upArrowKey.wasPressedThisFrame;
                frame.JumpHeld |= kb.spaceKey.isPressed || kb.wKey.isPressed || kb.upArrowKey.isPressed;
                frame.JumpReleased |= kb.spaceKey.wasReleasedThisFrame || kb.wKey.wasReleasedThisFrame
                                      || kb.upArrowKey.wasReleasedThisFrame;

                frame.AttackHeld |= kb.zKey.isPressed || kb.fKey.isPressed;
                frame.DynamitePressed |= kb.xKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame;
                frame.ActionPressed |= kb.cKey.wasPressedThisFrame;

                if (kb.eKey.wasPressedThisFrame) frame.WeaponCycle = 1;
                else if (kb.qKey.wasPressedThisFrame) frame.WeaponCycle = -1;
                if (kb.anyKey.wasPressedThisFrame)
                    Style = InputStyle.Keyboard;
            }

            if (mouse != null)
            {
                frame.AttackHeld |= mouse.leftButton.isPressed;
                frame.DynamitePressed |= mouse.rightButton.wasPressedThisFrame;
                frame.ActionPressed |= mouse.middleButton.wasPressedThisFrame;
                if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
                    Style = InputStyle.Keyboard;
                // One step per frame the wheel turns: its delta is pixels on some platforms and
                // notches on others. Rolled up: closer.
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                    frame.Zoom -= Mathf.Sign(wheel);
            }

            frame.Move = Vector2.ClampMagnitude(move, 1f);
            return frame;
        }

        private static bool AnyButton(Gamepad pad) =>
            pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame
            || pad.buttonWest.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame
            || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame
            || pad.startButton.wasPressedThisFrame;
    }
}
