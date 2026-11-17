using UnityEngine;

namespace Demolition.Run
{
    /// <summary>One buzz for a player's hands: how hard (0..1) and for how long.</summary>
    public readonly struct Pulse
    {
        public readonly float Strength;
        public readonly float Seconds;

        public Pulse(float strength, float seconds)
        {
            Strength = Mathf.Clamp01(strength);
            Seconds = Mathf.Min(seconds, Haptics.MaxSeconds);
        }

        /// <summary>Too faint to feel: nothing is played.</summary>
        public bool IsNone => Strength < Haptics.Faintest || Seconds <= 0f;

        public int Milliseconds => Mathf.RoundToInt(Seconds * 1000f);

        /// <summary>Android's amplitude, 1..255.</summary>
        public int Amplitude => Mathf.Clamp(Mathf.RoundToInt(Strength * 255f), 1, 255);

        /// <summary>For a motor that is only on or off (a browser's, a phone without amplitude
        /// control): how hard becomes how long.</summary>
        public int OnOffMilliseconds => Mathf.Max(Haptics.ShortestOnOff, Mathf.RoundToInt(Seconds * Strength * 1000f));

        /// <summary>A gamepad's low-frequency (heavy) motor: off for ticks, so they stay ticks
        /// on the light one, which gets <see cref="Strength"/> as it is.</summary>
        public float Heavy => Mathf.InverseLerp(Haptics.HeavyFrom, 1f, Strength);
    }

    /// <summary>
    /// Feel, not rules (docs/hit-plan.md §5.1, F5): what a player's hands get for what happens
    /// in a run. A tick per hit that lands, a buzz that grows with the collapse, a thump for
    /// a blast nearby, a jolt for damage taken. <c>RunJuice</c> scales the camera's shake the
    /// same way. The numbers are a first guess to tune with a pad and a phone in the hand.
    /// </summary>
    public static class Haptics
    {
        /// <summary>A fist or a headbutt landing.</summary>
        public const float HitStrength = 0.4f;
        public const float HitSeconds = 0.05f;
        /// <summary>The axe or the wrecking ball landing.</summary>
        public const float WeaponStrength = 0.6f;
        public const float WeaponSeconds = 0.07f;

        /// <summary>Cells at which a collapse buzzes its hardest and longest (as <c>RunJuice</c>'s
        /// shake); more cells add nothing.</summary>
        public const int BigCollapse = 40;
        /// <summary>A collapse of one cell.</summary>
        public const float CollapseMinStrength = 0.5f;
        public const float CollapseMinSeconds = 0.12f;
        /// <summary>The longest any pulse lasts: a whole street coming down is still half a second.</summary>
        public const float MaxSeconds = 0.5f;
        /// <summary>How far off a collapse somebody else set off is still felt, fading to nothing
        /// there: half the view's width.</summary>
        public const float CollapseRange = ScrollEdge.ViewWidth * 0.5f;

        /// <summary>A blast at its centre, fading to nothing <see cref="BlastRange"/> away.</summary>
        public const float BlastStrength = 0.9f;
        public const float BlastSeconds = 0.25f;
        public const float BlastRange = 10f;

        /// <summary>Any damage taken jolts at least this hard.</summary>
        public const float HurtMinStrength = 0.7f;
        public const float HurtSeconds = 0.2f;
        /// <summary>Damage that jolts at full strength: a stick going off at the player's feet.</summary>
        public const float HurtFull = 45f;

        /// <summary>Weaker than this (a collapse or a blast far off) is not played at all.</summary>
        public const float Faintest = 0.1f;
        /// <summary>After a pulse the hands rest for this share of its length, at least <see cref="MinRest"/>.</summary>
        public const float RestShare = 0.5f;
        public const float MinRest = 0.08f;

        /// <summary>A gamepad's heavy motor joins in above this strength: above a fist's tick.</summary>
        public const float HeavyFrom = 0.45f;
        /// <summary>Milliseconds: the shortest buzz asked of a motor that is only on or off.</summary>
        public const int ShortestOnOff = 15;

        public static Pulse Hit(bool weapon) =>
            weapon ? new Pulse(WeaponStrength, WeaponSeconds) : new Pulse(HitStrength, HitSeconds);

        /// <param name="own">The player set it off: felt in full wherever it comes down.</param>
        /// <param name="distance">From the player, for a collapse that isn't theirs.</param>
        public static Pulse Collapse(int cells, bool own, float distance)
        {
            float size = Mathf.InverseLerp(1f, BigCollapse, cells);
            float near = own ? 1f : 1f - Mathf.Clamp01(distance / CollapseRange);
            return new Pulse(Mathf.Lerp(CollapseMinStrength, 1f, size) * near, Mathf.Lerp(CollapseMinSeconds, MaxSeconds, size));
        }

        public static Pulse Blast(float distance) =>
            new Pulse(BlastStrength * (1f - Mathf.Clamp01(distance / BlastRange)), BlastSeconds);

        public static Pulse Hurt(float damage) =>
            new Pulse(damage > 0f ? Mathf.Lerp(HurtMinStrength, 1f, damage / HurtFull) : 0f, HurtSeconds);
    }

    /// <summary>
    /// One player's hands. Pulses never add up: the strongest plays, and one no stronger is
    /// dropped while it does and for a rest after it. So hits in a row stay ticks instead of
    /// a hum, and a building that comes down in stages is felt in bursts.
    /// </summary>
    public sealed class HapticChannel
    {
        private float _strength;
        private float _until = float.NegativeInfinity;
        private float _restUntil = float.NegativeInfinity;

        /// <summary>How hard the hands buzz at <paramref name="now"/>; 0 when they don't.</summary>
        public float StrengthAt(float now) => now < _until ? _strength : 0f;

        /// <returns>True when the pulse plays, from <paramref name="now"/> and in place of what
        /// played; false when it was dropped.</returns>
        public bool Play(Pulse pulse, float now)
        {
            if (pulse.IsNone || (now < _restUntil && pulse.Strength <= _strength))
                return false;
            _strength = pulse.Strength;
            _until = now + pulse.Seconds;
            _restUntil = _until + Mathf.Max(Haptics.MinRest, pulse.Seconds * Haptics.RestShare);
            return true;
        }

        public void Stop()
        {
            _strength = 0f;
            _until = _restUntil = float.NegativeInfinity;
        }
    }
}
