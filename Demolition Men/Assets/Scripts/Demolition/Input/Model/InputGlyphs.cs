using System.Collections.Generic;
using System.Text;

namespace Demolition
{
    /// <summary>Button names in prompts, per <see cref="InputStyle"/>: [KEY] on a keyboard,
    /// (BUTTON) on a gamepad, the on-screen button's label on touch. The pause menu lists
    /// them all as its controls card. Two players on one keyboard each read their half's
    /// keys, the first key <see cref="KeyLayout"/> lists for each.</summary>
    public static class InputGlyphs
    {
        /// <summary>Controls (rows) the HUD asks for by number.</summary>
        public const int Tnt = 3, ActionButton = 4, Swap = 5;

        // Token, what it does (the controls card), then a column per InputStyle: keyboard,
        // gamepad, touch, the keyboard's left half, its right half.
        private static readonly string[,] Glyphs =
        {
            { "{MOVE}", "MOVE", "[A] [D]", "(L-STICK)", "DRAG ON THE LEFT", "[A] [D]", "[LEFT] [RIGHT]" },
            { "{JUMP}", "JUMP", "[SPACE]", "(A)", "JUMP", "[SPACE]", "[UP]" },
            { "{HIT}", "HIT", "[Z]", "(X)", "HIT", "[F]", "[K]" },
            { "{TNT}", "THROW TNT", "[X]", "(B)", "TNT", "[R]", "[O]" },
            { "{ACTION}", "ACTION", "[C]", "(Y)", "ACTION", "[G]", "[L]" },
            { "{SWAP}", "SWAP WEAPON", "[Q] [E]", "(LB) (RB)", "SWAP", "[Q] [E]", "[U] [I]" },
            { "{ZOOM}", "ZOOM", "MOUSE WHEEL", "(R-STICK)", "PINCH", "MOUSE WHEEL", "MOUSE WHEEL" },
            { "{PAUSE}", "PAUSE", "[ESC]", "(START)", "II", "[ESC]", "[ESC]" },
        };

        private static readonly string[] Names = { "KEYBOARD", "GAMEPAD", "TOUCH", "WASD KEYS", "ARROW KEYS" };

        /// <summary>How many controls the card lists.</summary>
        public static int Controls => Glyphs.GetLength(0);

        /// <summary>How many <see cref="InputStyle"/>s there are.</summary>
        public static int Styles => Glyphs.GetLength(1) - 2;

        /// <summary>The card's heading for <paramref name="style"/>.</summary>
        public static string Name(InputStyle style) => Names[(int)style];

        /// <summary>What <paramref name="control"/> does.</summary>
        public static string Action(int control) => Glyphs[control, 1];

        /// <summary><paramref name="control"/>'s buttons in <paramref name="style"/>.</summary>
        public static string Buttons(int control, InputStyle style) => Glyphs[control, (int)style + 2];

        /// <summary>The message with every {MOVE}, {JUMP}, {HIT}, {TNT}, {ACTION}, {SWAP}, {ZOOM} and {PAUSE}
        /// replaced by the buttons of each style in use, joined by " / " (a keyboard player and a
        /// gamepad player read "[SPACE] / (A)"), the same buttons once (both halves of a keyboard
        /// pause with [ESC]). No styles: keyboard and gamepad.</summary>
        public static string Format(string message, IEnumerable<InputStyle> styles)
        {
            if (string.IsNullOrEmpty(message) || message.IndexOf('{') < 0)
                return message;
            var used = new bool[Styles];
            bool any = false;
            foreach (InputStyle style in styles)
                any = used[(int)style] = true;
            if (!any)
                used[(int)InputStyle.Keyboard] = used[(int)InputStyle.Gamepad] = true;

            var text = new StringBuilder(message);
            var glyph = new StringBuilder();
            for (int row = 0; row < Glyphs.GetLength(0); row++)
            {
                glyph.Clear();
                for (int style = 0; style < used.Length; style++)
                {
                    if (!used[style] || Repeats(row, style, used))
                        continue;
                    if (glyph.Length > 0)
                        glyph.Append(" / ");
                    glyph.Append(Glyphs[row, style + 2]);
                }
                text.Replace(Glyphs[row, 0], glyph.ToString());
            }
            return text.ToString();
        }

        // An earlier style in use has the same buttons for this control.
        private static bool Repeats(int row, int style, bool[] used)
        {
            for (int earlier = 0; earlier < style; earlier++)
                if (used[earlier] && Glyphs[row, earlier + 2] == Glyphs[row, style + 2])
                    return true;
            return false;
        }
    }
}
