using UnityEngine;

namespace Demolition.Actions
{
    /// <summary>A sprite that drifts, wobbles and fades, then removes itself: steam, music notes, dust.</summary>
    public sealed class FxRise : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private Vector2 _velocity;
        private float _seconds, _age, _wobble, _grow;
        private Color _colour;

        public static void Spawn(Sprite sprite, Vector3 at, Color colour, float size, float seconds, Vector2 velocity,
            float wobble = 0f, float grow = 0f, int order = 60)
        {
            if (sprite == null)
                return;
            var go = new GameObject("Fx");
            go.transform.position = at;
            go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = colour;
            sr.sortingOrder = order;
            FxRise fx = go.AddComponent<FxRise>();
            fx._sr = sr;
            fx._colour = colour;
            fx._seconds = Mathf.Max(0.05f, seconds);
            fx._velocity = velocity;
            fx._wobble = wobble;
            fx._grow = grow;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float k = _age / _seconds;
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            Vector2 sway = _wobble > 0f ? new Vector2(Mathf.Sin(_age * 9f) * _wobble, 0f) : Vector2.zero;
            transform.position += (Vector3)((_velocity + sway) * Time.deltaTime);
            transform.localScale += Vector3.one * (_grow * Time.deltaTime);
            Color c = _colour;
            c.a *= 1f - k * k;
            _sr.color = c;
        }
    }
}
