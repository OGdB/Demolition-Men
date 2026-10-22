using UnityEngine;
using UnityEngine.UI;

namespace Demolition
{
    /// <summary>
    /// Cycles a graphic's colour through the rainbow: the stars of a run finished within par.
    /// While it is on it owns the colour; turned off, the colour is the caller's again.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class RainbowTint : MonoBehaviour
    {
        private const float CyclesPerSecond = 0.4f;
        /// <summary>Hue offset between neighbouring stars, so a row shows a sweep of colours.</summary>
        public const float StarPhase = 0.15f;

        private Graphic _graphic;
        private float _phase;

        public static void Set(Graphic graphic, bool on, float phase = 0f)
        {
            var tint = graphic.GetComponent<RainbowTint>();
            if (tint == null)
            {
                if (!on)
                    return;
                tint = graphic.gameObject.AddComponent<RainbowTint>();
            }
            tint._phase = phase;
            tint.enabled = on;
        }

        private void Awake() => _graphic = GetComponent<Graphic>();

        // Unscaled: the results and menus show at any time scale.
        private void Update() =>
            _graphic.color = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * CyclesPerSecond + _phase, 1f), 0.55f, 1f);
    }
}
