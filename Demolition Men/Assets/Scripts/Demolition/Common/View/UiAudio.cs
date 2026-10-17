using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// Menu and results sounds: metal ticks and clanks for buttons, slams for titles, a
    /// rising chime per star. Clips are synthesised once (<see cref="SynthSound"/>) and play
    /// on a few persistent 2D voices that ignore the pause, since menus open over a paused run.
    /// </summary>
    public static class UiAudio
    {
        private const int Voices = 6;
        private const float Volume = 0.55f;

        private static AudioSource[] _voices;
        private static int _next;
        private static float _lastTick = -1f;
        private static AudioClip _tick, _clank, _chime, _crunch, _rumble, _thud;

        /// <summary>A button was reached (selected or hovered); also the meter's count-up.</summary>
        public static void Tick(float pitch = 1f)
        {
            if (Time.unscaledTime - _lastTick < 0.03f)
                return; // fast navigation or a fast meter: one tick per 30 ms
            _lastTick = Time.unscaledTime;
            Play(_tick, pitch, 0.3f);
        }

        public static void Click() => Play(_clank, 1f, 0.55f);

        /// <summary>Something heavy lands: a title, a stamp.</summary>
        public static void Slam()
        {
            Play(_crunch, 0.75f, 0.9f);
            Play(_rumble, 1.1f, 0.8f);
        }

        /// <summary>Star <paramref name="index"/> (0 to 2) lit: a rising major arpeggio over a
        /// crunch, the last one with a rumble.</summary>
        public static void Star(int index)
        {
            float[] steps = { 1f, 1.26f, 1.5f };
            Play(_chime, steps[Mathf.Clamp(index, 0, steps.Length - 1)], 0.8f);
            Play(_crunch, 1f + index * 0.15f, 0.6f);
            if (index >= 2)
                Play(_rumble, 0.9f, 0.7f);
        }

        public static void Coin() => Play(_chime, 2f, 0.25f);

        /// <summary>A run that ended without finishing.</summary>
        public static void Fail() => Play(_thud, 1f, 0.8f);

        private static void Play(AudioClip clip, float pitch, float loudness)
        {
            if (!Ready())
                return;
            AudioSource voice = _voices[_next];
            _next = (_next + 1) % Voices;
            voice.pitch = pitch;
            voice.PlayOneShot(clip, Volume * loudness);
        }

        private static bool Ready()
        {
            if (!Application.isPlaying)
                return false;
            if (_tick == null)
            {
                _tick = Clip("UiTick", SynthSound.Clang(0.06f, 900f, 11));
                _clank = Clip("UiClank", SynthSound.Clang(0.2f, 360f, 12));
                _chime = Clip("UiChime", SynthSound.Chime(1.1f, 880f));
                _crunch = Clip("UiCrunch", SynthSound.Crunch(0.35f, 0.25f, 13));
                _rumble = Clip("UiRumble", SynthSound.Rumble(1.2f, 14));
                _thud = Clip("UiThud", SynthSound.Clang(0.6f, 110f, 15));
            }
            if (_voices == null || _voices[0] == null)
            {
                var go = new GameObject("UiAudio");
                Object.DontDestroyOnLoad(go);
                _voices = new AudioSource[Voices];
                for (int i = 0; i < Voices; i++)
                {
                    _voices[i] = go.AddComponent<AudioSource>();
                    _voices[i].playOnAwake = false;
                    _voices[i].ignoreListenerPause = true;
                }
            }
            return true;
        }

        private static AudioClip Clip(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SynthSound.SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
