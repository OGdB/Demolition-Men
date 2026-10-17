using UnityEngine;

namespace Demolition.Run
{
    /// <summary>
    /// The sound of things breaking: a crunch per destroyed block (pitched by material, a
    /// clang for metal) and a rumble per collapse scaled by its size. Clips are synthesised
    /// once (<see cref="SynthSound"/>); a small pool of 2D sources plays them.
    /// </summary>
    public sealed class RunAudio : MonoBehaviour
    {
        private const int Voices = 8;
        /// <summary>A dynamite blast destroys many blocks at once: only this many crunches play.</summary>
        private const int MaxPerFrame = 3;

        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;

        private static AudioClip[] _crunches;
        private static AudioClip _clang;
        private static AudioClip _rumble;
        private AudioSource[] _voices;
        private AudioSource _rumbleVoice;
        private float _rumbleSize;
        private int _next;
        private int _frame;
        private int _playedThisFrame;

        private void Awake()
        {
            if (_crunches == null)
            {
                _crunches = new[]
                {
                    Clip("Crunch0", SynthSound.Crunch(0.22f, 0.2f, 1)),
                    Clip("Crunch1", SynthSound.Crunch(0.18f, 0.35f, 2)),
                    Clip("Crack", SynthSound.Crunch(0.12f, 0.8f, 3)),
                };
                _clang = Clip("Clang", SynthSound.Clang(0.45f, 310f, 4));
                _rumble = Clip("Rumble", SynthSound.Rumble(1.6f, 5));
            }
            _rumbleVoice = gameObject.AddComponent<AudioSource>();
            _rumbleVoice.playOnAwake = false;
            _voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
            }
        }

        private void OnEnable()
        {
            DestructionEvents.BlockDestroyed += OnBlockDestroyed;
            DestructionEvents.Collapsed += OnCollapsed;
        }

        private void OnDisable()
        {
            DestructionEvents.BlockDestroyed -= OnBlockDestroyed;
            DestructionEvents.Collapsed -= OnCollapsed;
        }

        private void OnBlockDestroyed(BlockDestroyedInfo info)
        {
            switch (info.Material)
            {
                case BlockMaterial.Glass:
                    return; // the player's glass-break sound covers it
                case BlockMaterial.Metal:
                case BlockMaterial.Support:
                    Play(_clang, Random.Range(0.85f, 1.15f), 0.8f);
                    break;
                case BlockMaterial.Wood:
                    Play(_crunches[2], Random.Range(0.9f, 1.2f), 0.7f);
                    break;
                default:
                    Play(_crunches[Random.Range(0, 2)], Random.Range(0.8f, 1.1f), 0.8f);
                    break;
            }
        }

        private void OnCollapsed(CollapseInfo info)
        {
            // Its own source: re-pitching a shared voice would bend a rumble still playing.
            float size = Mathf.Clamp01(info.Cells.Count / 40f);
            if (_rumbleVoice.isPlaying && size <= _rumbleSize)
                return; // a chain of collapses: the bigger rumble keeps going
            _rumbleSize = size;
            _rumbleVoice.Stop();
            _rumbleVoice.pitch = Mathf.Lerp(1.2f, 0.7f, size);
            _rumbleVoice.PlayOneShot(_rumble, volume * (0.5f + 0.5f * size));
        }

        private void Play(AudioClip clip, float pitch, float loudness)
        {
            if (_frame != Time.frameCount)
            {
                _frame = Time.frameCount;
                _playedThisFrame = 0;
            }
            if (++_playedThisFrame > MaxPerFrame)
                return;
            AudioSource voice = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            voice.pitch = pitch;
            voice.PlayOneShot(clip, volume * loudness);
        }

        private static AudioClip Clip(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SynthSound.SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
