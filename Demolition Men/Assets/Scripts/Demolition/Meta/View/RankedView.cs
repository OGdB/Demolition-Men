using System;
using System.Collections.Generic;
using System.Globalization;
using Demolition.Actions;
using Demolition.Levels;
using Demolition.Ranked;
using Demolition.Run;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Demolition.Meta
{
    /// <summary>
    /// RANKED (docs/ranked.md), from the level select: today's level, the same for everyone; its board
    /// (solo, or co-op for a crew of two, local or online) with this device's place; the action each
    /// player carries (any of them: ranked lends every action and no upgrades); PLAY. Pressing a row
    /// twice reports it. Development builds can also play the day offline, checked on this device.
    /// Built in code; Back (Esc / B) closes.
    /// </summary>
    public sealed class RankedView : MonoBehaviour
    {
        private const int Rows = 10;

        private Action _closed;
        private System.Action _unblock;
        private GameObject _previousSelection;
        private RankedMode _mode;
        private TextMeshProUGUI _day, _status, _you;
        private Button _play;
        private readonly Button[] _rows = new Button[Rows];
        private readonly Image[,] _stars = new Image[Rows, RunScore.MaxStars];
        private readonly int[] _runs = new int[Rows];
        private readonly string[] _names = new string[Rows];
        private int _reportArmed = -1;
        private float _reportArmedAt;

        public static bool IsOpen { get; private set; }

        public static void Open(Transform parent, Action closed = null)
        {
            if (IsOpen)
                return;
            var go = new GameObject("Ranked");
            go.transform.SetParent(parent, false);
            go.AddComponent<RankedView>()._closed = closed;
        }

        private void Awake()
        {
            IsOpen = true;
            _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            _mode = NetRole.IsOnline || Session.PlayerCount > 1 ? RankedMode.Coop : RankedMode.Solo;
            Build();
            Load();
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

        private void Close()
        {
            Destroy(gameObject);
            _closed?.Invoke();
        }

        private void Build()
        {
            Canvas canvas = UiFactory.Canvas(transform, "RankedCanvas", 40);
            _unblock = UiFactory.Modal(canvas);
            var root = (RectTransform)canvas.transform;
            UiFactory.Backdrop(root, new Color(0f, 0f, 0f, 0.75f));
            var centre = new Vector2(0.5f, 0.5f);

            UiFactory.TitledPanel(root, new Vector2(0f, 10f), new Vector2(1700f, 990f), "RANKED", 80f);
            _day = UiFactory.Text(root, "Day", centre, new Vector2(0f, 385f), 40f, TextAlignmentOptions.Center, new Vector2(1300f, 52f), outlined: true);
            _day.color = UiFactory.Gold;
            _status = UiFactory.Text(root, "Status", centre, new Vector2(0f, 335f), 30f, TextAlignmentOptions.Center, new Vector2(1300f, 44f));

            for (int i = 0; i < Rows; i++)
            {
                int row = i;
                Button button = UiFactory.Button(root, "Row " + i, centre, new Vector2(0f, 270f - i * 54f), new Vector2(1500f, 50f), () => Report(row));
                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Left;
                label.fontSize = 30f;
                label.margin = new Vector4(20f, 0f, 150f, 0f);
                for (int s = 0; s < RunScore.MaxStars; s++)
                    _stars[i, s] = UiFactory.Star(button.transform, "Star " + s, new Vector2(1f, 0.5f), new Vector2(-110f + s * 36f, 0f), 30f, false);
                _rows[i] = button;
                button.gameObject.SetActive(false);
            }
            _you = UiFactory.Text(root, "You", centre, new Vector2(0f, -285f), 36f, TextAlignmentOptions.Center, new Vector2(1300f, 50f), outlined: true);

            int players = _mode == RankedMode.Coop && !NetRole.IsOnline ? 2 : 1;
            bool test = Debug.isDebugBuild && !NetRole.IsOnline;
            float spacing = Mathf.Min(370f, 1620f / (players + (test ? 3 : 2)));
            var size = new Vector2(spacing - 20f, 84f);
            var buttons = new List<Button>();
            for (int p = 0; p < players; p++)
                buttons.Add(ActionPicker(root, p, size));
            _play = UiFactory.Button(root, "PLAY", centre, Vector2.zero, size, Play);
            _play.interactable = !NetRole.IsClient;
            buttons.Insert(0, _play);
            if (test)
            {
                buttons.Add(UiFactory.Button(root, "TEST OFFLINE", centre, Vector2.zero, size, PlayOffline));
                UiFactory.Label(buttons[buttons.Count - 1], "TEST OFFLINE", 32f);
            }
            buttons.Add(UiFactory.Button(root, "BACK", centre, Vector2.zero, size, Close));
            for (int i = 0; i < buttons.Count; i++)
                ((RectTransform)buttons[i].transform).anchoredPosition = new Vector2((i - (buttons.Count - 1) * 0.5f) * spacing, -400f);

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_play.gameObject);
        }

        // "P1: BURP": each press, the next action (every one, owned or not).
        private Button ActionPicker(RectTransform root, int player, Vector2 size)
        {
            Button button = null;
            button = UiFactory.Button(root, "Action " + player, new Vector2(0.5f, 0.5f), Vector2.zero, size, () =>
            {
                ActionKind kind = ActionCatalog.RankedPick(Profile.Data, player);
                int next = 0;
                for (int i = 0; i < ActionCatalog.All.Count; i++)
                    if (ActionCatalog.All[i].Kind == kind)
                        next = (i + 1) % ActionCatalog.All.Count;
                ActionCatalog.PickRanked(Profile.Data, player, ActionCatalog.All[next].Kind);
                Profile.Save();
                ShowAction(button, player);
            });
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.textWrappingMode = TextWrappingModes.Normal; // CONTROLLED DEMOLITION is wider than the button
            label.enableAutoSizing = true;
            label.fontSizeMin = 16f;
            label.fontSizeMax = 32f;
            ShowAction(button, player);
            return button;
        }

        private static void ShowAction(Button button, int player) =>
            UiFactory.Label(button, $"<size=60%>P{player + 1} ACTION</size>\n{ActionCatalog.Get(ActionCatalog.RankedPick(Profile.Data, player)).Name}", 32f);

        // ---------------- The board ----------------

        private void Load()
        {
            _status.text = "LOADING THE BOARD...";
            _day.text = $"TODAY  ·  {(_mode == RankedMode.Coop ? "CO-OP" : "SOLO")}  ·  {PlayerIdentity.Name}";
            RankedClient.Board(_mode, PlayerIdentity.Id, (board, error) =>
            {
                if (this == null)
                    return; // closed meanwhile
                if (board == null)
                {
                    _status.text = error ?? "THE RANKED SERVER SAID NO";
                    return;
                }
                Show(board);
            });
        }

        private void Show(Board board)
        {
            string date = new RankedDay { Day = board.day }.Date;
            _day.text = $"{date}  ·  {RankedSession.LevelName(board.template)}  ·  {(_mode == RankedMode.Coop ? "CO-OP" : "SOLO")}";
            _status.text = board.players == 0 ? "NOBODY ON THE BOARD YET: BE THE FIRST"
                : $"{board.players} ON THE BOARD  ·  PRESS A ROW TWICE TO REPORT A CHEAT";
            BoardEntry[] entries = board.entries ?? new BoardEntry[0];
            for (int i = 0; i < Rows; i++)
            {
                bool shown = i < entries.Length;
                _rows[i].gameObject.SetActive(shown);
                _runs[i] = shown ? entries[i].run : 0;
                if (!shown)
                    continue;
                BoardEntry e = entries[i];
                _names[i] = e.name;
                bool mine = board.you != null && board.you.run != 0 && e.run == board.you.run;
                UiFactory.Label(_rows[i], Line(e, mine), 30f).alignment = TextAlignmentOptions.Left;
                for (int s = 0; s < RunScore.MaxStars; s++)
                    _stars[i, s].color = s < e.stars ? UiFactory.StarLit : UiFactory.StarDark;
            }
            _you.text = board.you != null && board.you.rank > 0
                ? $"YOU: #{board.you.rank} OF {board.players}  ·  {Score(board.you.score)}"
                : "YOU: NO RUN ON TODAY'S BOARD YET";
        }

        private static string Line(BoardEntry e, bool mine)
        {
            string crew = string.IsNullOrEmpty(e.partner) ? e.name : $"{e.name} + {e.partner}";
            if (crew.Length > 24)
                crew = $"<size=80%>{crew}</size>"; // two long names stay clear of the score
            string gold = ColorUtility.ToHtmlStringRGB(UiFactory.Gold);
            string text = $"{e.rank,3}.  {crew}<pos=70%>{Score(e.score)}<pos=85%>{Clock(e.timeMs)}";
            return mine ? $"<color=#{gold}>{text}</color>" : text;
        }

        private static string Score(int score) => score.ToString("N0", CultureInfo.InvariantCulture);

        private static string Clock(int ms) => $"{ms / 60000}:{ms / 1000 % 60:00}.{ms / 100 % 10}";

        // The first press arms the report, a second within a few seconds sends it: no dialog for a row.
        private void Report(int row)
        {
            int run = _runs[row];
            if (run == 0)
                return;
            if (_reportArmed != row || Time.unscaledTime - _reportArmedAt > 4f)
            {
                _reportArmed = row;
                _reportArmedAt = Time.unscaledTime;
                _status.text = $"REPORT {_names[row]} FOR CHEATING? PRESS AGAIN";
                return;
            }
            _reportArmed = -1;
            _status.text = "SENDING THE REPORT...";
            RankedSession.Report(run, error =>
            {
                if (this != null)
                    _status.text = error ?? "REPORTED. THANKS: WE'LL TAKE A LOOK";
            });
        }

        // ---------------- Play ----------------

        private void Play()
        {
            _play.interactable = false;
            _status.text = "GETTING TODAY'S LEVEL...";
            RankedSession.Begin(_mode, error =>
            {
                if (this == null)
                    return;
                _status.text = error;
                _play.interactable = true;
            });
        }

        // Development builds without the server: the day's daily as a ranked run, the log checked
        // on this device and the verdict in the console (RunRecorder). Nothing is sent.
        private void PlayOffline()
        {
            GameConfig config = GameConfig.Instance;
            DateTime utc = DateTime.UtcNow;
            string template = config.Campaign[Daily.TemplateIndex(utc.Date, config.Campaign.Count)].Spec.id;
            LevelSelection.PlayRanked(RankedDay.Local(utc, template, _mode));
        }
    }
}
