using System;
using System.Collections.Generic;
using Demolition.Generation;

namespace Demolition.Run
{
    /// <summary>A bonus objective on a level's job sheet. Every one needs the level finished.</summary>
    public enum ObjectiveKind { Wreck, Chain, FistsOnly, NoDynamite, NoDamage, BeatPar }

    /// <summary>
    /// What a run did that a job sheet asks about, for the whole crew. Offline and on the host
    /// the run feeds it; an online client takes the host's (<see cref="Restore"/>).
    /// </summary>
    public sealed class RunTally
    {
        private readonly int[] _dynamite;
        private readonly float[] _health;

        public RunTally(int players)
        {
            _dynamite = new int[Math.Max(1, players)];
            _health = new float[_dynamite.Length];
            for (int i = 0; i < _dynamite.Length; i++)
            {
                _dynamite[i] = -1;
                _health[i] = -1f;
            }
        }

        /// <summary>Someone held a picked-up weapon.</summary>
        public bool Armed { get; private set; }
        /// <summary>Someone threw dynamite.</summary>
        public bool Dynamite { get; private set; }
        /// <summary>Someone lost health.</summary>
        public bool Hurt { get; private set; }
        /// <summary>Most cells one collapse brought down, counting the crumbles that follow it within
        /// <see cref="ChainWindow"/> (one building coming down, as the player sees it).</summary>
        public int BiggestCollapse { get; private set; }

        /// <summary>Seconds between collapses that still count as one: the validator's crumble window.</summary>
        public const float ChainWindow = BuildingValidator.CrumbleWindowSeconds;

        private int _chain;
        private float _lastCollapse = float.NegativeInfinity;

        /// <summary>A player in play this frame: the weapon in hand, sticks carried, health. Fewer
        /// sticks than last time is a throw, less health a hit (a revive only ever adds).</summary>
        public void Observe(int player, bool armed, int dynamite, float health)
        {
            if (player < 0 || player >= _dynamite.Length)
                return;
            Armed |= armed;
            Dynamite |= _dynamite[player] >= 0 && dynamite < _dynamite[player];
            Hurt |= _health[player] >= 0f && health < _health[player];
            _dynamite[player] = dynamite;
            _health[player] = health;
        }

        public void Collapsed(int cells, float time)
        {
            _chain = time - _lastCollapse <= ChainWindow ? _chain + cells : cells;
            _lastCollapse = time;
            BiggestCollapse = Math.Max(BiggestCollapse, _chain);
        }

        public byte Flags => (byte)((Armed ? 1 : 0) | (Dynamite ? 2 : 0) | (Hurt ? 4 : 0));

        /// <summary>Online client: the host's tally.</summary>
        public void Restore(byte flags, int biggestCollapse)
        {
            Armed = (flags & 1) != 0;
            Dynamite = (flags & 2) != 0;
            Hurt = (flags & 4) != 0;
            BiggestCollapse = biggestCollapse;
        }
    }

    /// <summary>
    /// Job sheets (roadmap v1.2, Bonus objectives): three objectives per level on top of its
    /// stars, the same every time for the same level (picked from its seed, or pinned in its
    /// spec). Each one done pays <see cref="CashEach"/> once.
    /// </summary>
    public static class Objectives
    {
        public const int PerSheet = 3;
        public const int CashEach = 250;
        public const float WreckShare = 0.9f;
        /// <summary>Cells one collapse (with its crumbles, <see cref="RunTally.ChainWindow"/>) must bring
        /// down. LevelGenerationTests checks every campaign level has a building that can.</summary>
        public const int ChainCells = 12;
        public const int AllDone = (1 << PerSheet) - 1;

        private static readonly ObjectiveKind[] Kinds = (ObjectiveKind[])Enum.GetValues(typeof(ObjectiveKind));

        /// <summary>The sheet of a level: its pinned objectives first (ids as in <see cref="Id"/>),
        /// then picks from <paramref name="seed"/> until there are <see cref="PerSheet"/>.</summary>
        public static ObjectiveKind[] Sheet(int seed, IReadOnlyList<string> pinned = null)
        {
            var sheet = new List<ObjectiveKind>();
            if (pinned != null)
                foreach (string id in pinned)
                    foreach (ObjectiveKind kind in Kinds)
                        if (Id(kind) == id && !sheet.Contains(kind) && sheet.Count < PerSheet)
                            sheet.Add(kind);
            var pool = new List<ObjectiveKind>(Kinds);
            pool.RemoveAll(sheet.Contains);
            var rng = new Pcg32(unchecked(seed * 31 + 0x0B1E)); // its own stream: the level's isn't touched
            while (sheet.Count < PerSheet && pool.Count > 0)
            {
                int pick = rng.Range(0, pool.Count - 1);
                sheet.Add(pool[pick]);
                pool.RemoveAt(pick);
            }
            return sheet.ToArray();
        }

        /// <summary>The id a level spec pins it by: "wreck", "chain", "fistsonly", ...</summary>
        public static string Id(ObjectiveKind kind) => kind.ToString().ToLowerInvariant();

        public static string Text(ObjectiveKind kind)
        {
            switch (kind)
            {
                case ObjectiveKind.Wreck: return $"WRECK {Math.Round(WreckShare * 100f)}%";
                case ObjectiveKind.Chain: return $"{ChainCells}+ BLOCKS IN ONE COLLAPSE";
                case ObjectiveKind.FistsOnly: return "FISTS ONLY";
                case ObjectiveKind.NoDynamite: return "NO DYNAMITE";
                case ObjectiveKind.NoDamage: return "NOT A SCRATCH";
                default: return "BEAT PAR";
            }
        }

        public static bool Met(ObjectiveKind kind, RunTally tally, bool finished, float destroyed, bool withinPar)
        {
            if (!finished)
                return false;
            switch (kind)
            {
                case ObjectiveKind.Wreck: return destroyed >= WreckShare;
                case ObjectiveKind.Chain: return tally.BiggestCollapse >= ChainCells;
                case ObjectiveKind.FistsOnly: return !tally.Armed;
                case ObjectiveKind.NoDynamite: return !tally.Dynamite;
                case ObjectiveKind.NoDamage: return !tally.Hurt;
                default: return withinPar;
            }
        }

        /// <summary>Bit i set: the sheet's objective i was met.</summary>
        public static int MetMask(IReadOnlyList<ObjectiveKind> sheet, RunTally tally, bool finished, float destroyed, bool withinPar)
        {
            int mask = 0;
            for (int i = 0; sheet != null && i < sheet.Count && i < PerSheet; i++)
                if (Met(sheet[i], tally, finished, destroyed, withinPar))
                    mask |= 1 << i;
            return mask;
        }

        /// <summary>Bit i set: this run can no longer meet the sheet's objective i (a weapon was
        /// held, dynamite thrown, damage taken), whatever happens next.</summary>
        public static int BlownMask(IReadOnlyList<ObjectiveKind> sheet, RunTally tally)
        {
            int mask = 0;
            for (int i = 0; sheet != null && i < sheet.Count && i < PerSheet; i++)
            {
                ObjectiveKind kind = sheet[i];
                if ((kind == ObjectiveKind.FistsOnly && tally.Armed) || (kind == ObjectiveKind.NoDynamite && tally.Dynamite)
                    || (kind == ObjectiveKind.NoDamage && tally.Hurt))
                    mask |= 1 << i;
            }
            return mask;
        }

        public static int Count(int mask)
        {
            int count = 0;
            for (int bits = mask & AllDone; bits != 0; bits &= bits - 1)
                count++;
            return count;
        }
    }
}
