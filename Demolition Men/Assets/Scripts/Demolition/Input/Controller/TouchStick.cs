using UnityEngine;
using UnityEngine.EventSystems;

namespace Demolition
{
    /// <summary>A floating thumbstick: touching anywhere in its zone puts the stick under
    /// the thumb, dragging steers. Writes <see cref="TouchInputSource.Move"/>.</summary>
    public sealed class TouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const float DeadZone = 0.15f;

        private TouchInputSource _source;
        private RectTransform _zone;
        private RectTransform _base;
        private RectTransform _knob;
        private Vector2 _home;
        private float _radius;
        private int _pointer = int.MinValue;

        public void Configure(TouchInputSource source, RectTransform stickBase, RectTransform knob, float radius)
        {
            _source = source;
            _zone = (RectTransform)transform;
            _base = stickBase;
            _knob = knob;
            _radius = radius;
            _home = stickBase.anchoredPosition;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_pointer != int.MinValue)
                return;
            _pointer = e.pointerId;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_zone, e.position, e.pressEventCamera, out Vector2 local))
                _base.anchoredPosition = local - _zone.rect.min;
            Steer(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId == _pointer)
                Steer(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _pointer)
                return;
            _pointer = int.MinValue;
            Release();
        }

        private void OnDisable()
        {
            _pointer = int.MinValue;
            Release();
        }

        private void Release()
        {
            if (_source != null)
                _source.Move = Vector2.zero;
            if (_base != null)
            {
                _base.anchoredPosition = _home;
                _knob.anchoredPosition = Vector2.zero;
            }
        }

        private void Steer(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_base, e.position, e.pressEventCamera, out Vector2 local))
                return;
            Vector2 offset = Vector2.ClampMagnitude(local, _radius);
            _knob.anchoredPosition = offset;
            Vector2 move = offset / _radius;
            _source.Move = move.magnitude < DeadZone ? Vector2.zero : move;
        }
    }
}
