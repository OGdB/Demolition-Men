using UnityEngine;

namespace Demolition
{
    /// <summary>What a keyboard player has of the keyboard: all of it, or one half while
    /// another player is on the same keyboard.</summary>
    public enum KeyHalf { Whole, Left, Right }

    /// <summary>The keys' state this frame, by the Input System's <c>Key</c> names ("A",
    /// "Space", "UpArrow").</summary>
    public interface IKeyState
    {
        bool Held(string key);
        bool Pressed(string key);
        bool Released(string key);
    }

    /// <summary>
    /// The keyboard's bindings.
    ///
    /// One player has all of it (Whole): A/D or ◀/▶ move · Space, W or ▲ jump · Z or F hit ·
    /// X or R TNT · C action · Q/E weapon.
    /// Two players have a half each, and no key is in both:
    ///   Left:  A/D move · Space or W jump · F hit · R TNT · G action · Q/E weapon
    ///   Right: ◀/▶ move · ▲ jump       · K hit · O TNT · L action · U/I weapon
    /// On the left half F, R, Q and E do what they do for one player; the right half has the
    /// same shape under the other hand (hit and action on the home row, TNT above them).
    /// The mouse and the wheel's zoom are DeviceInputSource's, Esc the pause menu's.
    ///
    /// The first key of each list is the one <see cref="InputGlyphs"/> shows; a half's first
    /// jump key is also its join key in the lobby.
    /// </summary>
    public static class KeyLayout
    {
        private sealed class Keys
        {
            public string[] Left, Right, Up, Down, Jump, Hit, Tnt, Action, Previous, Next;
        }

        // In KeyHalf order.
        private static readonly Keys[] Halves =
        {
            new Keys
            {
                Left = new[] { "A", "LeftArrow" }, Right = new[] { "D", "RightArrow" },
                Up = new[] { "W", "UpArrow" }, Down = new[] { "S", "DownArrow" },
                Jump = new[] { "Space", "W", "UpArrow" },
                Hit = new[] { "Z", "F" }, Tnt = new[] { "X", "R" }, Action = new[] { "C" },
                Previous = new[] { "Q" }, Next = new[] { "E" },
            },
            new Keys
            {
                Left = new[] { "A" }, Right = new[] { "D" }, Up = new[] { "W" }, Down = new[] { "S" },
                Jump = new[] { "Space", "W" },
                Hit = new[] { "F" }, Tnt = new[] { "R" }, Action = new[] { "G" },
                Previous = new[] { "Q" }, Next = new[] { "E" },
            },
            new Keys
            {
                Left = new[] { "LeftArrow" }, Right = new[] { "RightArrow" },
                Up = new[] { "UpArrow" }, Down = new[] { "DownArrow" },
                Jump = new[] { "UpArrow" },
                Hit = new[] { "K" }, Tnt = new[] { "O" }, Action = new[] { "L" },
                Previous = new[] { "U" }, Next = new[] { "I" },
            },
        };

        /// <summary>The glyphs a player on <paramref name="half"/> reads.</summary>
        public static InputStyle Style(KeyHalf half) =>
            half == KeyHalf.Left ? InputStyle.KeysLeft : half == KeyHalf.Right ? InputStyle.KeysRight : InputStyle.Keyboard;

        /// <summary>This frame from the keys of <paramref name="half"/>. The move isn't
        /// clamped: the source adds a stick to it first.</summary>
        public static PlayerInputFrame Read(KeyHalf half, IKeyState keys)
        {
            Keys map = Halves[(int)half];
            return new PlayerInputFrame
            {
                Move = new Vector2((Held(keys, map.Right) ? 1f : 0f) - (Held(keys, map.Left) ? 1f : 0f),
                    (Held(keys, map.Up) ? 1f : 0f) - (Held(keys, map.Down) ? 1f : 0f)),
                JumpPressed = Pressed(keys, map.Jump),
                JumpHeld = Held(keys, map.Jump),
                JumpReleased = Released(keys, map.Jump),
                AttackHeld = Held(keys, map.Hit),
                DynamitePressed = Pressed(keys, map.Tnt),
                ActionPressed = Pressed(keys, map.Action),
                WeaponCycle = Pressed(keys, map.Next) ? 1 : Pressed(keys, map.Previous) ? -1 : 0,
            };
        }

        private static bool Held(IKeyState keys, string[] names)
        {
            foreach (string name in names)
                if (keys.Held(name))
                    return true;
            return false;
        }

        private static bool Pressed(IKeyState keys, string[] names)
        {
            foreach (string name in names)
                if (keys.Pressed(name))
                    return true;
            return false;
        }

        private static bool Released(IKeyState keys, string[] names)
        {
            foreach (string name in names)
                if (keys.Released(name))
                    return true;
            return false;
        }
    }
}
