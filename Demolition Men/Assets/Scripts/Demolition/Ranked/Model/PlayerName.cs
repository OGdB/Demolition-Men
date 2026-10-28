using System;
using System.Text;

namespace Demolition.Ranked
{
    /// <summary>
    /// The name a player goes by on the ranked boards (docs/ranked.md §8): typed on the first screen,
    /// shown to everyone, so the game and the server hold it to the same rules. Upper case (the
    /// display font's), letters, digits, spaces and dashes, 3 to 16 of them, nothing on
    /// <see cref="Blocked"/>. Until one is typed a player is a generated WRECKER 1234.
    /// </summary>
    public static class PlayerName
    {
        public const int MinLength = 3, MaxLength = 16;

        private static readonly string[] Generated = { "WRECKER", "SMASHER", "BASHER", "CRUSHER", "DOZER", "HAMMER" };

        // Stems no name may hold, matched after undoing look-alikes (0 for O, 1 for I, ...) and
        // dropping what isn't a letter. Those inside ordinary words only as a whole word, so
        // SCUNTHORPE, SCRAPER, CATERING and PIKACHU pass.
        // shortcut: a starting list, English and Dutch; names get REPORT on the boards (K5) for the rest.
        private static readonly string[] Blocked =
        {
            "fuck", "shit", "nigger", "nigga", "faggot", "retard", "whore", "slut", "bitch", "penis",
            "vagina", "pussy", "nazi", "hitler", "kanker", "tyfus", "flikker", "mongool", "neuken",
        };

        private static readonly string[] BlockedWords =
        {
            "cunt", "rape", "fag", "kkk", "kut", "lul", "pik", "dick", "cock", "sex", "anal", "homo", "hoer", "tering",
        };

        /// <summary>One typed character as it goes in, or '\0' to drop it.</summary>
        public static char Accept(char c)
        {
            c = char.ToUpperInvariant(c);
            return (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == ' ' || c == '-' ? c : '\0';
        }

        /// <summary>The name as it would be kept: allowed characters only, single spaces, trimmed, at most <see cref="MaxLength"/>.</summary>
        public static string Clean(string raw)
        {
            var s = new StringBuilder();
            foreach (char typed in raw ?? string.Empty)
            {
                char c = Accept(typed);
                if (c == '\0' || (c == ' ' && (s.Length == 0 || s[s.Length - 1] == ' ')))
                    continue;
                s.Append(c);
            }
            string name = s.ToString().Trim();
            return name.Length > MaxLength ? name.Substring(0, MaxLength).TrimEnd() : name;
        }

        /// <summary>Good for the boards: clean already, long enough, and nothing on the list.</summary>
        public static bool Allowed(string name) =>
            name != null && Clean(name) == name && name.Length >= MinLength && !Offensive(name);

        public static bool Offensive(string name)
        {
            string flat = Flatten(name, keepSpaces: false);
            foreach (string stem in Blocked)
                if (flat.Contains(stem))
                    return true;
            foreach (string word in Flatten(name, keepSpaces: true).Split(' '))
                foreach (string blocked in BlockedWords)
                    if (word == blocked || word == blocked + "s")
                        return true;
            return false;
        }

        private static string Flatten(string name, bool keepSpaces)
        {
            var s = new StringBuilder();
            foreach (char c in (name ?? string.Empty).ToLowerInvariant())
            {
                char letter;
                switch (c)
                {
                    case '0': letter = 'o'; break;
                    case '1': letter = 'i'; break;
                    case '3': letter = 'e'; break;
                    case '4': letter = 'a'; break;
                    case '5': letter = 's'; break;
                    case '7': letter = 't'; break;
                    case '8': letter = 'b'; break;
                    default: letter = c; break;
                }
                if (letter >= 'a' && letter <= 'z')
                    s.Append(letter);
                else if (keepSpaces && (c == ' ' || c == '-') && s.Length > 0 && s[s.Length - 1] != ' ')
                    s.Append(' ');
            }
            return s.ToString();
        }

        /// <summary>The name a player has before typing one, the same every time for the same id.</summary>
        public static string Default(string playerId)
        {
            uint h = 2166136261;
            foreach (char c in playerId ?? string.Empty)
                h = (h ^ c) * 16777619;
            return $"{Generated[h % (uint)Generated.Length]} {1000 + h / (uint)Generated.Length % 9000}";
        }
    }
}
