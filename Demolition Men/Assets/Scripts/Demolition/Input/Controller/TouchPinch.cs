using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Demolition
{
    /// <summary>Two fingers on the screen away from the touch controls: pinching zooms the
    /// camera, following the fingers (apart: closer). Writes <see cref="TouchInputSource"/>
    /// zoom steps.</summary>
    public sealed class TouchPinch : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private readonly Dictionary<int, Vector2> _fingers = new Dictionary<int, Vector2>();
        private TouchInputSource _source;
        private float _spread; // between the two fingers as last seen, 0 with fewer than two

        public void Configure(TouchInputSource source) => _source = source;

        public void OnPointerDown(PointerEventData e)
        {
            if (_fingers.Count >= 2)
                return;
            _fingers[e.pointerId] = e.position;
            _spread = Spread();
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_fingers.ContainsKey(e.pointerId))
                return;
            _fingers[e.pointerId] = e.position;
            float spread = Spread();
            if (_spread > 1f && spread > 1f)
                _source.AddZoom(Mathf.Log(_spread / spread) / Mathf.Log(PlayerInputFrame.ZoomStep));
            _spread = spread;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (_fingers.Remove(e.pointerId))
                _spread = Spread();
        }

        private void OnDisable()
        {
            _fingers.Clear();
            _spread = 0f;
        }

        private float Spread()
        {
            if (_fingers.Count < 2)
                return 0f;
            Vector2 first = default;
            bool any = false;
            foreach (Vector2 finger in _fingers.Values)
            {
                if (any)
                    return Vector2.Distance(first, finger);
                first = finger;
                any = true;
            }
            return 0f;
        }
    }
}
