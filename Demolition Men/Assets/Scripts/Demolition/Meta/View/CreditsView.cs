using System.Text;
using Demolition.Levels;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Demolition.Meta
{
    /// <summary>
    /// Credits overlay from the settings: who made the game and every free asset it uses with
    /// its licence, read from <c>Resources/Credits.txt</c> ("# " starts a section). Licences
    /// such as CC BY require this credit, so add a line there with every new asset.
    /// They scroll: wheel or drag, the stick, the D-pad or the arrow keys.
    /// PRIVACY POLICY opens <see cref="GameConfig.privacyPolicyUrl"/> in the browser.
    /// Back (Esc / B) closes it.
    /// </summary>
    public sealed class CreditsView : MonoBehaviour
    {
        private const float ScrollSpeed = 900f; // UI units a second
        private GameObject _previousSelection;
        private ScrollRect _scroll;
        private RectTransform _bar;
        private System.Action _unblock;

        public static bool IsOpen { get; private set; }

        public static void Open(Transform parent)
        {
            if (IsOpen)
                return;
            var go = new GameObject("Credits");
            go.transform.SetParent(parent, false);
            go.AddComponent<CreditsView>();
        }

        private void Awake()
        {
            IsOpen = true;
            _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            Build();
        }

        private void OnDestroy()
        {
            IsOpen = false;
            _unblock?.Invoke();
            if (_previousSelection != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_previousSelection);
        }

        private void Update()
        {
            bool back = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            foreach (Gamepad pad in Gamepad.all)
                back |= pad.buttonEast.wasPressedThisFrame;
            if (back)
                Close();
            Scroll();
        }

        private void Scroll()
        {
            float axis = 0f;
            if (Keyboard.current != null)
                axis = (Keyboard.current.upArrowKey.isPressed ? 1f : 0f) - (Keyboard.current.downArrowKey.isPressed ? 1f : 0f);
            foreach (Gamepad pad in Gamepad.all)
                axis += pad.leftStick.ReadValue().y + pad.dpad.ReadValue().y;
            float view = _scroll.viewport.rect.height, content = _scroll.content.rect.height;
            float extra = content - view;
            if (extra <= 0f)
            {
                _bar.gameObject.SetActive(false);
                return;
            }
            if (Mathf.Abs(axis) > 0.25f)
                _scroll.verticalNormalizedPosition = Mathf.Clamp01(_scroll.verticalNormalizedPosition + Mathf.Clamp(axis, -1f, 1f) * ScrollSpeed * Time.unscaledDeltaTime / extra);
            float height = view * view / content;
            _bar.sizeDelta = new Vector2(_bar.sizeDelta.x, height);
            _bar.anchoredPosition = new Vector2(_bar.anchoredPosition.x, -(1f - _scroll.verticalNormalizedPosition) * (view - height));
        }

        private void Close() => Destroy(gameObject);

        private void Build()
        {
            Canvas canvas = UiFactory.Canvas(transform, "CreditsCanvas", 50);
            _unblock = UiFactory.Modal(canvas);
            var root = (RectTransform)canvas.transform;
            Image dim = UiFactory.Backdrop(root, new Color(0f, 0f, 0f, 0.7f));
            dim.raycastTarget = true; // block clicks to the settings behind
            var centre = new Vector2(0.5f, 0.5f);

            UiFactory.TitledPanel(root, new Vector2(0f, -5f), new Vector2(1700f, 950f), "CREDITS", 80f);
            Image view = UiFactory.Image(root, "View", centre, new Vector2(-10f, 10f), new Vector2(1560f, 690f), Color.clear);
            view.raycastTarget = true; // what the wheel and a drag land on
            view.gameObject.AddComponent<RectMask2D>();
            var top = new Vector2(0.5f, 1f);
            TextMeshProUGUI body = UiFactory.Text(view.rectTransform, "Body", top, Vector2.zero, 28f, TextAlignmentOptions.Top, new Vector2(1540f, 690f));
            body.textWrappingMode = TextWrappingModes.Normal;
            var credits = Resources.Load<TextAsset>("Credits");
            body.text = credits != null ? Format(credits.text) : string.Empty;
            body.rectTransform.sizeDelta = new Vector2(1540f, body.GetPreferredValues(body.text, 1540f, Mathf.Infinity).y);
            _scroll = view.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.viewport = view.rectTransform;
            _scroll.content = body.rectTransform;
            _scroll.scrollSensitivity = 30f;
            _scroll.verticalNormalizedPosition = 1f;
            Image bar = UiFactory.Image(view.rectTransform, "Bar", new Vector2(1f, 1f), new Vector2(16f, 0f), new Vector2(8f, 100f), UiFactory.Gold);
            bar.maskable = false;
            _bar = bar.rectTransform;

            Button back = UiFactory.Button(root, "BACK", centre, new Vector2(-230f, -400f), new Vector2(360f, 90f), Close);
            UiFactory.Button(root, "PRIVACY POLICY", centre, new Vector2(230f, -400f), new Vector2(480f, 90f),
                () => Application.OpenURL(GameConfig.Instance.privacyPolicyUrl));
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(back.gameObject);
        }

        private static string Format(string text)
        {
            var s = new StringBuilder();
            string gold = ColorUtility.ToHtmlStringRGB(UiFactory.Gold);
            foreach (string raw in text.Replace("\r", string.Empty).Split('\n'))
            {
                string line = raw.TrimEnd();
                s.AppendLine(line.StartsWith("# ")
                    ? $"<color=#{gold}><size=120%>{line.Substring(2)}</size></color>"
                    : line);
            }
            return s.ToString();
        }
    }
}
