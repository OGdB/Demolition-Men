using Demolition.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Demolition.Run
{
    /// <summary>Pause overlay built in code: Resume, Restart, Settings, Menu, and a card with
    /// the controls of this device's players. Shown while <see cref="PauseController.MenuOpen"/>;
    /// on a client the host's pause shows it too, saying so where RESUME would be.</summary>
    [RequireComponent(typeof(PauseController))]
    public sealed class PauseView : MonoBehaviour
    {
        private const float MenuWidth = 760f, PanelHeight = 640f, Gap = 40f;
        private const float ActionWidth = 260f, DeviceWidth = 230f, RowHeight = 56f; // eight controls fit the panel

        private PauseController _pause;
        private RunController _run;
        private GameObject _root;
        private RectTransform _menu;
        private RectTransform _card;
        private Button _resume, _settings;
        private TextMeshProUGUI _hostPaused;

        private void Awake()
        {
            _pause = GetComponent<PauseController>();
            _run = GetComponent<RunController>();
            PauseController.Changed += OnChanged;
        }

        private void OnDestroy() => PauseController.Changed -= OnChanged;

        private void OnChanged(bool open)
        {
            if (_root == null)
            {
                if (!open)
                    return; // unpaused while the scene is torn down: nothing to hide
                Build();
            }
            _root.SetActive(open);
            if (!open)
                return;
            bool host = PauseController.HostPaused;
            _resume.gameObject.SetActive(!host);
            _hostPaused.gameObject.SetActive(host);
            BuildCard(); // the devices in use can change between pauses
            if (EventSystem.current != null && !SettingsView.IsOpen)
                EventSystem.current.SetSelectedGameObject((host ? _settings : _resume).gameObject);
        }

        private void Build()
        {
            Canvas canvas = UiFactory.Canvas(transform, "Pause", 25);
            _root = canvas.gameObject;
            var root = (RectTransform)canvas.transform;
            UiFactory.Image(UiFactory.Stretch(root, "Dim"), "Background", Vector2.zero, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f))
                .rectTransform.anchorMax = Vector2.one;
            var centre = new Vector2(0.5f, 0.5f);

            _menu = UiFactory.Rect(root, "Menu", centre, Vector2.zero, new Vector2(MenuWidth, PanelHeight));
            UiFactory.TitledPanel(_menu, new Vector2(0f, -10f), new Vector2(MenuWidth, PanelHeight), "PAUSED", 96f);

            var size = new Vector2(480f, 96f);
            _resume = UiFactory.Button(_menu, "RESUME", centre, new Vector2(0f, 120f), size, () => _pause.SetPaused(false));
            _hostPaused = UiFactory.Text(_menu, "HostPaused", centre, new Vector2(0f, 120f), 44f, TextAlignmentOptions.Center,
                new Vector2(MenuWidth - 80f, 96f), outlined: true);
            _hostPaused.enableAutoSizing = true;
            _hostPaused.fontSizeMin = 24f;
            _hostPaused.fontSizeMax = 44f;
            _hostPaused.text = "THE HOST PAUSED THE GAME";
            _hostPaused.color = UiFactory.Gold;
            // Online only the host restarts (for both); the client's MENU leaves the session.
            UiFactory.Button(_menu, "RESTART", centre, new Vector2(0f, 0f), size, _run.Retry).interactable = !NetRole.IsClient;
            _settings = UiFactory.Button(_menu, "SETTINGS", centre, new Vector2(0f, -120f), size, () => SettingsView.Open(transform));
            UiFactory.Button(_menu, NetRole.IsClient ? "LEAVE" : "MENU", centre, new Vector2(0f, -240f), size, _run.QuitToMenu);
        }

        // A row per control, a column per kind of device this device's players use (a keyboard
        // and a gamepad in local co-op), beside the menu.
        private void BuildCard()
        {
            if (_card != null)
                Destroy(_card.gameObject);
            var used = new bool[3];
            int devices = 0;
            foreach (RunController.Player player in _run.Players)
                if (!player.Health.IsProxy && player.Input != null && !used[(int)player.Input.Style])
                {
                    used[(int)player.Input.Style] = true;
                    devices++;
                }
            if (devices == 0)
            {
                used[(int)InputStyle.Keyboard] = used[(int)InputStyle.Gamepad] = true;
                devices = 2;
            }

            float width = 80f + ActionWidth + DeviceWidth * devices;
            float left = -(MenuWidth + Gap + width) * 0.5f;
            _menu.anchoredPosition = new Vector2(left + MenuWidth * 0.5f, 0f);
            var centre = new Vector2(0.5f, 0.5f);
            _card = UiFactory.Rect(_menu.parent, "Controls", centre, new Vector2(left + MenuWidth + Gap + width * 0.5f, 0f),
                new Vector2(width, PanelHeight));
            UiFactory.TitledPanel(_card, new Vector2(0f, -10f), new Vector2(width, PanelHeight), "CONTROLS", 72f);

            float x = -width * 0.5f + 40f, top = 180f;
            int column = 0;
            for (int style = 0; style < used.Length; style++)
            {
                if (!used[style])
                    continue;
                TextMeshProUGUI head = Cell(((InputStyle)style).ToString().ToUpperInvariant(), DeviceX(x, column++), top,
                    DeviceWidth, TextAlignmentOptions.Center);
                head.color = UiFactory.Gold;
            }
            for (int control = 0; control < InputGlyphs.Controls; control++)
            {
                float y = top - RowHeight * (control + 1);
                if (control % 2 == 0)
                    UiFactory.Image(_card, "Row", centre, new Vector2(0f, y), new Vector2(width - 64f, RowHeight), new Color(0f, 0f, 0f, 0.25f));
                Cell(InputGlyphs.Action(control), x + ActionWidth * 0.5f, y, ActionWidth, TextAlignmentOptions.Left);
                column = 0;
                for (int style = 0; style < used.Length; style++)
                    if (used[style])
                        Cell(InputGlyphs.Buttons(control, (InputStyle)style), DeviceX(x, column++), y, DeviceWidth - 10f, TextAlignmentOptions.Center);
            }
        }

        private static float DeviceX(float x, int column) => x + ActionWidth + DeviceWidth * (column + 0.5f);

        // Long names (DRAG ON THE LEFT) shrink to fit their column.
        private TextMeshProUGUI Cell(string text, float x, float y, float width, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI cell = UiFactory.Text(_card, "Cell", new Vector2(0.5f, 0.5f), new Vector2(x, y), 34f, alignment,
                new Vector2(width, RowHeight), outlined: true);
            cell.enableAutoSizing = true;
            cell.fontSizeMin = 18f;
            cell.fontSizeMax = 34f;
            cell.text = text;
            return cell;
        }
    }
}
