using System;

namespace Demolition.Levels
{
    /// <summary>The daily challenge: one seed per calendar date, the same on every device.</summary>
    public static class Daily
    {
        private static readonly DateTime Epoch = new DateTime(2026, 1, 1);

        public static string Id(DateTime date) => "daily-" + date.ToString("yyyyMMdd");

        public static int Seed(DateTime date) => unchecked((date.Year * 10000 + date.Month * 100 + date.Day) * 7919);

        /// <summary>Which of <paramref name="count"/> campaign levels lends its pacing today.</summary>
        public static int TemplateIndex(DateTime date, int count)
        {
            int days = (int)(date.Date - Epoch).TotalDays;
            return count <= 0 ? 0 : ((days % count) + count) % count;
        }
    }
}
