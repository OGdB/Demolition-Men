using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// A background layer that follows the camera at a fraction of its speed and wraps
    /// around, so a few sprites make an endless parallax backdrop. 0 = fixed in the world,
    /// 1 = glued to the camera. Uses the main camera when none is set (runtime-built levels).
    /// </summary>
    // After the camera has moved this frame, so the backdrop doesn't lag a frame behind.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ParallaxLayer : MonoBehaviour
    {
        // Field names kept from the original Parralyx script so the prefabs keep their values.
        [SerializeField] private GameObject cam;
        [SerializeField, Range(0f, 1f)] private float parallaxEffect;

        private float _length;
        private float _start;

        private void Start()
        {
            _start = transform.position.x;
            _length = GetComponent<SpriteRenderer>().bounds.size.x;
        }

        private void LateUpdate()
        {
            if (cam == null)
            {
                Camera main = Camera.main;
                if (main == null)
                    return;
                cam = main.gameObject;
            }
            float camX = cam.transform.position.x;
            float travelled = camX * (1f - parallaxEffect);
            transform.position = new Vector3(_start + camX * parallaxEffect, transform.position.y, transform.position.z);

            if (_length <= 0f)
                return;
            if (travelled > _start + _length) _start += _length;
            else if (travelled < _start - _length) _start -= _length;
        }
    }
}
