using System.Collections;
using UnityEngine;

namespace Demolition.Generation
{
    /// <summary>
    /// The sound system of a building with speakers in it (a club): one bar of bass and kick
    /// (<see cref="SynthSound.ClubLoop"/>) on a loop, louder the nearer the camera's middle is
    /// to the building, silent past <see cref="Range"/>. It runs on the building's power: when
    /// that fails (<see cref="BuildingLights.Cut"/>) the record winds down and stops.
    /// </summary>
    public sealed class BuildingMusic : MonoBehaviour
    {
        /// <summary>The prop that makes a building play.</summary>
        public const string Speaker = "speaker";
        /// <summary>World units from the building's middle at which nothing is heard.</summary>
        private const float Range = 22f;
        private const float Volume = 0.7f;
        private const float WindDown = 0.7f;

        private static AudioClip _loop;
        private AudioSource _source;
        private float _middle;
        private float _power = 1f;

        public static void Play(BuildingLights power, float width)
        {
            if (_loop == null)
            {
                float[] samples = SynthSound.ClubLoop();
                _loop = AudioClip.Create("ClubLoop", samples.Length, 1, SynthSound.SampleRate, false);
                _loop.SetData(samples, 0);
            }
            var music = power.gameObject.AddComponent<BuildingMusic>();
            music._middle = width * 0.5f;
            music._source = power.gameObject.AddComponent<AudioSource>();
            music._source.clip = _loop;
            music._source.loop = true;
            music._source.volume = 0f;
            music._source.Play();
            power.Cut += music.OnCut;
        }

        private void Update()
        {
            Camera cam = Camera.main;
            if (cam == null || _source == null)
                return;
            float near = Mathf.Clamp01(1f - Mathf.Abs(cam.transform.position.x - (transform.position.x + _middle)) / Range);
            _source.volume = Volume * near * near * _power;
        }

        private void OnCut() => StartCoroutine(Wind());

        // The turntable loses its power: slower, lower, gone.
        private IEnumerator Wind()
        {
            for (float t = 0f; t < WindDown; t += Time.deltaTime)
            {
                _power = 1f - t / WindDown;
                _source.pitch = Mathf.Lerp(1f, 0.3f, t / WindDown);
                yield return null;
            }
            _source.Stop();
            enabled = false;
        }
    }
}
