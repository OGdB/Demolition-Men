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

        /// <summary>Until the server picks: the daily's level for this UTC date (development builds).</summary>
        public static RankedDay Local(DateTime utc, string template, RankedMode mode) =>
            new RankedDay { Day = DayOf(utc), Template = template, Seed = Daily.Seed(utc.Date), Mode = mode };
    }
}
