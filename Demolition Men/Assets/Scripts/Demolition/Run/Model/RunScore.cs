using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Run
{
    public enum Grade { C, B, A, S }

    /// <summary>
    /// Scoring rules for one run (roadmap M1, decision D3). Destroyed and collapsed
    /// cells pay their material's points; one structural event that drops many cells
    /// pays a chain multiplier, so hitting a weak point beats grinding bricks. Finishing
    /// under par multiplies the total and turns the stars rainbow.
    /// </summary>
    public sealed class RunScore
    {
        public const float ChainStepPerCell = 1f / 20f;
        public const float MaxChainMultiplier = 3f;
        public const float MaxSpeedMultiplier = 1.5f;
        /// <summary>Fraction of par at (or below) which the full speed bonus applies.</summary>
        public const float FullSpeedBonusAt = 0.6f;
        /// <summary>Level-wide destruction needed for each grade.</summary>
        public const float GradeB = 0.4f, GradeA = 0.6f, GradeS = 0.8f;

        private readonly int[] _points;

        public RunScore(int playerCount)
        {
            _points = new int[Mathf.Max(1, playerCount)];
        }

        public int PlayerCount => _points.Length;
        public int Points(int player) => _points[player];
        /// <summary>Points no player caused.</summary>
        public int Unattributed { get; private set; }

        public int Total
        {
            get
            {
                int sum = Unattributed;
                for (int i = 0; i < _points.Length; i++)
                    sum += _points[i];
                return sum;
            }
        }

        /// <returns>Points awarded.</returns>
        public int AddDestroyed(int player, BlockMaterial material, int cells)
        {
            int points = BlockMaterialInfo.Points(material) * Mathf.Max(1, cells);
            Credit(player, points);
            return points;
        }

        /// <returns>Points awarded.</returns>
        public int AddCollapse(int player, IReadOnlyList<BlockMaterial> cells)
        {
            int basePoints = 0;
            for (int i = 0; i < cells.Count; i++)
                basePoints += BlockMaterialInfo.Points(cells[i]);
            int points = Mathf.RoundToInt(basePoints * ChainMultiplier(cells.Count));
            Credit(player, points);
            return points;
        }

        /// <summary>Online client: take the host's points.</summary>
        public void Restore(int[] points, int unattributed)
        {
            for (int i = 0; i < _points.Length && i < points.Length; i++)
                _points[i] = points[i];
            Unattributed = unattributed;
        }

        public void ApplyPenalty(int player, int amount)
        {
            if (IsPlayer(player))
                _points[player] = Mathf.Max(0, _points[player] - amount);
        }

        public int FinalTotal(float time, float parTime) =>
            Mathf.RoundToInt(Total * SpeedMultiplier(time, parTime));

        public static float ChainMultiplier(int cells) =>
            Mathf.Min(MaxChainMultiplier, 1f + cells * ChainStepPerCell);

        /// <summary>×1.5 at or below 60% of par, easing to ×1 at par; never below ×1.</summary>
        public static float SpeedMultiplier(float time, float parTime)
        {
            if (parTime <= 0f)
                return 1f;
            float t = Mathf.InverseLerp(FullSpeedBonusAt, 1f, time / parTime);
            return Mathf.Lerp(MaxSpeedMultiplier, 1f, t);
        }

        public static Grade GradeFor(float destroyedFraction)
        {
            if (destroyedFraction >= GradeS) return Grade.S;
            if (destroyedFraction >= GradeA) return Grade.A;
            if (destroyedFraction >= GradeB) return Grade.B;
            return Grade.C;
        }

        /// <summary>A finish within par turns the stars rainbow. No par means no time limit.</summary>
        public static bool WithinPar(float time, float parTime) => parTime <= 0f || time <= parTime;

        /// <summary>Players see a grade as stars: one per grade above C.</summary>
        public const int MaxStars = 3;

        public static int Stars(Grade grade) => (int)grade;

        /// <summary>Destruction that earns star <paramref name="star"/> (0-based).</summary>
        public static float StarThreshold(int star) => star <= 0 ? GradeB : star == 1 ? GradeA : GradeS;

        private bool IsPlayer(int player) => player >= 0 && player < _points.Length;

        private void Credit(int player, int points)
        {
            if (IsPlayer(player))
                _points[player] += points;
            else
                Unattributed += points;
        }
    }
}
