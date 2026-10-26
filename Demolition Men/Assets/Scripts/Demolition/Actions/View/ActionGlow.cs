using UnityEngine;

namespace Demolition.Actions
{
    /// <summary>A one-pixel outline in a colour around the player's sprite while a buff lasts (the
    /// hi-vis vest's orange, the coffee's brown, the partner's gold): four copies of the sprite,
    /// a pixel out each way, behind it. The health flash owns the sprite's own colour.</summary>
    public sealed class ActionGlow : MonoBehaviour
    {
        private static readonly Vector2[] Offsets = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };

        private SpriteRenderer _source;
        private readonly SpriteRenderer[] _copies = new SpriteRenderer[4];
        private float _until;
        private Color _colour;

        /// <summary>Outline <paramref name="target"/>'s sprite in <paramref name="colour"/> for <paramref name="seconds"/>.</summary>
        public static void Show(GameObject target, Color colour, float seconds)
        {
            ActionGlow glow = target.GetComponent<ActionGlow>();
            if (glow == null)
                glow = target.AddComponent<ActionGlow>();
            glow._colour = colour;
            glow._until = Time.time + seconds;
            glow.enabled = true;
        }

        private void Awake()
        {
            _source = GetComponentInChildren<SpriteRenderer>();
            for (int i = 0; i < _copies.Length; i++)
            {
                var go = new GameObject("Glow");
                go.transform.SetParent(_source != null ? _source.transform : transform, false);
                go.transform.localPosition = Offsets[i] / 32f; // one sprite pixel at 32 PPU
                _copies[i] = go.AddComponent<SpriteRenderer>();
            }
        }

        private void LateUpdate()
        {
            bool on = Time.time < _until && _source != null && _source.enabled;
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 10f);
            foreach (SpriteRenderer copy in _copies)
            {
                copy.enabled = on;
                if (!on)
                    continue;
                copy.sprite = _source.sprite;
                copy.flipX = _source.flipX;
                copy.sortingLayerID = _source.sortingLayerID;
                copy.sortingOrder = _source.sortingOrder - 1;
                copy.color = new Color(_colour.r, _colour.g, _colour.b, _colour.a * pulse);
            }
            if (!on)
                enabled = Time.time < _until;
        }
    }
}
