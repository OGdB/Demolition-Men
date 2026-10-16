using UnityEngine;

namespace Demolition
{
    /// <summary>One-shot expanding, fading sprite — used to flash the punch overlap
    /// area on every swing. Destroys itself when done.</summary>
    public class SpriteFlash : MonoBehaviour
    {
        private float _size = 1f;
        private float _duration = 0.12f;
        private float _t;
        private SpriteRenderer _sr;
        private Color _startColor;

        public void Configure(float size, float duration)
        {
            _size = size;
            _duration = Mathf.Max(0.01f, duration);
        }

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            if (_sr != null)
                _startColor = _sr.color;
            transform.localScale = Vector3.one * (_size * 0.55f);
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _duration);

            transform.localScale = Vector3.one * (_size * Mathf.Lerp(0.55f, 1.1f, k));
            if (_sr != null)
            {
                var c = _startColor;
                c.a *= 1f - k;
                _sr.color = c;
            }

            if (k >= 1f)
                Destroy(gameObject);
        }
    }
}
