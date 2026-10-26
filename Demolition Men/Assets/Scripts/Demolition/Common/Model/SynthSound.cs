using System;

namespace Demolition
{
    /// <summary>
    /// Destruction and UI sounds synthesised as sample arrays (no audio assets needed):
    /// filtered noise with a decay for crunches and rumbles, decaying partials for metal and
    /// chimes. Pure math; the view turns the samples into AudioClips.
    /// </summary>
    public static class SynthSound
    {
        public const int SampleRate = 22050;

        /// <summary>Short crunch of masonry or wood: noise through a low-pass, fast decay.
        /// <paramref name="brightness"/> 0..1 opens the filter (wood cracks brighter).</summary>
        public static float[] Crunch(float seconds, float brightness, int seed)
        {
            var rng = new Random(seed);
            int n = (int)(seconds * SampleRate);
            var data = new float[n];
            float lp = 0f, alpha = 0.05f + 0.6f * brightness;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += alpha * (noise - lp);
                float grit = rng.NextDouble() < 0.02 ? 0.6f : 1f; // little clicks of debris
                data[i] = lp * grit * Envelope(t, 0.01f, 5f) * 1.6f;
            }
            return Normalise(data, 0.8f);
        }

        /// <summary>Deep rumble of a collapse: brown noise with a slow swell and fade.</summary>
        public static float[] Rumble(float seconds, int seed)
        {
            var rng = new Random(seed);
            int n = (int)(seconds * SampleRate);
            var data = new float[n];
            float brown = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                brown = (brown + 0.02f * (float)(rng.NextDouble() * 2.0 - 1.0)) * 0.998f;
                lp += 0.08f * (brown - lp);
                data[i] = lp * Envelope(t, 0.08f, 2.2f);
            }
            return Normalise(data, 0.9f);
        }

        /// <summary>Metal hit: inharmonic decaying partials over a noise transient.</summary>
        public static float[] Clang(float seconds, float pitch, int seed)
        {
            var rng = new Random(seed);
            int n = (int)(seconds * SampleRate);
            var data = new float[n];
            float[] partials = { 1f, 2.76f, 5.4f, 8.93f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float tone = 0f;
                for (int p = 0; p < partials.Length; p++)
                    tone += (float)Math.Sin(2.0 * Math.PI * pitch * partials[p] * t) * (float)Math.Exp(-t * (6f + p * 5f)) / (p + 1);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * (float)Math.Exp(-t * 60f);
                data[i] = tone + noise * 0.5f;
            }
            return Normalise(data, 0.7f);
        }

        /// <summary>Bright bell for rewards (stars, coins): near-harmonic partials over a
        /// soft attack, ringing out. Keep <paramref name="pitch"/> under ~2.5 kHz: the top
        /// partial must stay below the 11 kHz Nyquist limit.</summary>
        public static float[] Chime(float seconds, float pitch)
        {
            int n = (int)(seconds * SampleRate);
            var data = new float[n];
            float[] partials = { 1f, 2f, 3f, 4.07f };
            float[] gains = { 1f, 0.5f, 0.3f, 0.2f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float tone = 0f;
                for (int p = 0; p < partials.Length; p++)
                    tone += (float)Math.Sin(2.0 * Math.PI * pitch * partials[p] * t) * gains[p] * (float)Math.Exp(-t * (3f + p * 3f) / seconds);
                data[i] = tone * Math.Min(1f, t / 0.004f);
            }
            return Normalise(data, 0.7f);
        }

        /// <summary>A long belch: a buzzing low pulse that wobbles, sags in pitch and gurgles
        /// out, with a breathy rasp over it.</summary>
        public static float[] Burp(float seconds, int seed)
        {
            var rng = new Random(seed);
            int n = (int)(seconds * SampleRate);
            var data = new float[n];
            double phase = 0.0;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float wobble = 1f + 0.12f * (float)Math.Sin(2.0 * Math.PI * 6.5 * t * seconds);
                float jitter = 1f + 0.06f * (float)(rng.NextDouble() * 2.0 - 1.0);
                phase += 82.0 * (1.0 - 0.3 * t) * wobble * jitter / SampleRate;
                float saw = (float)(2.0 * (phase - Math.Floor(phase)) - 1.0);
                float rasp = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.35f;
                lp += 0.18f * (saw + rasp - lp);
                float gurgle = 0.7f + 0.3f * (float)Math.Sin(2.0 * Math.PI * 23.0 * t * seconds);
                data[i] = lp * gurgle * Envelope(t, 0.06f, 1.4f);
            }
            return Normalise(data, 0.85f);
        }

        /// <summary>Two-finger whistle: a sine sweeping up to a held shriek, a little vibrato.</summary>
        public static float[] Whistle(float seconds)
        {
            int n = (int)(seconds * SampleRate);
            var data = new float[n];
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float sweep = t < 0.35f ? t / 0.35f : 1f;
                double freq = 1400.0 + 1100.0 * sweep + 40.0 * Math.Sin(2.0 * Math.PI * 9.0 * t * seconds);
                phase += freq / SampleRate;
                float fade = t > 0.85f ? (1f - t) / 0.15f : 1f;
                data[i] = (float)Math.Sin(2.0 * Math.PI * phase) * Math.Min(1f, t / 0.03f) * fade;
            }
            return Normalise(data, 0.6f);
        }

        // Quick attack, then exponential decay; t in 0..1.
        private static float Envelope(float t, float attack, float decay) =>
            (t < attack ? t / attack : 1f) * (float)Math.Exp(-decay * t);

        private static float[] Normalise(float[] data, float peak)
        {
            float max = 1e-6f;
            foreach (float v in data)
                max = Math.Max(max, Math.Abs(v));
            float k = peak / max;
            for (int i = 0; i < data.Length; i++)
                data[i] *= k;
            return data;
        }
    }
}
