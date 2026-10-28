using System;
using Demolition.Levels;

namespace Demolition.Ranked
{
    /// <summary>
    /// The ranked level of a day (docs/ranked.md K-D4): the server names it, a campaign level's pacing
    /// with the day's seed, so every device builds the same one.
    /// </summary>
    public sealed class RankedDay
    {
        /// <summary>UTC date as yyyymmdd.</summary>
        public int Day;
        /// <summary>The campaign level whose pacing it borrows.</summary>
        public string Template = string.Empty;
        public int Seed;
        public RankedMode Mode;

        public string LevelId => $"ranked-{Day}-{(Mode == RankedMode.Coop ? "coop" : "solo")}";

        public string Date => $"{Day / 10000:0000}-{Day / 100 % 100:00}-{Day % 100:00}";

        public static int DayOf(DateTime utc) => utc.Year * 10000 + utc.Month * 100 + utc.Day;

        private static readonly DateTime Epoch = new DateTime(2026, 1, 1);

        /// <summary>The server's pick for a UTC date: the campaign levels in turn (sorted by id, so
        /// it doesn't depend on the order a build lists them), a seed of the date's own that the
        /// local daily doesn't use. Both boards play the same level.</summary>
        public static RankedDay Pick(DateTime utc, System.Collections.Generic.IReadOnlyList<string> templates, RankedMode mode)
        {
            var sorted = new System.Collections.Generic.List<string>(templates);
            sorted.Sort(string.CompareOrdinal);
            int days = (int)(utc.Date - Epoch).TotalDays;
            int index = sorted.Count == 0 ? 0 : ((days % sorted.Count) + sorted.Count) % sorted.Count;
            return new RankedDay
            {
                Day = DayOf(utc),
                Template = sorted.Count > 0 ? sorted[index] : string.Empty,
                Seed = unchecked(Daily.Seed(utc.Date) * 31 + 7),
                Mode = mode,
            };
        }

        /// <summary>Until the server picks: the daily's level for this UTC date (development builds).</summary>
        public static RankedDay Local(DateTime utc, string template, RankedMode mode) =>
            new RankedDay { Day = DayOf(utc), Template = template, Seed = Daily.Seed(utc.Date), Mode = mode };
    }
}
