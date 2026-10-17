using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Demolition
{
    /// <summary>An on-screen button reporting press and release (hold to attack, tap to
    /// throw). Shrinks and lights up while held.</summary>
    public sealed class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private Action<bool> _changed;
        private Image _image;
        private Color _idle;
        private int _pointer = int.MinValue;

        public void Configure(Action<bool> changed)
        {
            _changed = changed;
            _image = GetComponent<Image>();
            _idle = _image.color;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_pointer != int.MinValue)
                return;
            _pointer = e.pointerId;
            Set(true);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _pointer)
                return;
            _pointer = int.MinValue;
            Set(false);
        }

        private void OnDisable()
        {
            if (_pointer == int.MinValue)
                return;
            _pointer = int.MinValue;
            Set(false);
        }

        private void Set(bool held)
        {
            transform.localScale = Vector3.one * (held ? 0.92f : 1f);
            if (_image != null)
                _image.color = held ? new Color(1f, 0.85f, 0.3f, Mathf.Max(_idle.a, 0.8f)) : _idle;
            _changed?.Invoke(held);
        }
    }
}
