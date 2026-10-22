using System.Collections.Generic;
using System.Text;

namespace Demolition
{
    /// <summary>Button names in prompts, per <see cref="InputStyle"/>: [KEY] on a keyboard,
    /// (BUTTON) on a gamepad, the on-screen button's label on touch.</summary>
    public static class InputGlyphs
    {
        // Token, keyboard, gamepad, touch.
        private static readonly string[,] Glyphs =
        {
            { "{MOVE}", "[A] [D]", "(L-STICK)", "DRAG ON THE LEFT" },
            { "{JUMP}", "[SPACE]", "(A)", "JUMP" },
            { "{HIT}", "[Z]", "(X)", "HIT" },
            { "{TNT}", "[X]", "(B)", "TNT" },
            { "{SWAP}", "[Q] [E]", "(LB) (RB)", "SWAP" },
        };

        /// <summary>The message with every {MOVE}, {JUMP}, {HIT}, {TNT} and {SWAP} replaced by the
        /// buttons of each style in use, joined by " / " (a keyboard player and a gamepad
        /// player read "[SPACE] / (A)"). No styles: keyboard and gamepad.</summary>
        public static string Format(string message, IEnumerable<InputStyle> styles)
        {
            if (string.IsNullOrEmpty(message) || message.IndexOf('{') < 0)
                return message;
            var used = new bool[3];
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
                    if (!used[style])
                        continue;
                    if (glyph.Length > 0)
                        glyph.Append(" / ");
                    glyph.Append(Glyphs[row, style + 1]);
                }
                text.Replace(Glyphs[row, 0], glyph.ToString());
            }
            return text.ToString();
        }
    }
}
