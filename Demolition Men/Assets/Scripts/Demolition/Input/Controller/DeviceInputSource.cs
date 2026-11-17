using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Demolition
{
    /// <summary>
    /// Keyboard + mouse and/or one gamepad, read straight from the Input System.
    ///
    /// Gamepad: left stick move · A jump · X attack (hold) · B dynamite · Y action · LB/RB weapon ·
    /// right stick up/down zoom in/out.
    /// Keyboard: the keys are <see cref="KeyLayout"/>'s (one player: A/D or ◀/▶ move · Space/W
    /// jump · Z or F attack · X or R dynamite · C action · Q/E weapon), plus L-Mouse attack ·
    /// R-Mouse dynamite · M-Mouse action · mouse wheel zoom. Two players on one keyboard each
    /// read their <see cref="KeyHalf"/>: nobody has the mouse buttons then (a palm on a
    /// laptop's trackpad would throw TNT), and the wheel zooms through the left half only,
    /// so a notch counts once.
    /// </summary>
    public sealed class DeviceInputSource : IPlayerInputSource, IKeyState
    {
        private const float StickZoomSteps = 3f; // zoom steps per second, stick held all the way
        private const float StickZoomDeadzone = 0.35f;

        private readonly InputDevice _assigned;
        private readonly int _gamepadIndex;
        private readonly bool _keyboard;
        private readonly KeyHalf _half;

        // KeyLayout names its keys as the Input System's Key enum does.
        private static readonly Dictionary<string, Key> KeysByName = new Dictionary<string, Key>();
        private Keyboard _reading; // the keyboard KeyLayout reads through this source

        /// <summary>Read only this device (keyboard + mouse, or one gamepad); of a keyboard
        /// two players share, only <paramref name="half"/>.</summary>
        public DeviceInputSource(InputDevice device, KeyHalf half = KeyHalf.Whole)
        {
            _assigned = device;
            _half = device is Keyboard ? half : KeyHalf.Whole;
            Style = device is Gamepad ? InputStyle.Gamepad : KeyLayout.Style(_half);
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
        public static IPlayerInputSource For(InputDevice device, KeyHalf half = KeyHalf.Whole) =>
            device is Touchscreen ? TouchInputSource.Shared : new DeviceInputSource(device, half);

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

            if (pad != null)
            {
                frame.Move = pad.leftStick.ReadValue();
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
                _reading = kb;
                frame = PlayerInputFrame.Merge(KeyLayout.Read(_half, this), frame); // the keys' weapon swap first, as before
                if (kb.anyKey.wasPressedThisFrame)
                    Style = KeyLayout.Style(_half);
            }

            if (mouse != null && _half == KeyHalf.Whole)
            {
                frame.AttackHeld |= mouse.leftButton.isPressed;
                frame.DynamitePressed |= mouse.rightButton.wasPressedThisFrame;
                frame.ActionPressed |= mouse.middleButton.wasPressedThisFrame;
                if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
                    Style = InputStyle.Keyboard;
            }

            if (mouse != null && _half != KeyHalf.Right)
            {
                // One step per frame the wheel turns: its delta is pixels on some platforms and
                // notches on others. Rolled up: closer.
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                    frame.Zoom -= Mathf.Sign(wheel);
            }

            frame.Move = Vector2.ClampMagnitude(frame.Move, 1f);
            return frame;
        }

        bool IKeyState.Held(string key) => Control(key).isPressed;
        bool IKeyState.Pressed(string key) => Control(key).wasPressedThisFrame;
        bool IKeyState.Released(string key) => Control(key).wasReleasedThisFrame;

        private KeyControl Control(string name)
        {
            if (!KeysByName.TryGetValue(name, out Key key))
                KeysByName[name] = key = (Key)System.Enum.Parse(typeof(Key), name);
            return _reading[key];
        }

        /// <summary>A button a player plays or joins with went down on <paramref name="pad"/> this frame.</summary>
        public static bool AnyButton(Gamepad pad) =>
            pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame
            || pad.buttonWest.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame
            || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame
            || pad.startButton.wasPressedThisFrame;
    }
}
