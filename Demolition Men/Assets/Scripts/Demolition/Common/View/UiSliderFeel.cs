using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Demolition
{
    /// <summary>Feel for a code-built slider (<see cref="UiFactory.Slider"/>), like
    /// <see cref="UiButtonFeel"/>: the handle grows while selected or hovered, ticks when reached and
    /// at every 5% it passes, higher the further up. <see cref="Settled"/> hears when it stops moving:
    /// the pointer let go, or each step of a key or a stick.</summary>
    [RequireComponent(typeof(Slider))]
    public sealed class UiSliderFeel : MonoBehaviour, ISelectHandler, IDeselectHandler,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float Grow = 1.15f;
        private const float Sharpness = 14f;

        public System.Action Settled;

        private Slider _slider;
        private bool _selected;
        private bool _hover;
        private bool _down;
        private int _notch;

        private void Awake()
        {
            _slider = GetComponent<Slider>();
            _notch = Notch(_slider.normalizedValue);
            _slider.onValueChanged.AddListener(Moved);
        }

        private static int Notch(float value) => Mathf.RoundToInt(value * 20f);

        private void Moved(float value)
        {
            float normalized = _slider.normalizedValue;
            if (Notch(normalized) != _notch)
            {
                _notch = Notch(normalized);
                UiAudio.Tick(0.8f + 0.5f * normalized);
            }
            if (!_down)
                Settled?.Invoke();
        }

        private void Update()
        {
            RectTransform handle = _slider.handleRect;
            if (handle == null)
                return;
            float target = _slider.IsInteractable() && (_selected || _hover || _down) ? Grow : 1f;
            float scale = Mathf.Lerp(handle.localScale.x, target, 1f - Mathf.Exp(-Sharpness * Time.unscaledDeltaTime));
            handle.localScale = new Vector3(scale, scale, 1f);
        }

        public void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            if (_slider.IsInteractable())
                UiAudio.Tick();
        }

        public void OnDeselect(BaseEventData eventData) => _selected = false;

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hover = true;
            if (_slider.IsInteractable() && !_selected)
                UiAudio.Tick();
        }

        public void OnPointerExit(PointerEventData eventData) => _hover = false;

        public void OnPointerDown(PointerEventData eventData) => _down = true;

        public void OnPointerUp(PointerEventData eventData)
        {
            _down = false;
            Settled?.Invoke();
        }
    }
}
