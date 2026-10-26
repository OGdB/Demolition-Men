using UnityEngine;

namespace Demolition
{
    /// <summary>Which button glyphs to show a player: keys, pad buttons, or none (the
    /// on-screen touch buttons label themselves).</summary>
    public enum InputStyle { Keyboard, Gamepad, Touch }

    /// <summary>
    /// One frame of one player's intent, whatever produced it (keyboard, gamepad, touch
    /// controls, a bot). Players act only on frames, so a later network session can send
    /// frames instead of device state.
    /// </summary>
    public struct PlayerInputFrame
    {
        /// <summary>One <see cref="Zoom"/> step scales the camera's view by this factor.</summary>
        public const float ZoomStep = 1.1f;

        public Vector2 Move;
        public bool JumpPressed;
        public bool JumpHeld;
        public bool JumpReleased;
        public bool AttackHeld;
        public bool DynamitePressed;
        /// <summary>Fire the action this player carries (its meter must be full).</summary>
        public bool ActionPressed;
        /// <summary>+1 / -1 on the frame the next / previous weapon is requested, otherwise 0.</summary>
        public int WeaponCycle;
        /// <summary>Camera zoom steps this frame: + shows more, - less. A mouse wheel notch is
        /// one step, a held stick a few per second, a pinch as far as the fingers move. The
        /// camera is a view, so this isn't gameplay.</summary>
        public float Zoom;

        /// <summary>The player is doing something: moving, a button held or pressed.</summary>
        public bool IsActive => Move.sqrMagnitude > 0.04f || JumpHeld || AttackHeld || DynamitePressed || ActionPressed || WeaponCycle != 0;

        /// <summary>Both sources at once: moves add up (clamped), buttons combine.</summary>
        public static PlayerInputFrame Merge(PlayerInputFrame a, PlayerInputFrame b) => new PlayerInputFrame
        {
            Move = Vector2.ClampMagnitude(a.Move + b.Move, 1f),
            JumpPressed = a.JumpPressed || b.JumpPressed,
            JumpHeld = a.JumpHeld || b.JumpHeld,
            JumpReleased = a.JumpReleased || b.JumpReleased,
            AttackHeld = a.AttackHeld || b.AttackHeld,
            DynamitePressed = a.DynamitePressed || b.DynamitePressed,
            ActionPressed = a.ActionPressed || b.ActionPressed,
            WeaponCycle = a.WeaponCycle != 0 ? a.WeaponCycle : b.WeaponCycle,
            Zoom = a.Zoom + b.Zoom,
        };
    }
}
