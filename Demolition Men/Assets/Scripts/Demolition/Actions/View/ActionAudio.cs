using UnityEngine;

namespace Demolition.Actions
{
    /// <summary>The actions' sounds, synthesised once (<see cref="SynthSound"/>): the burp, the
    /// whistle, the countdown beeps, the slam, the slurp and the radio's notes. A few 2D voices
    /// that pause with the game.</summary>
    public static class ActionAudio
    {
        private const int Voices = 6;
        private const float Volume = 0.7f;

        // A major pentatonic: the radio plays a tune of these.
        private static readonly float[] Tune = { 1f, 1.125f, 1.25f, 1.5f, 1.6875f, 2f };

        private static AudioSource[] _voices;
        private static int _next;
        private static AudioClip _burp, _whistle, _beep, _slam, _slurp, _note, _clank;

        public static void Burp() => Play(_burp, Random.Range(0.92f, 1.05f), 1f);
        public static void Whistle() => Play(_whistle, 1f, 0.8f);
        public static void Beep(int count) => Play(_beep, count > 0 ? 1f : 1.5f, 0.7f);
        public static void Slam() => Play(_slam, 0.8f, 1f);
        public static void Slurp() => Play(_slurp, 1.6f, 0.6f);
        public static void Clank() => Play(_clank, 1f, 0.7f);
        /// <summary>Note <paramref name="step"/> of the radio's tune.</summary>
        public static void Note(int step) => Play(_note, Tune[(step * 3 + step / 4) % Tune.Length], 0.45f);

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
            if (_burp == null)
            {
                _burp = Clip("Burp", SynthSound.Burp(0.95f, 21));
                _whistle = Clip("Whistle", SynthSound.Whistle(0.6f));
                _beep = Clip("Beep", SynthSound.Chime(0.25f, 1320f));
                _slam = Clip("Slam", SynthSound.Rumble(0.9f, 22));
                _slurp = Clip("Slurp", SynthSound.Crunch(0.3f, 0.9f, 23));
                _note = Clip("Note", SynthSound.Chime(0.35f, 660f));
                _clank = Clip("Clank", SynthSound.Clang(0.3f, 420f, 24));
            }
            if (_voices == null || _voices[0] == null)
            {
                var go = new GameObject("ActionAudio"); // a scene object: gone with the level, rebuilt in the next
                _voices = new AudioSource[Voices];
                for (int i = 0; i < Voices; i++)
                {
                    _voices[i] = go.AddComponent<AudioSource>();
                    _voices[i].playOnAwake = false;
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
