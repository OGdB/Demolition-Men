using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Run
{
    /// <summary>A job sheet as one line of rich text: objectives done green, done this run gold,
    /// out of reach this run struck through, the rest white.</summary>
    public static class JobSheetText
    {
        private static readonly string Done = ColorUtility.ToHtmlStringRGB(new Color(0.5f, 1f, 0.5f));
        private static readonly string Blown = ColorUtility.ToHtmlStringRGB(new Color(0.5f, 0.5f, 0.55f));

        /// <param name="done">Done on an earlier run (or this one), bit i for objective i.</param>
        /// <param name="fresh">Done for the first time this run.</param>
        /// <param name="blown">Out of reach for this run.</param>
        public static string Line(IReadOnlyList<ObjectiveKind> sheet, int done, int fresh = 0, int blown = 0)
        {
            var parts = new List<string>();
            for (int i = 0; i < sheet.Count; i++)
            {
                int bit = 1 << i;
                string text = Objectives.Text(sheet[i]);
                parts.Add((fresh & bit) != 0 ? $"<color=#{ColorUtility.ToHtmlStringRGB(UiFactory.Gold)}>{text} NEW!</color>"
                    : (done & bit) != 0 ? $"<color=#{Done}>{text} DONE</color>"
                    : (blown & bit) != 0 ? $"<color=#{Blown}><s>{text}</s></color>"
                    : text);
            }
            return "JOBS:  " + string.Join("   ", parts);
        }
    }
}
