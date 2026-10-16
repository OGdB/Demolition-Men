using System;

namespace Demolition.Generation
{
    /// <summary>
    /// PCG32 random numbers: the same seed gives the same sequence on every machine and
    /// runtime, unlike <c>UnityEngine.Random</c> or <c>System.Random</c>. Generated
    /// content (and later netcode) depends on that.
    /// </summary>
    public sealed class Pcg32
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const ulong Increment = 1442695040888963407UL;
        private ulong _state;

        public Pcg32(int seed)
        {
            Next();
            _state += (ulong)(uint)seed;
            Next();
        }

        public uint Next()
        {
            ulong old = _state;
            _state = old * Multiplier + Increment;
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rotation = (int)(old >> 59);
            return (xorShifted >> rotation) | (xorShifted << (-rotation & 31));
        }

        /// <summary>Uniform int in [min, max], both inclusive.</summary>
        public int Range(int min, int max)
        {
            if (max <= min)
                return min;
            return min + (int)(Next() % (uint)(max - min + 1));
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float Value() => Next() / 4294967296f;

        public bool Chance(float probability) => Value() < probability;
    }

    /// <summary>Inclusive int range as written in archetype JSON: <c>{"min": 2, "max": 4}</c>.</summary>
    [Serializable]
    public struct IntRange
    {
        public int min;
        public int max;

        public IntRange(int min, int max)
        {
            this.min = min;
            this.max = max;
        }

        public int Pick(Pcg32 rng) => rng.Range(min, Math.Max(min, max));
        public bool Contains(int value) => value >= min && value <= max;
    }
}
