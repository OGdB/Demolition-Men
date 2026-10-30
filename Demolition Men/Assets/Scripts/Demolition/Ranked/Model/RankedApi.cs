using System;

namespace Demolition.Ranked
{
    // The ranked server's messages (docs/ranked.md §2, server/ranked), as JSON both ways: the game
    // reads and writes them with JsonUtility, the server with System.Text.Json, so they are public
    // fields with these exact names. JsonUtility has no null for these: an empty one is "none".

    /// <summary>POST /start: a new attempt at today's level.</summary>
    [Serializable]
    public sealed class StartRequest
    {
        /// <summary>The device's id (PlayerIdentity) and the name it goes by.</summary>
        public string player;
        public string name;
        /// <summary>"solo" or "coop".</summary>
        public string mode;
        /// <summary>Online co-op: the other device's id and name; empty otherwise.</summary>
        public string partner;
        public string partnerName;
        /// <summary>The build's RankedRules.Version.</summary>
        public int rules;
    }

    [Serializable]
    public sealed class StartResponse
    {
        public string attempt;
        /// <summary>The attempt's sealing key, base64.</summary>
        public string key;
        public int day;
        public string template;
        public int seed;
        public string mode;
    }

    /// <summary>POST /submit: the finished (or failed) run's sealed log, base64.</summary>
    [Serializable]
    public sealed class SubmitRequest
    {
        public string attempt;
        public string log;
    }

    [Serializable]
    public sealed class SubmitResponse
    {
        /// <summary>Place on the day's board, 0 for a run that didn't finish.</summary>
        public int rank;
        /// <summary>Entries on the board.</summary>
        public int of;
        public int score;
        /// <summary>A best of the day for this player (an earlier run stays if it was better).</summary>
        public bool best;
    }

    [Serializable]
    public sealed class BoardEntry
    {
        /// <summary>The run, for REPORT and the ghost; 0 for none.</summary>
        public int run;
        public int rank;
        public string name;
        /// <summary>Co-op: the partner's name; empty for a local crew.</summary>
        public string partner;
        public int score;
        public int timeMs;
        public int stars;
    }

    /// <summary>GET /board?day&amp;mode&amp;player: the day's top runs and the player's own.</summary>
    [Serializable]
    public sealed class Board
    {
        public int day;
        public string template;
        public int seed;
        public string mode;
        /// <summary>Entries on the whole board.</summary>
        public int players;
        public BoardEntry[] entries = new BoardEntry[0];
        /// <summary>The asking player's best, or rank 0.</summary>
        public BoardEntry you = new BoardEntry();
    }

    /// <summary>POST /report: a player flags a run.</summary>
    [Serializable]
    public sealed class ReportRequest
    {
        public string player;
        public int run;
    }

    /// <summary>GET /ghost?day&amp;mode: the best run's log (unsealed, base64), to race against.</summary>
    [Serializable]
    public sealed class Ghost
    {
        public int run;
        public string name;
        public int score;
        public string log;
    }

    public static class RankedApi
    {
        public const string Solo = "solo", Coop = "coop";

        public static string ModeName(RankedMode mode) => mode == RankedMode.Coop ? Coop : Solo;

        public static bool TryMode(string name, out RankedMode mode)
        {
            mode = name == Coop ? RankedMode.Coop : RankedMode.Solo;
            return name == Coop || name == Solo;
        }

        /// <summary>A player id as PlayerIdentity makes it: 32 lower-case hex characters.</summary>
        public static bool IsPlayerId(string id)
        {
            if (id == null || id.Length != 32)
                return false;
            foreach (char c in id)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    return false;
            return true;
        }
    }
}
