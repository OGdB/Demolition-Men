using UnityEngine;

namespace Demolition.Actions
{
    /// <summary>The actions' sounds: recordings from <c>Resources/Sounds/Actions</c> (Freesound,
    /// content-work/sounds/sources.md), each one synthesised (<see cref="SynthSound"/>) when its file is
    /// missing: the can, the gulps, the burp, the whistles, the slam's whoosh, rip and thud, the countdown's
    /// beep, velcro and the hard hat, the radio's static, kick and music (a synth tune without it),
    /// power-ups, boings. A few 2D voices that pause with the game.</summary>
    public static class ActionAudio
    {
        private const int Voices = 8;
        private const float Volume = 0.7f;
        private const string Folder = "Sounds/Actions/";

        // A major pentatonic: the radio plays a tune of these.
        private static readonly float[] Tune = { 1f, 1.125f, 1.25f, 1.5f, 1.6875f, 2f };

        private static AudioSource[] _voices;
        private static AudioSource _radio; // the music's own voice: the others' pitch never bends it
        private static int _next;
        private static AudioClip _burp, _whistle, _beep, _rumble, _thud, _note, _can, _gulp, _whoosh, _rip, _velcro,
            _helmet, _plunger, _shovel, _bomb, _powerUp, _boing, _static, _kick, _music, _paper, _pop, _pour;

        /// <summary>A can of fizz cracked open.</summary>
        public static void CanOpen() => Play(_can, 1f, 0.8f);
        public static void Gulp() => Play(_gulp, Random.Range(0.9f, 1.1f), 0.6f);
        /// <summary>Coffee from the flask into its cup.</summary>
        public static void Pour() => Play(_pour, 1f, 0.5f);
        public static void Burp() => Play(_burp, Random.Range(0.92f, 1.05f), 1f);
        /// <summary>Air rushing past: <paramref name="rising"/> for a leap, else a deep breath in.</summary>
        public static void Whoosh(bool rising) => Play(_whoosh, rising ? 1.2f : 0.7f, rising ? 0.7f : 0.4f);
        /// <summary>Trousers tearing.</summary>
        public static void Rip() => Play(_rip, 1f, 0.6f);
        public static void Velcro() => Play(_velcro, 1f, 0.6f);
        /// <summary>The hard hat on.</summary>
        public static void Helmet() => Play(_helmet, 1f, 0.6f);
        /// <summary>The detonator's plunger pulled up.</summary>
        public static void Plunger() => Play(_plunger, 1f, 0.7f);
        /// <summary>The shovel stuck in the ground.</summary>
        public static void Shovel() => Play(_shovel, 1f, 0.7f);
        public static void Whistle() => Play(_whistle, 1f, 0.8f);
        /// <summary>A load falling from the sky, cartoon style.</summary>
        public static void Falling() => Play(_bomb, 1f, 0.5f);
        /// <summary>The 3-2-1 buzz; the last one higher.</summary>
        public static void Beep(int count) => Play(_beep, count > 1 ? 1f : 1.5f, 0.5f);
        /// <summary>Something heavy lands: a thud with the rubble's rumble after it.</summary>
        public static void Slam()
        {
            Play(_thud, Random.Range(0.9f, 1.05f), 1f);
            Play(_rumble, 0.8f, 0.8f);
        }
        /// <summary>A lighter thump: a radio set down.</summary>
        public static void Thump() => Play(_thud, 1.6f, 0.5f);
        /// <summary>A buff starts: a rising buzz.</summary>
        public static void PowerUp() => Play(_powerUp, 1f, 0.45f);
        /// <summary>Rubble bouncing off the vest.</summary>
        public static void Boing() => Play(_boing, Random.Range(0.9f, 1.15f), 0.5f);
        /// <summary>The radio switched on.</summary>
        public static void Static() => Play(_static, 1f, 0.45f);
        /// <summary>One beat of the radio: a kick, the music on the first (it lasts the radio's 8 s, at its
        /// 120 bpm), else note <paramref name="step"/> of the synth tune.</summary>
        public static void Beat(int step)
        {
            Play(_kick, 1f, 0.7f);
            if (_music == null)
                Play(_note, Tune[(step * 3 + step / 4) % Tune.Length], 0.4f);
            else if (step == 0 && Ready())
            {
                _radio.clip = _music;
                _radio.volume = Volume * 0.6f;
                _radio.Play(); // a second radio restarts it rather than playing out of step
            }
        }
        /// <summary>The blueprint unrolled.</summary>
        public static void Paper() => Play(_paper, 1f, 0.5f);
        /// <summary>The intern turns up, or clocks off.</summary>
        public static void Pop() => Play(_pop, Random.Range(0.95f, 1.1f), 0.6f);

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
                _burp = Clip("Burp", () => SynthSound.Burp(0.95f, 21));
                _whistle = Clip("Whistle", () => SynthSound.Whistle(0.6f));
                _beep = Clip("Beep", () => SynthSound.Sweep(0.14f, 880f, 880f, decay: 1.5f, square: true));
                _rumble = Clip("Rumble", () => SynthSound.Rumble(0.9f, 22));
                _thud = Clip("Thud", () => SynthSound.Sweep(0.35f, 120f, 38f, decay: 6f));
                _note = Clip("Note", () => SynthSound.Chime(0.35f, 660f));
                _can = Clip("Can", () => SynthSound.Noise(0.4f, 25, 0.95f, 0.5f, highPass: true, decay: 5f));
                _gulp = Clip("Gulp", () => SynthSound.Sweep(0.11f, 340f, 150f, decay: 4f));
                _whoosh = Clip("Whoosh", () => SynthSound.Noise(0.45f, 26, 0.05f, 0.6f, attack: 0.4f, decay: 3f));
                _rip = Clip("Rip", () => SynthSound.Noise(0.3f, 27, 0.7f, 0.5f, highPass: true, grain: 0.7f, decay: 2f));
                _velcro = Clip("Velcro", () => SynthSound.Noise(0.2f, 31, 0.9f, 0.7f, highPass: true, grain: 0.7f, decay: 3f));
                _helmet = Clip("Helmet", () => SynthSound.Sweep(0.22f, 190f, 60f, decay: 10f));
                _plunger = Clip("Plunger", () => SynthSound.Clang(0.3f, 294f, 24));
                _shovel = Clip("Shovel", () => SynthSound.Clang(0.2f, 670f, 32));
                _bomb = Clip("Falling", () => SynthSound.Sweep(1f, 2100f, 700f, decay: 0.6f));
                _powerUp = Clip("PowerUp", () => SynthSound.Sweep(0.35f, 330f, 1320f, decay: 1.5f, square: true));
                _boing = Clip("Boing", () => SynthSound.Sweep(0.4f, 200f, 300f, decay: 4f, wobble: 0.35f));
                _static = Clip("Static", () => SynthSound.Noise(0.5f, 28, 0.9f, 0.9f, highPass: true, grain: 0.4f, decay: 3f));
                _kick = Clip("Kick", () => SynthSound.Sweep(0.18f, 150f, 45f, decay: 7f));
                _paper = Clip("Paper", () => SynthSound.Noise(0.35f, 29, 0.6f, 0.4f, grain: 0.85f, attack: 0.1f, decay: 3f));
                _pop = Clip("Pop", () => SynthSound.Sweep(0.09f, 700f, 220f, decay: 5f));
                _pour = Clip("Pour", () => SynthSound.Noise(0.6f, 30, 0.25f, 0.45f, grain: 0.3f, attack: 0.15f, decay: 2.5f));
                _music = Resources.Load<AudioClip>(Folder + "Radio"); // no synth stand-in: Beat plays the tune instead
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
                _radio = go.AddComponent<AudioSource>();
                _radio.playOnAwake = false;
            }
            return true;
        }

        private static AudioClip Clip(string name, System.Func<float[]> synth)
        {
            var recorded = Resources.Load<AudioClip>(Folder + name);
            if (recorded != null)
                return recorded;
            float[] samples = synth();
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SynthSound.SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
