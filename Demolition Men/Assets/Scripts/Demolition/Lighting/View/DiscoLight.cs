using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Demolition.Lighting
{
    /// <summary>A club's light: it turns through the colours and pumps on the beat of
    /// <see cref="SynthSound.ClubLoop"/> (near enough: this follows the game's clock, the music the audio's).</summary>
    public sealed class DiscoLight : MonoBehaviour
    {
        private Light2D _light;
        private float _intensity, _offset;

        private void Awake()
        {
            _light = GetComponent<Light2D>();
            _intensity = _light.intensity;
            _offset = transform.position.x * 0.13f; // neighbours in different colours
        }

        private void Update()
        {
            float beat = Time.time * SynthSound.ClubBpm / 60f;
            _light.color = Color.HSVToRGB(Mathf.Repeat(beat * 0.125f + _offset, 1f), 0.75f, 1f);
            _light.intensity = _intensity * (0.65f + 0.35f * (1f - Mathf.Repeat(beat, 1f)));
        }
    }
}
