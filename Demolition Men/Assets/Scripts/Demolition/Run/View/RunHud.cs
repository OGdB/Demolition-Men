using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Demolition.Run
{
    /// <summary>
    /// In-run HUD, built in code: timer and par, destruction %, a panel per player
    /// (score, health, weapon, dynamite, rejoin state), the 3-2-1 countdown, the last
    /// call, out-of-bounds warnings pinned to the left edge at each player's height, and
    /// floating score popups. Reads <see cref="RunController"/>; changes nothing.
    /// </summary>
    [RequireComponent(typeof(RunController))]
    public sealed class RunHud : MonoBehaviour
    {
        private sealed class PlayerPanel
        {
            public TextMeshProUGUI Score;
            public RectTransform HealthFill;
            public TextMeshProUGUI Status;
            public RectTransform Warning;
            public TextMeshProUGUI WarningCount;
        }

        private sealed class Popup
        {
            public TextMeshProUGUI Text;
            public Vector3 World;
            public float Age = float.MaxValue;
        }

        private const float PopupLifetime = 0.9f;
        private static readonly Color[] PlayerColors = { new Color(1f, 0.85f, 0.3f), new Color(1f, 0.45f, 0.35f) };

        [Tooltip("Shown at the left edge while a player is out of bounds.")]
        [SerializeField] private Sprite warningSign;

        private RunController _run;
        private RectTransform _root;
        private TextMeshProUGUI _timer;
        private TextMeshProUGUI _par;
        private TextMeshProUGUI _destruction;
        private TextMeshProUGUI _center;
        private TextMeshProUGUI _prompt;
        private float _promptUntil;
        private readonly List<PlayerPanel> _panels = new List<PlayerPanel>();
        private readonly List<Popup> _popups = new List<Popup>();

        private void Awake()
        {
            _run = GetComponent<RunController>();
        }

        private void Start()
        {
            Canvas canvas = UiFactory.Canvas(transform, "HUD", 10);
            _root = (RectTransform)canvas.transform;

            var top = new Vector2(0.5f, 1f);
            _timer = UiFactory.Text(_root, "Timer", top, new Vector2(0f, -10f), 56f, TextAlignmentOptions.Center);
            _par = UiFactory.Text(_root, "Par", top, new Vector2(-150f, -85f), 26f, TextAlignmentOptions.Center, new Vector2(280f, 36f));
            _par.text = "PAR " + FormatTime(_run.ParTime);
            _destruction = UiFactory.Text(_root, "Destruction", top, new Vector2(150f, -85f), 26f, TextAlignmentOptions.Center, new Vector2(280f, 36f));
            _center = UiFactory.Text(_root, "Center", new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), 140f,
                TextAlignmentOptions.Center, new Vector2(1400f, 200f));

            _prompt = UiFactory.Text(_root, "Prompt", new Vector2(0.5f, 0f), new Vector2(0f, 45f), 34f,
                TextAlignmentOptions.Center, new Vector2(1800f, 60f));
            _prompt.color = new Color(1f, 0.85f, 0.3f);

            for (int i = 0; i < _run.Players.Count; i++)
                _panels.Add(BuildPanel(i));
            for (int i = 0; i < 24; i++)
                _popups.Add(BuildPopup());

            _run.Scored += OnScored;
            _run.PromptShown += OnPrompt;
        }

        private void OnDestroy()
        {
            if (_run == null)
                return;
            _run.Scored -= OnScored;
            _run.PromptShown -= OnPrompt;
        }

        private void OnPrompt(string message, float seconds)
        {
            _prompt.text = message;
            _promptUntil = Time.time + seconds;
        }

        private PlayerPanel BuildPanel(int index)
        {
            bool left = index == 0;
            var anchor = new Vector2(left ? 0f : 1f, 1f);
            float side = left ? 1f : -1f;
            var align = left ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
            var panel = new PlayerPanel();

            TextMeshProUGUI name = UiFactory.Text(_root, $"P{index + 1}", anchor, new Vector2(30f * side, -20f), 30f, align, new Vector2(400f, 42f));
            name.text = $"P{index + 1}";
            name.color = PlayerColors[index % PlayerColors.Length];
            panel.Score = UiFactory.Text(_root, $"P{index + 1} Score", anchor, new Vector2(100f * side, -16f), 40f, align, new Vector2(400f, 50f));

            Image back = UiFactory.Image(_root, $"P{index + 1} Health", anchor, new Vector2(30f * side, -72f), new Vector2(300f, 18f), new Color(0f, 0f, 0f, 0.6f));
            Image fill = UiFactory.Image(back.transform, "Fill", Vector2.zero, Vector2.zero, Vector2.zero, PlayerColors[index % PlayerColors.Length]);
            panel.HealthFill = fill.rectTransform;
            panel.HealthFill.anchorMin = Vector2.zero;
            panel.HealthFill.anchorMax = Vector2.one;
            panel.HealthFill.pivot = new Vector2(left ? 0f : 1f, 0.5f);

            panel.Status = UiFactory.Text(_root, $"P{index + 1} Status", anchor, new Vector2(30f * side, -98f), 24f, align, new Vector2(500f, 34f));

            panel.Warning = UiFactory.Rect(_root, $"P{index + 1} Warning", new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(110f, 110f));
            Image sign = UiFactory.Image(panel.Warning, "Sign", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f),
                warningSign != null ? Color.white : new Color(1f, 0.8f, 0.1f));
            sign.sprite = warningSign;
            sign.preserveAspect = true;
            panel.Warning.pivot = new Vector2(0f, 0.5f);
            panel.WarningCount = UiFactory.Text(panel.Warning, "Count", new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), 56f,
                TextAlignmentOptions.Center, new Vector2(110f, 70f));
            panel.Warning.gameObject.SetActive(false);
            return panel;
        }

        private Popup BuildPopup()
        {
            TextMeshProUGUI text = UiFactory.Text(_root, "Popup", Vector2.zero, Vector2.zero, 36f, TextAlignmentOptions.Center, new Vector2(300f, 60f));
            text.gameObject.SetActive(false);
            return new Popup { Text = text };
        }

        private void LateUpdate()
        {
            if (_root == null)
                return;
            RunState state = _run.State;

            _timer.text = FormatTime(state.Elapsed);
            _destruction.text = $"DESTRUCTION {Mathf.FloorToInt(_run.DestroyedFraction * 100f)}%";
            _center.text = CenterText(state);
            _prompt.gameObject.SetActive(Time.time < _promptUntil && !state.IsOver);

            for (int i = 0; i < _panels.Count; i++)
                UpdatePanel(i, state);
            UpdatePopups();
        }

        private static string CenterText(RunState state)
        {
            switch (state.Phase)
            {
                case RunPhase.Countdown:
                    return Mathf.CeilToInt(state.Countdown).ToString();
                case RunPhase.Playing:
                    if (state.Elapsed < 0.8f)
                        return "GO!";
                    if (state.LastCallRemaining >= 0f)
                        return $"LAST CALL {Mathf.CeilToInt(state.LastCallRemaining)}";
                    return string.Empty;
                default:
                    return string.Empty;
            }
        }

        private void UpdatePanel(int index, RunState state)
        {
            PlayerPanel panel = _panels[index];
            RunController.Player player = _run.Players[index];

            panel.Score.text = _run.Score.Points(index).ToString("N0");
            float health = player.Health.Max > 0f ? player.Health.Current / player.Health.Max : 0f;
            panel.HealthFill.anchorMax = new Vector2(index == 0 ? health : 1f, 1f);
            panel.HealthFill.anchorMin = new Vector2(index == 0 ? 0f : 1f - health, 0f);

            switch (state.Status(index))
            {
                case PlayerStatus.Rejoining:
                    panel.Status.text = $"BACK IN {Mathf.CeilToInt(state.RejoinRemaining(index))}";
                    break;
                case PlayerStatus.Out:
                    panel.Status.text = "OUT - FIND A RESURRECT";
                    break;
                default:
                    string respawn = state.HasFreeRespawn(index) ? "  +1 LIFE" : string.Empty;
                    int uses = player.Combat.WeaponUses;
                    string weapon = WeaponName(player.Combat.Weapon) + (uses >= 0 ? " " + uses : string.Empty);
                    panel.Status.text = $"{weapon}  TNT x{player.Combat.DynamiteCount}{respawn}";
                    break;
            }

            float oob = state.OutOfBoundsRemaining(index);
            bool warn = oob >= 0f && state.Status(index) == PlayerStatus.Alive;
            panel.Warning.gameObject.SetActive(warn);
            if (warn)
            {
                panel.WarningCount.text = Mathf.CeilToInt(oob).ToString();
                Vector2 local = ToCanvas(player.Transform.position + Vector3.up);
                panel.Warning.anchoredPosition = new Vector2(20f, Mathf.Clamp(local.y, -400f, 400f));
            }
        }

        private void OnScored(int player, int points, Vector3 world)
        {
            if (points <= 0)
                return;
            Popup popup = _popups[0];
            for (int i = 1; i < _popups.Count; i++)
                if (_popups[i].Age > popup.Age)
                    popup = _popups[i];
            popup.Age = 0f;
            popup.World = world;
            popup.Text.text = "+" + points;
            popup.Text.fontSize = Mathf.Clamp(28f + points * 0.05f, 28f, 90f);
            popup.Text.color = player >= 0 ? PlayerColors[player % PlayerColors.Length] : Color.white;
            popup.Text.gameObject.SetActive(true);
        }

        private void UpdatePopups()
        {
            foreach (Popup popup in _popups)
            {
                if (popup.Age >= PopupLifetime)
                {
                    if (popup.Text.gameObject.activeSelf)
                        popup.Text.gameObject.SetActive(false);
                    continue;
                }
                popup.Age += Time.deltaTime;
                float t = popup.Age / PopupLifetime;
                ((RectTransform)popup.Text.transform).anchoredPosition =
                    ToCanvas(popup.World) + new Vector2(0f, 80f * t) + _root.rect.size * 0.5f;
                Color c = popup.Text.color;
                c.a = 1f - t * t;
                popup.Text.color = c;
            }
        }

        /// <summary>World point → position relative to the canvas centre.</summary>
        private Vector2 ToCanvas(Vector3 world)
        {
            Camera cam = _run.Camera;
            if (cam == null)
                return Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, cam.WorldToScreenPoint(world), null, out Vector2 local);
            return local;
        }

        public static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            return $"{minutes}:{seconds - minutes * 60f:00.0}";
        }

        private static string WeaponName(WeaponType weapon)
        {
            switch (weapon)
            {
                case WeaponType.Axe: return "AXE";
                case WeaponType.WreckingBall: return "WRECKING BALL";
                default: return "FISTS";
            }
        }
    }
}
