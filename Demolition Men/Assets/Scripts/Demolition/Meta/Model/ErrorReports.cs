using System;
using System.Collections.Generic;
using System.Text;

namespace Demolition.Meta
{
    /// <summary>
    /// One C# exception for our own server (server/relay's /error). These fields are all of it, and
    /// the relay's Errors.cs takes exactly these: no id of the device, the install or the player.
    /// </summary>
    [Serializable]
    public sealed class ErrorReport
    {
        /// <summary>The game's version (<c>Application.version</c>).</summary>
        public string v;
        /// <summary>What the game runs on: web, android, pc or other.</summary>
        public string p;
        /// <summary>The device's model (<c>SystemInfo.deviceModel</c>), for bugs one kind of phone has.</summary>
        public string device;
        /// <summary>The exception's type and message.</summary>
        public string msg;
        /// <summary>Where it was thrown.</summary>
        public string stack;
    }

    /// <summary>
    /// Which exceptions are reported and what of them goes out. Unity catches a C# exception, logs it and
    /// plays on, so Android vitals never sees it; this does. Each kind (its type and stack trace) is sent
    /// once per session and at most <see cref="MaxPerSession"/> kinds, so a bug in Update can't send one a
    /// frame. The device's own folders come out of the text first: a path can hold the user's name.
    /// </summary>
    public sealed class ErrorReports
    {
        /// <summary>The relay's caps (server/relay/Relay/Errors.cs): longer is cut, not refused.</summary>
        public const int MaxMessage = 500, MaxStack = 4000, MaxDevice = 64;
        public const int MaxPerSession = 5;
        public const string PathMark = "<path>";

        private readonly HashSet<string> _seen = new HashSet<string>();

        /// <summary>The report for an exception Unity logged, or null when it isn't sent: one of this kind
        /// went already, the session's are used up, or there is no message.</summary>
        /// <param name="privatePaths">The device's folders to take out (save folder, home, ...); null or empty ones are skipped.</param>
        public ErrorReport Take(string condition, string stackTrace, string version, string platform, string device, string[] privatePaths)
        {
            string msg = Cut(Clean(Scrub(condition, privatePaths), true), MaxMessage);
            if (msg.Trim().Length == 0 || _seen.Count >= MaxPerSession)
                return null;
            string stack = Cut(Clean(Scrub(stackTrace, privatePaths), true), MaxStack);
            if (!_seen.Add(Kind(msg) + "\n" + stack))
                return null;
            return new ErrorReport
            {
                v = version, p = platform, device = Cut(Clean(device, false), MaxDevice), msg = msg, stack = stack,
            };
        }

        /// <summary>The exception's type: its message up to the first colon, as the relay groups them.</summary>
        public static string Kind(string msg)
        {
            int colon = msg.IndexOf(':');
            return colon > 0 ? msg.Substring(0, colon) : msg;
        }

        /// <summary>Every folder in <paramref name="paths"/>, written with / or \, in any case, as <see cref="PathMark"/>; the longest first,
        /// so a save folder inside the home folder goes as a whole.</summary>
        public static string Scrub(string text, string[] paths)
        {
            if (string.IsNullOrEmpty(text) || paths == null)
                return text ?? string.Empty;
            var sorted = new List<string>();
            foreach (string path in paths)
                if (!string.IsNullOrEmpty(path) && path.Trim('/', '\\').Length > 0)
                    sorted.Add(path.TrimEnd('/', '\\'));
            sorted.Sort((a, b) => b.Length.CompareTo(a.Length));
            foreach (string path in sorted)
            {
                text = text.Replace(path.Replace('\\', '/'), PathMark, StringComparison.OrdinalIgnoreCase);
                text = text.Replace(path.Replace('/', '\\'), PathMark, StringComparison.OrdinalIgnoreCase);
            }
            return text;
        }

        // Control characters (the relay refuses them) become spaces; line breaks stay where they may.
        private static string Clean(string text, bool lines)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            var s = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (lines && c == '\r')
                    continue; // Windows line ends: the \n stays
                s.Append(c >= ' ' || (lines && (c == '\n' || c == '\t')) ? c : ' ');
            }
            return s.ToString();
        }

        private static string Cut(string text, int max) => text.Length <= max ? text : text.Substring(0, max);
    }
}
