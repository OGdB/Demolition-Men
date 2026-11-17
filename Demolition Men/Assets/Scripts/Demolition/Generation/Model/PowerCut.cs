using UnityEngine;

namespace Demolition.Generation
{
    /// <summary>
    /// When a building's power fails and in what order its lights go out. It fails once the
    /// building is coming down: one collapse big enough, or enough of it gone bit by bit. The
    /// fault then runs out from the break through the wiring, so the lamps nearest it die first
    /// and the far end of the building a moment later, each a little off the beat and some
    /// after a flicker: never all in one frame, all within <see cref="MaxSeconds"/>.
    /// </summary>
    public static class PowerCut
    {
        /// <summary>Share of the building gone at which the power fails whatever took it
        /// (the rubble card's "half down").</summary>
        public const float GoneShare = 0.4f;
        /// <summary>One collapse of this share of the building cuts the power at once...</summary>
        public const float CollapseShare = 0.15f;
        /// <summary>...if it is at least this many cells (a kiosk losing two blocks isn't one).</summary>
        public const int MinCollapse = 6;
        public const float SecondsPerCell = 0.05f;
        public const float Jitter = 0.12f;
        public const float MaxSeconds = 0.9f;
        public const int MaxFlickers = 2;

        /// <summary>True when a building of <paramref name="initial"/> cells, <paramref name="gone"/>
        /// of them lost by now and <paramref name="batch"/> of those just now, loses its power.</summary>
        public static bool Trips(int initial, int gone, int batch) =>
            initial > 0 && (gone >= initial * GoneShare - Epsilon || (batch >= MinCollapse && batch >= initial * CollapseShare - Epsilon));

        // 15 of 100 cells is 15%, whatever a float makes of 0.15 x 100.
        private const float Epsilon = 1e-3f;

        /// <summary>Seconds after the cut at which the lamp at <paramref name="lamp"/> dies, the
        /// break being at <paramref name="from"/> (both in cells).</summary>
        public static float Delay(Vector2 lamp, Vector2 from, uint hash) =>
            Mathf.Min(MaxSeconds, Vector2.Distance(lamp, from) * SecondsPerCell + (hash & 0xFF) / 255f * Jitter);

        /// <summary>How often a lamp comes back on before it stays off.</summary>
        public static int Flickers(uint hash) => (int)((hash >> 8) % (MaxFlickers + 1));

        /// <summary>A lamp's own number: the same building fails the same way on every device.</summary>
        public static uint Hash(int seed, int lamp)
        {
            unchecked
            {
                uint h = (uint)seed * 73856093u ^ (uint)lamp * 19349663u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                return h ^ (h >> 15);
            }
        }
    }
}
