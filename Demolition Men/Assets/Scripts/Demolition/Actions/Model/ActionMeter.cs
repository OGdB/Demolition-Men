using Demolition.Run;

namespace Demolition.Actions
{
    /// <summary>
    /// One player's action meter: demolition fills it, a streak keeps it. Every destroyed cell
    /// adds a point and a collapse pays its cells times the score's chain multiplier, so one
    /// weak point fills it faster than grinding bricks. A few seconds without breaking anything
    /// and it starts to drain, until it is full: a full meter waits for its button.
    /// </summary>
    public sealed class ActionMeter
    {
        /// <summary>Seconds without a point before the meter drains.</summary>
        public const float IdleGrace = 4f;
        /// <summary>Share of a full meter lost per second once it drains.</summary>
        public const float DrainPerSecond = 0.05f;

        private float _idle;

        public ActionMeter(float capacity) => Capacity = capacity > 0f ? capacity : 1f;

        public float Capacity { get; }
        public float Charge { get; private set; }
        public bool Full => Charge >= Capacity;
        public float Fraction => Charge / Capacity;

        /// <summary>Meter points for destruction: a destroyed block's cells, or a collapse's
        /// cells times <see cref="RunScore.ChainMultiplier"/>.</summary>
        public static float PointsFor(int cells, bool collapse) =>
            collapse ? cells * RunScore.ChainMultiplier(cells) : cells;

        public void Add(float points)
        {
            if (points <= 0f)
                return;
            Charge = System.Math.Min(Capacity, Charge + points);
            _idle = 0f;
        }

        public void Tick(float seconds)
        {
            if (Full)
                return;
            _idle += seconds;
            if (_idle > IdleGrace)
                Charge = System.Math.Max(0f, Charge - DrainPerSecond * Capacity * seconds);
        }

        /// <summary>Fire: true and empty when full, else false and unchanged.</summary>
        public bool TrySpend()
        {
            if (!Full)
                return false;
            Charge = 0f;
            _idle = 0f;
            return true;
        }

        /// <summary>A full meter for free (the tutorial's stretch about actions).</summary>
        public void Fill() => Charge = Capacity;

        /// <summary>Spent elsewhere: online, the other device's player fired theirs.</summary>
        public void Empty()
        {
            Charge = 0f;
            _idle = 0f;
        }
    }
}
