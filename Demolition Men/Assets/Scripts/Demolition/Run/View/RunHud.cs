using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Demolition.Run
{
    /// <summary>
    /// In-run HUD, built in code: timer and par, a hazard-striped demolition meter with
    /// the grade marks, a panel per player (score, health, free life, weapon, TNT with its
    /// button), the 3-2-1 countdown, the last call, tutorial hints, out-of-bounds warnings
    /// pinned to the left edge, pickup toasts above the players and floating score popups.
    /// Reads <see cref="RunController"/>; changes nothing.
    /// </summary>
    [RequireComponent(typeof(RunController))]
    public sealed class RunHud : MonoBehaviour
    {
        private sealed class PlayerPanel
        {
            public TextMeshProUGUI Score;
            public RectTransform HealthFill;
            public TextMeshProUGUI Life;
            public Image WeaponIcon;
            public TextMeshProUGUI Weapon;
            public Image TntIcon;
            public TextMeshProUGUI Tnt;
            public RectTransform TntGroup;
            public int LastTnt;
            public float TntPulse;
            public RectTransform Warning;
            public TextMeshProUGUI WarningCount;
            public TextMeshProUGUI Toast;
            public float ToastUntil;
        }

        private sealed class Popup
        {
            public TextMeshProUGUI Text;
            public Vector3 World;
            public float Age = float.MaxValue;
        }

        private const float PopupLifetime = 0.9f;
        private const float ToastSeconds = 2.5f;
        private const float MeterWidth = 460f;
        private static readonly Color[] PlayerColors = { new Color(1f, 0.85f, 0.3f), new Color(1f, 0.45f, 0.35f) };

        [Tooltip("Shown at the left edge while a player is out of bounds.")]
        [SerializeField] private Sprite warningSign;
        [SerializeField] private Sprite axeIcon;
        [SerializeField] private Sprite wreckingBallIcon;
        [SerializeField] private Sprite dynamiteIcon;

        private RunController _run;
        private RectTransform _root;
        private TextMeshProUGUI _timer;
        private RectTransform _meterFill;
        private TextMeshProUGUI _meterValue;
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
            TextMeshProUGUI par = UiFactory.Text(_root, "Par", top, new Vector2(260f, -26f), 24f, TextAlignmentOptions.Center, new Vector2(220f, 34f));
            par.text = "PAR " + FormatTime(_run.ParTime);
            BuildMeter(top);

            _center = UiFactory.Text(_root, "Center", new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), 140f,
                TextAlignmentOptions.Center, new Vector2(1400f, 200f), outlined: true);
            _prompt = UiFactory.Text(_root, "Prompt", new Vector2(0.5f, 0f), new Vector2(0f, 45f), 34f,
                TextAlignmentOptions.Center, new Vector2(1800f, 60f), outlined: true);
            _prompt.color = new Color(1f, 0.85f, 0.3f);

            for (int i = 0; i < _run.Players.Count; i++)
                _panels.Add(BuildPanel(i));
            for (int i = 0; i < 24; i++)
                _popups.Add(BuildPopup());

            _run.Scored += OnScored;
            _run.PromptShown += OnPrompt;
            Pickup.Collected += OnPickup;
        }

        private void OnDestroy()
        {
            Pickup.Collected -= OnPickup;
            if (_run == null)
                return;
            _run.Scored -= OnScored;
            _run.PromptShown -= OnPrompt;
        }

        // ---------------- Build ----------------

        // A warning-striped bar filling with level-wide destruction, ticked at the grades.
        private void BuildMeter(Vector2 top)
        {
            const float y = -94f, height = 24f;
            TextMeshProUGUI label = UiFactory.Text(_root, "MeterLabel", top, new Vector2(-MeterWidth * 0.5f - 110f, y + 2f), 22f,
                TextAlignmentOptions.Right, new Vector2(200f, 30f));
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.text = "DEMOLITION";

            Image frame = UiFactory.Image(_root, "Meter", top, new Vector2(0f, y), new Vector2(MeterWidth, height), new Color(0.08f, 0.08f, 0.08f));
            Image back = UiFactory.Image(frame.transform, "Back", Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.25f, 0.25f, 0.25f));
            Inset(back.rectTransform, 1f, 3f);
            Image fill = UiFactory.Image(back.transform, "Fill", Vector2.zero, Vector2.zero, Vector2.zero, Color.white);
            fill.sprite = PrimitiveSprite.Hazard();
            fill.type = Image.Type.Tiled;
            _meterFill = fill.rectTransform;
            Inset(_meterFill, 0f, 0f);

            foreach ((float at, string grade) in new[] { (RunScore.GradeB, "B"), (RunScore.GradeA, "A"), (RunScore.GradeS, "S") })
            {
                Image tick = UiFactory.Image(frame.transform, "Tick " + grade, new Vector2(at, 0.5f), Vector2.zero, new Vector2(3f, height), Color.white);
                tick.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                TextMeshProUGUI mark = UiFactory.Text(frame.transform, "Grade " + grade, new Vector2(at, 0.5f), new Vector2(10f, 0f), 18f,
                    TextAlignmentOptions.Center, new Vector2(20f, height), outlined: true);
                mark.text = grade;
            }

            _meterValue = UiFactory.Text(_root, "MeterValue", top, new Vector2(MeterWidth * 0.5f + 70f, y + 2f), 24f,
                TextAlignmentOptions.Center, new Vector2(120f, 30f));
            _meterValue.rectTransform.pivot = new Vector2(0.5f, 1f);
        }

        private static void Inset(RectTransform rt, float fill, float pad)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(fill, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        private PlayerPanel BuildPanel(int index)
        {
            bool left = index == 0;
            var anchor = new Vector2(left ? 0f : 1f, 1f);
            float side = left ? 1f : -1f;
            var align = left ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
            Color colour = PlayerColors[index % PlayerColors.Length];
            string id = $"P{index + 1}";
            var panel = new PlayerPanel();

            TextMeshProUGUI name = UiFactory.Text(_root, id, anchor, new Vector2(30f * side, -20f), 30f, align, new Vector2(400f, 42f));
            name.text = id;
            name.color = colour;
            panel.Score = UiFactory.Text(_root, id + " Score", anchor, new Vector2(100f * side, -16f), 40f, align, new Vector2(400f, 50f));

            Image back = UiFactory.Image(_root, id + " Health", anchor, new Vector2(30f * side, -72f), new Vector2(300f, 18f), new Color(0f, 0f, 0f, 0.6f));
            Image fill = UiFactory.Image(back.transform, "Fill", Vector2.zero, Vector2.zero, Vector2.zero, colour);
            panel.HealthFill = fill.rectTransform;
            panel.HealthFill.anchorMin = Vector2.zero;
            panel.HealthFill.anchorMax = Vector2.one;
            panel.HealthFill.pivot = new Vector2(left ? 0f : 1f, 0.5f);
            panel.Life = UiFactory.Text(_root, id + " Life", anchor, new Vector2(342f * side, -66f), 20f, align, new Vector2(140f, 28f));

            panel.WeaponIcon = UiFactory.Image(_root, id + " WeaponIcon", anchor, new Vector2(30f * side, -94f), new Vector2(30f, 30f), Color.white);
            panel.WeaponIcon.preserveAspect = true;
            panel.Weapon = UiFactory.Text(_root, id + " Weapon", anchor, new Vector2(68f * side, -96f), 24f, align, new Vector2(300f, 30f));

            panel.TntGroup = UiFactory.Rect(_root, id + " TNT", anchor, new Vector2(240f * side, -94f), new Vector2(150f, 30f));
            panel.TntIcon = UiFactory.Image(panel.TntGroup, "Icon", anchor, Vector2.zero, new Vector2(30f, 30f), Color.white);
            panel.TntIcon.sprite = dynamiteIcon;
            panel.TntIcon.preserveAspect = true;
            panel.Tnt = UiFactory.Text(panel.TntGroup, "Count", anchor, new Vector2(36f * side, -2f), 24f, align, new Vector2(120f, 30f));

            panel.Warning = UiFactory.Rect(_root, id + " Warning", new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(110f, 110f));
            Image sign = UiFactory.Image(panel.Warning, "Sign", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f),
                warningSign != null ? Color.white : new Color(1f, 0.8f, 0.1f));
            sign.sprite = warningSign;
            sign.preserveAspect = true;
            panel.WarningCount = UiFactory.Text(panel.Warning, "Count", new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), 56f,
                TextAlignmentOptions.Center, new Vector2(110f, 70f), outlined: true);
            panel.Warning.gameObject.SetActive(false);

            panel.Toast = UiFactory.Text(_root, id + " Toast", Vector2.zero, Vector2.zero, 30f, TextAlignmentOptions.Center,
                new Vector2(700f, 44f), outlined: true);
            panel.Toast.color = colour;
            panel.Toast.rectTransform.pivot = new Vector2(0.5f, 0f); // centred above the head
            panel.Toast.gameObject.SetActive(false);
            return panel;
        }

        private Popup BuildPopup()
        {
            TextMeshProUGUI text = UiFactory.Text(_root, "Popup", Vector2.zero, Vector2.zero, 36f, TextAlignmentOptions.Center,
                new Vector2(300f, 60f), outlined: true);
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.gameObject.SetActive(false);
            return new Popup { Text = text };
        }

        // ---------------- Update ----------------

        private void LateUpdate()
        {
            if (_root == null)
                return;
            RunState state = _run.State;

            _timer.text = FormatTime(state.Elapsed);
            float destroyed = _run.DestroyedFraction;
            _meterFill.anchorMax = new Vector2(Mathf.Clamp01(destroyed), 1f);
            _meterValue.text = $"{Mathf.FloorToInt(destroyed * 100f)}%  {RunScore.GradeFor(destroyed)}";
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
            PlayerStatus status = state.Status(index);
            bool alive = status == PlayerStatus.Alive;

            panel.Score.text = _run.Score.Points(index).ToString("N0");
            float health = player.Health.Max > 0f ? player.Health.Current / player.Health.Max : 0f;
            panel.HealthFill.anchorMax = new Vector2(index == 0 ? health : 1f, 1f);
            panel.HealthFill.anchorMin = new Vector2(index == 0 ? 0f : 1f - health, 0f);
            panel.Life.text = state.HasFreeRespawn(index) ? "+1 LIFE" : string.Empty;

            if (status == PlayerStatus.Rejoining)
                panel.Weapon.text = $"BACK IN {Mathf.CeilToInt(state.RejoinRemaining(index))}";
            else if (status == PlayerStatus.Out)
                panel.Weapon.text = "OUT - FIND A RESURRECT";
            else
                panel.Weapon.text = WeaponName(player.Combat.Weapon) +
                                    (player.Combat.WeaponUses >= 0 ? " " + player.Combat.WeaponUses : string.Empty);

            Sprite icon = alive ? WeaponIcon(player.Combat.Weapon) : null;
            panel.WeaponIcon.gameObject.SetActive(icon != null);
            panel.WeaponIcon.sprite = icon;
            ((RectTransform)panel.Weapon.transform).anchoredPosition = new Vector2((icon != null ? 68f : 30f) * (index == 0 ? 1f : -1f), -96f);

            UpdateTnt(panel, player, alive);
            UpdateToast(panel, player);

            float oob = state.OutOfBoundsRemaining(index);
            bool warn = oob >= 0f && alive;
            panel.Warning.gameObject.SetActive(warn);
            if (warn)
            {
                panel.WarningCount.text = Mathf.CeilToInt(oob).ToString();
                Vector2 local = ToCanvas(player.Transform.position + Vector3.up);
                panel.Warning.anchoredPosition = new Vector2(20f, Mathf.Clamp(local.y, -400f, 400f));
            }
        }

        // TNT count with the throw button for this player's device; pulses when it goes up.
        private void UpdateTnt(PlayerPanel panel, RunController.Player player, bool alive)
        {
            int count = player.Combat.DynamiteCount;
            panel.TntGroup.gameObject.SetActive(alive);
            if (count > panel.LastTnt)
                panel.TntPulse = 0.4f;
            panel.LastTnt = count;

            panel.Tnt.text = count > 0 ? $"x{count}  {Key(player, "X", "B", string.Empty)}" : "x0";
            float alpha = count > 0 ? 1f : 0.35f;
            panel.TntIcon.color = new Color(1f, 1f, 1f, alpha);
            panel.Tnt.alpha = alpha;

            panel.TntPulse = Mathf.Max(0f, panel.TntPulse - Time.deltaTime);
            panel.TntGroup.localScale = Vector3.one * (1f + panel.TntPulse * 0.75f);
        }

        private void OnPickup(PlayerCombat combat, PickupKind kind, int amount)
        {
            int index = combat.PlayerIndex;
            if (index < 0 || index >= _panels.Count)
                return;
            RunController.Player player = _run.Players[index];
            string switchKeys = player.Input != null && player.Input.Style == InputStyle.Touch
                ? "TAP SWAP"
                : Key(player, "Q", "LB", string.Empty) + " " + Key(player, "E", "RB", string.Empty);
            switch (kind)
            {
                case PickupKind.Dynamite:
                    _panels[index].Toast.text = $"+{amount} TNT   {Key(player, "X", "B", "TAP TNT")} TO THROW";
                    break;
                case PickupKind.Axe:
                case PickupKind.WreckingBall:
                    _panels[index].Toast.text = $"{WeaponName(kind == PickupKind.Axe ? WeaponType.Axe : WeaponType.WreckingBall)} x{amount}   {switchKeys} TO SWITCH";
                    break;
                default:
                    _panels[index].Toast.text = "PARTNER RESURRECTED!";
                    break;
            }
            _panels[index].ToastUntil = Time.time + ToastSeconds;
        }

        private void UpdateToast(PlayerPanel panel, RunController.Player player)
        {
            bool show = Time.time < panel.ToastUntil;
            panel.Toast.gameObject.SetActive(show);
            if (show)
                ((RectTransform)panel.Toast.transform).anchoredPosition =
                    ToCanvas(player.Transform.position + Vector3.up * 3.2f) + _root.rect.size * 0.5f;
        }

        /// <summary>A button prompt for whatever this player last used: [X] key, (B) pad
        /// button, or the name on the on-screen touch button.</summary>
        private static string Key(RunController.Player player, string keyboard, string gamepad, string touch)
        {
            switch (player.Input != null ? player.Input.Style : InputStyle.Keyboard)
            {
                case InputStyle.Gamepad: return $"({gamepad})";
                case InputStyle.Touch: return touch;
                default: return $"[{keyboard}]";
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

        private void OnPrompt(string message, float seconds)
        {
            _prompt.text = message;
            _promptUntil = Time.time + seconds;
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

        private Sprite WeaponIcon(WeaponType weapon)
        {
            switch (weapon)
            {
                case WeaponType.Axe: return axeIcon;
                case WeaponType.WreckingBall: return wreckingBallIcon;
                default: return null;
            }
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
