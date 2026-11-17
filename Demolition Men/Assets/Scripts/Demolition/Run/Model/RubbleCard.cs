using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Demolition.Levels;
using UnityEngine;

namespace Demolition.Run
{
    /// <summary>
    /// The rubble card (docs/hit-plan.md §5.1, F2): a finished run as a few lines of text to
    /// paste anywhere. The street's buildings stand in a row, one mark each, left to right;
    /// street props and gates are not on it. The last line is there only with a link to give.
    /// <code>
    /// DEMOLITION MEN · Daily 12 Nov
    /// 82% ★★★ · 1:42
    /// 💥💥🏚️💥🏢💥💥🏚️
    /// play it: https://...
    /// </code>
    /// </summary>
    public static class RubbleCard
    {
        /// <summary>A building at least this far down is marked flattened; one at least
        /// <see cref="HalfDownAt"/> half down; the rest left standing. The same shares of the
        /// whole level light its third and its first star, so the strip and the stars agree.</summary>
        public const float FlattenedAt = RunScore.GradeS;
        public const float HalfDownAt = RunScore.GradeB;

        // Escapes, so the card comes out the same however this file is saved.
        public const string Flattened = "\U0001F4A5";      // collision
        public const string HalfDown = "\U0001F3DA\uFE0F"; // derelict house
        public const string Standing = "\U0001F3E2";       // office building
        private const string Rainbow = "\U0001F308";
        private const char Star = '\u2605', NoStar = '\u2606';
        private const string Dot = " \u00B7 ";

        public static string Mark(float destroyed) =>
            destroyed >= FlattenedAt ? Flattened : destroyed >= HalfDownAt ? HalfDown : Standing;

        /// <summary>What a daily is called on the card: "Daily 12 Nov" in every language.</summary>
        public static string DailyTitle(DateTime date) => "Daily " + date.ToString("d MMM", CultureInfo.InvariantCulture);

        /// <summary>The card of a finished run. The percentage and the stars are the results
        /// screen's; the stars carry a rainbow for a finish within par.</summary>
        /// <param name="title">The level's name, or <see cref="DailyTitle"/>.</param>
        /// <param name="destroyed">The level-wide demolition, 0..1.</param>
        /// <param name="buildings">How far down each building is, 0..1, in street order (<see cref="Street"/>).</param>
        /// <param name="link">Where to play; null or empty leaves the line out.</param>
        public static string Text(string title, float destroyed, bool withinPar, float seconds, IReadOnlyList<float> buildings, string link)
        {
            var card = new StringBuilder("DEMOLITION MEN");
            if (!string.IsNullOrEmpty(title))
                card.Append(Dot).Append(title);

            int stars = RunScore.Stars(RunScore.GradeFor(destroyed));
            card.Append('\n').Append(Mathf.FloorToInt(destroyed * 100f)).Append("% ")
                .Append(Star, stars).Append(NoStar, RunScore.MaxStars - stars);
            if (stars > 0 && withinPar)
                card.Append(Rainbow);
            int whole = Mathf.FloorToInt(seconds);
            card.Append(Dot).Append(whole / 60).Append(':').Append((whole % 60).ToString("00"));

            if (buildings.Count > 0)
                card.Append('\n');
            for (int i = 0; i < buildings.Count; i++)
                card.Append(Mark(buildings[i]));

            if (!string.IsNullOrEmpty(link))
                card.Append("\nplay it: ").Append(link);
            return card.ToString();
        }

        /// <summary>How far down each building on the card is, 0..1, left to right.
        /// <paramref name="removedCells"/> holds the cells gone from the standing structure per
        /// entry of <see cref="LevelLayout.Buildings"/>, counted as the run counts the level's.</summary>
        public static List<float> Street(LevelLayout layout, IReadOnlyList<int> removedCells)
        {
            var street = new List<float>();
            for (int i = 0; i < layout.Buildings.Count; i++) // left to right already: the grammar sorts them
            {
                BuildingPlacement building = layout.Buildings[i];
                if (!building.IsProp && !building.IsGate)
                    street.Add(Mathf.Clamp01((float)removedCells[i] / Mathf.Max(1, building.Spec.CellCount)));
            }
            return street;
        }
    }
}
