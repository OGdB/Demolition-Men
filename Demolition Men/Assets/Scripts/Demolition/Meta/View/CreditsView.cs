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
    /// PRIVACY POLICY opens <see cref="GameConfig.privacyPolicyUrl"/> in the browser.
    /// Back (Esc / B) closes it.
    /// </summary>
    public sealed class CreditsView : MonoBehaviour
    {
        private GameObject _previousSelection;
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
            TextMeshProUGUI body = UiFactory.Text(root, "Body", centre, new Vector2(0f, 20f), 30f, TextAlignmentOptions.Top, new Vector2(1540f, 700f));
            body.textWrappingMode = TextWrappingModes.Normal;
            body.enableAutoSizing = true; // long licence lines shrink to fit instead of running off the plate
            body.fontSizeMin = 18f;
            body.fontSizeMax = 30f;
            var credits = Resources.Load<TextAsset>("Credits");
            body.text = credits != null ? Format(credits.text) : string.Empty;

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
