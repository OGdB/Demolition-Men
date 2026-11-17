using System;

namespace Demolition.Meta
{
    /// <summary>
    /// One anonymous count for our own server (docs/hit-plan.md §5.1, F0). These fields are all of it,
    /// and server/relay's Counters.cs takes exactly these: nothing says who sent it. No id of the
    /// device, the install or the player, no name, no room code, no text anyone typed.
    /// </summary>
    [Serializable]
    public sealed class CountEvent
    {
        /// <summary>The event: one of the names in <see cref="Counters"/>.</summary>
        public string e;
        /// <summary>What the game runs on: web, android, pc or other.</summary>
        public string p;
        /// <summary>level_done: <see cref="Counters.LevelName"/>.</summary>
        public string level = string.Empty;
        /// <summary>level_done: 0-3.</summary>
        public int stars;
        /// <summary>level_done: 1 or 2.</summary>
        public int players;
        /// <summary>level_done: 0 on one device, 1 the online host, 2 the online guest (as <c>NetMode</c>).</summary>
        public int online;
        /// <summary>day: days since the first start, one of <see cref="Counters.Days"/>.</summary>
        public int since;
    }

    /// <summary>What the counters remember on the device so each thing is counted once. Never sent.</summary>
    [Serializable]
    public sealed class CounterState
    {
        /// <summary>The local day (a day number) the counters first ran here; 0 before that.</summary>
        public int installDay;
        /// <summary>The last local day the counters looked at.</summary>
        public int lastDay;
        /// <summary>The Tutorial's first finish is counted.</summary>
        public bool tutorial;
    }

    /// <summary>
    /// What the game counts and when (docs/hit-plan.md §5.1, F0): the first start once, the Tutorial's
    /// first finish once, every finished level, every shared run, every room a host sent an invite link
    /// to, and on days 0, 1, 7 and 30 after the first start that the game was opened that day. The device works the day out itself, so
    /// day-1 retention is the day-1 counts over the day-0 counts with nobody followed.
    /// </summary>
    public static class Counters
    {
        public const string FirstStart = "first_start";
        public const string TutorialDone = "tutorial_done";
        public const string LevelDone = "level_done";
        public const string Share = "share";
        public const string Invite = "invite";
        public const string Day = "day";

        /// <summary>The days since the first start that are counted; a session on any other day isn't.</summary>
        public static readonly int[] Days = { 0, 1, 7, 30 };

        /// <summary>Nothing is counted in the editor (tests, the Clip Lab, bot runs), on a temporary
        /// profile, or before the build has an address to send to.</summary>
        public static bool MaySend(string url, bool editor, bool temporaryProfile) =>
            !editor && !temporaryProfile && !string.IsNullOrEmpty(url);

        /// <summary>The app started or came back to the front. True when <paramref name="state"/> changed.</summary>
        public static bool Session(CounterState state, DateTime now, string platform, Action<CountEvent> send)
        {
            int today = (int)(now.Date.Ticks / TimeSpan.TicksPerDay);
            bool changed = false;
            if (state.installDay == 0)
            {
                state.installDay = today;
                send(new CountEvent { e = FirstStart, p = platform });
                changed = true;
            }
            if (today > state.lastDay) // once a day; a clock set back counts nothing
            {
                state.lastDay = today;
                int since = today - state.installDay;
                if (Array.IndexOf(Days, since) >= 0)
                    send(new CountEvent { e = Day, p = platform, since = since });
                changed = true;
            }
            return changed;
        }

        /// <summary>A run reached the finish. <paramref name="firstTutorial"/>: it was the Tutorial's first
        /// finish on this profile. True when <paramref name="state"/> changed.</summary>
        public static bool LevelFinished(CounterState state, string levelId, int stars, int players, int online, bool firstTutorial,
            string platform, Action<CountEvent> send)
        {
            bool changed = firstTutorial && !state.tutorial;
            if (changed)
            {
                state.tutorial = true;
                send(new CountEvent { e = TutorialDone, p = platform });
            }
            string level = LevelName(levelId);
            if (level != null)
                send(new CountEvent { e = LevelDone, p = platform, level = level, stars = stars, players = players, online = online });
            return changed;
        }

        /// <summary>A run's card was shared.</summary>
        public static void Shared(string platform, Action<CountEvent> send) => send(new CountEvent { e = Share, p = platform });

        /// <summary>A host shared the invite link to a room (docs/hit-plan.md §4.2). The room's code isn't in it.</summary>
        public static void Invited(string platform, Action<CountEvent> send) => send(new CountEvent { e = Invite, p = platform });

        /// <summary>The level as the counters name it: a campaign level's id, "tutorial", and one name for
        /// every daily and for every ranked day. Null for an id the server wouldn't take (a-z, 0-9
        /// and -, 24 at most), which then isn't counted.</summary>
        public static string LevelName(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            string name = id.StartsWith("daily-", StringComparison.Ordinal) ? "daily"
                : id.StartsWith("ranked-", StringComparison.Ordinal) ? "ranked"
                : id.ToLowerInvariant();
            if (name.Length > 24)
                return null;
            foreach (char c in name)
                if (!(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-'))
                    return null;
            return name;
        }
    }
}
