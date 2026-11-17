using System;

namespace Demolition.Online
{
    /// <summary>A room's code as the relay hands them out (server/relay RelayServer.cs, which has
    /// the same alphabet): 6 characters, without the look-alikes 0/O and 1/I.</summary>
    public static class RoomCode
    {
        public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        public const int Length = 6;

        /// <summary>The code in capitals, or null when <paramref name="text"/> isn't one.</summary>
        public static string Clean(string text)
        {
            if (text == null || text.Length != Length)
                return null;
            var code = new char[Length];
            for (int i = 0; i < Length; i++)
            {
                char c = text[i];
                if (c >= 'a' && c <= 'z')
                    c = (char)(c - ('a' - 'A')); // ASCII only: no other letter may turn into one of the alphabet's
                if (Alphabet.IndexOf(c) < 0)
                    return null;
                code[i] = c;
            }
            return new string(code);
        }
    }

    /// <summary>
    /// The invite link (docs/hit-plan.md §4.2): <c>&lt;play URL&gt;/&lt;version&gt;/?r=ABCDEF</c>, e.g.
    /// <c>https://play.example.org/1.0.0/?r=ABCDEF</c>. A host shares it; the browser build that
    /// opens with it joins the room. The version is the one a host holds a joiner to, and each
    /// version's browser build has its own folder, so a phone on an older build still sends its
    /// friend to a build it lets in. Anyone can write a link, so reading one trusts nothing but
    /// a well-formed room code.
    /// </summary>
    public static class InviteLink
    {
        private const string Key = "r=";
        private const int MaxVersionLength = 32;

        /// <summary>Where the browser build of <paramref name="version"/> is served, ending in a
        /// slash. Null when there is nowhere to send anyone: no play URL (it must start with
        /// http:// or https:// and have no query), or a version that can't be a folder's name.</summary>
        public static string Page(string playUrl, string version)
        {
            playUrl = Base(playUrl);
            return playUrl != null && IsFolderName(version) ? playUrl + "/" + version + "/" : null;
        }

        /// <summary>The link to <paramref name="code"/>'s room, or null without a <see cref="Page"/> or a real code.</summary>
        public static string Build(string playUrl, string version, string code)
        {
            string page = Page(playUrl, version);
            code = RoomCode.Clean(code);
            return page != null && code != null ? page + "?" + Key + code : null;
        }

        /// <summary>The room code in the address a browser build was opened with
        /// (<c>Application.absoluteURL</c>): the query's first <c>r</c>, if it is a room code. Null otherwise.</summary>
        public static string Code(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;
            int fragment = url.IndexOf('#');
            if (fragment >= 0)
                url = url.Substring(0, fragment);
            int query = url.IndexOf('?');
            if (query < 0)
                return null;
            foreach (string pair in url.Substring(query + 1).Split('&'))
                if (pair.StartsWith(Key, StringComparison.Ordinal))
                    return RoomCode.Clean(pair.Substring(Key.Length));
            return null;
        }

        /// <summary>The page at <paramref name="pageUrl"/> is served from the play URL: the owner's
        /// own copy of the browser build, not a portal's.</summary>
        public static bool IsOwnCopy(string pageUrl, string playUrl)
        {
            playUrl = Base(playUrl);
            if (playUrl == null || pageUrl == null || !pageUrl.StartsWith(playUrl, StringComparison.OrdinalIgnoreCase))
                return false;
            // The whole host, not its start: play.example.org is not play.example.org.elsewhere.net.
            return pageUrl.Length == playUrl.Length || "/?#".IndexOf(pageUrl[playUrl.Length]) >= 0;
        }

        // The play URL without its last slash; null when a link built on it wouldn't be one.
        private static string Base(string playUrl)
        {
            playUrl = playUrl?.Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(playUrl) || playUrl.IndexOfAny(new[] { '?', '#', ' ' }) >= 0)
                return null;
            bool web = playUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || playUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
            return web ? playUrl : null;
        }

        // Letters, digits, dots, dashes and underscores, and not dots alone: "." and ".." are no folders.
        private static bool IsFolderName(string version)
        {
            if (string.IsNullOrEmpty(version) || version.Length > MaxVersionLength)
                return false;
            bool named = false;
            foreach (char c in version)
            {
                bool alphanumeric = c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9';
                if (!alphanumeric && c != '.' && c != '-' && c != '_')
                    return false;
                named |= alphanumeric;
            }
            return named;
        }
    }
}
