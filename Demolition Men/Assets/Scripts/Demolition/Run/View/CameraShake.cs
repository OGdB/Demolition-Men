using UnityEngine;

namespace Demolition.Run
{
    /// <summary>
    /// Screen shake as "trauma" (0..1) that decays; the offset grows with trauma squared,
    /// so small knocks stay subtle and big collapses really rumble. Anyone can add trauma;
    /// <see cref="ScrollCamera"/> applies the offset. Off when the settings say so.
    /// </summary>
    public static class CameraShake
    {
        private const float MaxOffset = 0.6f;
        private const float Decay = 1.4f;
        private const float Frequency = 22f;

        public static bool Enabled = true;
        private static float _trauma;

        public static void Add(float trauma) => _trauma = Mathf.Clamp01(_trauma + trauma);

        /// <summary>This frame's offset; call once per frame.</summary>
        public static Vector3 Tick(float dt)
        {
            if (dt <= 0f)
                return Vector3.zero; // paused: hold still, keep the trauma for later
            _trauma = Mathf.Max(0f, _trauma - Decay * dt);
            if (!Enabled || _trauma <= 0f)
                return Vector3.zero;
            float amount = _trauma * _trauma * MaxOffset;
            float t = Time.unscaledTime * Frequency;
            return new Vector3((Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * amount,
                               (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * amount, 0f);
        }

        public static void Clear() => _trauma = 0f;
    }
}
