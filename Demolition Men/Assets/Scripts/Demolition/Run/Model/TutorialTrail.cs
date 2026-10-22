using System.Collections.Generic;

namespace Demolition.Run
{
    /// <summary>
    /// Where the tutorial's signs stand. Each sign explains one stretch of the level, from
    /// its marker to the next. Once the front player reaches a marker, that sign is the
    /// current one and travels with them, kept inside its stretch; reaching the next marker
    /// leaves it standing at that boundary and the next sign takes over.
    /// </summary>
    public sealed class TutorialTrail
    {
        private readonly float[] _markers;

        /// <param name="markers">Marker x, left to right.</param>
        public TutorialTrail(IReadOnlyList<float> markers)
        {
            _markers = new float[markers.Count];
            for (int i = 0; i < _markers.Length; i++)
                _markers[i] = markers[i];
        }

        public int Count => _markers.Length;

        /// <summary>The sign of the furthest marker reached, -1 before the first. Never goes back.</summary>
        public int Current { get; private set; } = -1;

        /// <summary>The front player is at <paramref name="frontX"/>. True when another sign became
        /// current; a player fast enough to pass several markers at once skips to the last.</summary>
        public bool Reach(float frontX)
        {
            int reached = Current;
            while (reached + 1 < _markers.Length && frontX >= _markers[reached + 1])
                reached++;
            bool changed = reached != Current;
            Current = reached;
            return changed;
        }

        public bool IsShown(int sign) => sign <= Current;

        /// <summary>The boundary a passed sign stands at (the next marker), +infinity for the last sign.</summary>
        public float End(int sign) => sign + 1 < _markers.Length ? _markers[sign + 1] : float.PositiveInfinity;

        /// <summary>How wide a sign's stretch is, +infinity for the last.</summary>
        public float Width(int sign) => End(sign) - _markers[sign];

        /// <summary>Centre x of a sign reaching <paramref name="halfWidth"/> to each side: the
        /// current one under the front player, a passed one with its right edge on the next
        /// marker. Either way it stays inside its own stretch, or centred on it if the stretch
        /// is narrower than the sign.</summary>
        public float SignX(int sign, float frontX, float halfWidth)
        {
            float lo = _markers[sign] + halfWidth;
            float end = End(sign);
            float hi = end - halfWidth;
            if (lo > hi)
                return (_markers[sign] + end) * 0.5f;
            if (sign < Current)
                return hi;
            return frontX < lo ? lo : frontX > hi ? hi : frontX;
        }
    }
}
