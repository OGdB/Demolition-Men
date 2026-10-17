using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Demolition
{
    /// <summary>Feel for a code-built button (<see cref="UiFactory.Button"/>): it grows while
    /// selected or hovered (readable from the sofa), dips while pressed, dims its label while
    /// disabled, ticks when reached and clanks when pressed.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class UiButtonFeel : MonoBehaviour, ISelectHandler, IDeselectHandler,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float Grow = 1.07f;
        private const float Dip = 0.94f;
        private const float Sharpness = 14f;

        private Button _button;
        private TextMeshProUGUI _label;
        private bool _selected;
        private bool _hover;
        private bool _down;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _label = GetComponentInChildren<TextMeshProUGUI>();
            _button.onClick.AddListener(UiAudio.Click);
        }

        private void Update()
        {
            bool live = _button.IsInteractable();
            float target = !live ? 1f : _down ? Dip : _selected || _hover ? Grow : 1f;
            float scale = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-Sharpness * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(scale, scale, 1f);
            if (_label != null)
                _label.alpha = _button.interactable ? 1f : 0.45f;
        }

        public void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            if (_button.IsInteractable())
                UiAudio.Tick();
        }

        public void OnDeselect(BaseEventData eventData) => _selected = false;

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hover = true;
            if (_button.IsInteractable() && !_selected)
                UiAudio.Tick();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hover = false;
            _down = false;
        }

        public void OnPointerDown(PointerEventData eventData) => _down = true;

        public void OnPointerUp(PointerEventData eventData) => _down = false;
    }
}
