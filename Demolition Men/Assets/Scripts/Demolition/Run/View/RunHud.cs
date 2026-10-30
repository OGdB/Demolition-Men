using System.Collections.Generic;
using Demolition.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Demolition.Run
{
    /// <summary>
    /// In-run HUD, built in code, drawn straight over the level: timer and par, a
    /// hazard-striped demolition meter with the grade marks, a steel panel per player in its
    /// top corner (P2 mirrored: their colour's crew tag and the score, a slot for the weapon in
    /// hand with its hits left and one for the TNT with its button; a free life under it),
    /// the 3-2-1 countdown, the last call, messages, the tutorial's signs (<see cref="TutorialSigns"/>),
    /// out-of-bounds warnings pinned to the edge the player left by, health bars, pickup toasts above the
    /// players and floating score popups.
    /// Reads <see cref="RunController"/>; changes nothing.
    /// </summary>
    [RequireComponent(typeof(RunController))]
    public sealed class RunHud : MonoBehaviour
    {
        private sealed class PlayerPanel
        {
            public TextMeshProUGUI Score;
            public RectTransform HealthBar;
            public CanvasGroup HealthGroup;
            public RectTransform HealthFill;
            public float LastHealth = -1f;
            public float HealthChangedAt = float.NegativeInfinity;
            public TextMeshProUGUI Life;
            public RectTransform Kit; // the slots; out of play the status line stands there instead
            public TextMeshProUGUI Status;
            public RectTransform WeaponSlot;
            public Image WeaponIcon;
            public TextMeshProUGUI WeaponHits;
            public WeaponType LastWeapon;
            public float WeaponPulse;
            public RectTransform TntSlot;
            public Image TntIcon;
            public TextMeshProUGUI Tnt;
            public TextMeshProUGUI TntKey;
            public int LastTnt;
            public float TntPulse;
            public RectTransform ActionBar;
            public RectTransform ActionFill;
            public Image ActionFillImage;
            public TextMeshProUGUI ActionLabel;
            public bool ActionWasFull;
            public float ActionPulse;
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
        private const float HeadHeight = 1.9f; // top of the player sprite's head above its feet, world units
        private const float HealthBarGap = 14f;
        private static readonly Vector2 HealthBarSize = new Vector2(96f, 12f);
        private const float HealthIdleAlpha = 0.35f;
        private const float HealthShowSeconds = 1.5f; // fully visible this long after the health changes
        private const float HealthFadeSeconds = 0.5f;
        private static readonly Color[] PlayerColors = { new Color(1f, 0.85f, 0.3f), new Color(1f, 0.45f, 0.35f) };
        private static readonly Color OverPar = new Color(1f, 0.4f, 0.3f);
        private static readonly Vector2 PanelSize = new Vector2(340f, 172f);
        private const float ActionBarHeight = 32f;
        private static readonly Color ActionCharging = new Color(0.85f, 0.6f, 0.15f);
        private const float PanelMargin = 20f;
        // Big enough that the weapon sprites (up to 45 px tall) draw at full size or more at
        // 1080p: shrunk, point filtering drops the wrecking ball's thin handle.
        private const float SlotSize = 56f;
        private const float TntSlotX = 140f; // from the panel's outer edge, like every x in it
        private const int LowHits = 3; // hits left at which the weapon's count turns red
        private const float WarningSize = 110f;
        private const float WarningInset = 20f; // from the screen's edge
        private static readonly Color PlateTint = new Color(0.15f, 0.16f, 0.19f, 0.88f);
        // Light steel: the icons' dark outlines and the wooden handles read against it.
        private static readonly Color SlotTint = new Color(0.56f, 0.6f, 0.65f, 1f);
        private static readonly Color LifeColor = new Color(0.55f, 0.95f, 0.45f);

        [Tooltip("Shown at the screen's edge while a player is out of bounds.")]
        [SerializeField] private Sprite warningSign;
        [SerializeField] private Sprite axeIcon;
        [SerializeField] private Sprite wreckingBallIcon;
        [SerializeField] private Sprite dynamiteIcon;

        private RunController _run;
        private CanvasScaler _scaler;
        private Vector2Int _appliedFor;
        private RectTransform _root;
        private TextMeshProUGUI _timer;
        private TextMeshProUGUI _par;
        private RectTransform _meterFill;
        private TextMeshProUGUI _meterValue;
        private readonly Image[] _meterStars = new Image[RunScore.MaxStars];
        private TextMeshProUGUI _center;
        private TextMeshProUGUI _prompt;
        private float _promptUntil;
        private TutorialSigns _signs;
        private readonly List<PlayerPanel> _panels = new List<PlayerPanel>();
        private readonly List<Popup> _popups = new List<Popup>();

        private void Awake()
        {
            _run = GetComponent<RunController>();
        }

        private void Start()
        {
            Canvas canvas = UiFactory.Canvas(transform, "HUD", 10);
            _scaler = canvas.GetComponent<CanvasScaler>();
            ApplyScale();
            Profile.SettingsChanged += ApplyScale;
            _root = UiFactory.SafeArea(canvas.transform);
            if (TutorialPrompt.All.Count > 0)
                _signs = new TutorialSigns(_root, TutorialPrompt.All); // first: the rest of the HUD draws over them

            var top = new Vector2(0.5f, 1f);
            _timer = UiFactory.Text(_root, "Timer", top, new Vector2(0f, -10f), 56f, TextAlignmentOptions.Center, outlined: true);
            _par = Thin(UiFactory.Text(_root, "Par", top, new Vector2(260f, -26f), 24f, TextAlignmentOptions.Center, new Vector2(220f, 34f), outlined: true));
            _par.text = "PAR " + FormatTime(_run.ParTime);
            BuildMeter(top);

            _center = UiFactory.Text(_root, "Center", new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), 140f,
                TextAlignmentOptions.Center, new Vector2(1400f, 200f), outlined: true);
            _prompt = UiFactory.Text(_root, "Prompt", new Vector2(0.5f, 0f), new Vector2(0f, 45f), 34f,
                TextAlignmentOptions.Center, new Vector2(1800f, 60f), outlined: true);
            _prompt.color = new Color(1f, 0.85f, 0.3f);
            _prompt.textWrappingMode = TextWrappingModes.Normal; // long hints wrap on narrow screens

            for (int i = 0; i < _run.Players.Count; i++)
                _panels.Add(BuildPanel(i));
            for (int i = 0; i < 24; i++)
                _popups.Add(BuildPopup());

            _run.Scored += OnScored;
            _run.PromptShown += OnPrompt;
            Pickup.Collected += OnPickup;
        }

        // The HUD size setting, capped by the safe area's shape (GameSettings.HudScale).
        private void ApplyScale()
        {
            if (_scaler == null)
                return;
            _appliedFor = new Vector2Int(Screen.width, Screen.height);
            float scale = Profile.Data.settings.HudScale(Platform.IsMobile, SafeAspect());
            _scaler.referenceResolution = new Vector2(1920f, 1080f) / Mathf.Max(0.5f, scale);
        }

        /// <summary>Screen pixels from the top of the safe area down to the bottom of P2's panel
        /// and life line in the top-right corner; 0 with one player.</summary>
        public float TopRightDepth => _root != null && _panels.Count > 1
            ? (PanelMargin + PanelSize.y + 34f) * _root.lossyScale.y
            : 0f;

        public static float SafeAspect()
        {
            Rect safe = Screen.safeArea;
            return safe.height > 0f ? safe.width / safe.height : 16f / 9f;
        }

        private void OnDestroy()
        {
            Profile.SettingsChanged -= ApplyScale;
            Pickup.Collected -= OnPickup;
            if (_run == null)
                return;
            _run.Scored -= OnScored;
            _run.PromptShown -= OnPrompt;
        }

        // ---------------- Build ----------------

        // A warning-striped bar filling with level-wide destruction, ticked with a star at each
        // grade; a star lights once the meter passes it.
        private void BuildMeter(Vector2 top)
        {
            const float y = -94f, height = 24f;
            TextMeshProUGUI label = Thin(UiFactory.Text(_root, "MeterLabel", top, new Vector2(-MeterWidth * 0.5f - 110f, y + 2f), 22f,
                TextAlignmentOptions.Right, new Vector2(200f, 30f), outlined: true));
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

            for (int star = 0; star < _meterStars.Length; star++)
            {
                float at = RunScore.StarThreshold(star);
                Image tick = UiFactory.Image(frame.transform, "Tick " + star, new Vector2(at, 0.5f), Vector2.zero, new Vector2(3f, height), Color.white);
                tick.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _meterStars[star] = UiFactory.Star(frame.transform, "Star " + star, new Vector2(at, 0.5f), new Vector2(0f, -height * 0.5f - 6f), 30f, false);
                _meterStars[star].rectTransform.pivot = new Vector2(0.5f, 1f);
            }

            _meterValue = Thin(UiFactory.Text(_root, "MeterValue", top, new Vector2(MeterWidth * 0.5f + 70f, y + 2f), 24f,
                TextAlignmentOptions.Center, new Vector2(120f, 30f), outlined: true));
            _meterValue.rectTransform.pivot = new Vector2(0.5f, 1f);
        }

        // Small text: the full outline would eat into its strokes.
        private static TextMeshProUGUI Thin(TextMeshProUGUI text)
        {
            if (UiFactory.ThinOutline != null)
                text.fontSharedMaterial = UiFactory.ThinOutline;
            return text;
        }

        private static void Inset(RectTransform rt, float fill, float pad)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(fill, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        // A steel plate in the player's top corner; every x in it runs inwards from that corner.
        private PlayerPanel BuildPanel(int index)
        {
            bool left = index == 0;
            var anchor = new Vector2(left ? 0f : 1f, 1f);
            float side = left ? 1f : -1f;
            var align = left ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
            Color colour = PlayerColors[index % PlayerColors.Length];
            string id = $"P{index + 1}";
            var panel = new PlayerPanel();

            Transform plate = Plate(_root, id + " Panel", anchor, new Vector2(PanelMargin * side, -PanelMargin), PanelSize, PlateTint).transform;
            Image tag = Plate(plate, "Tag", anchor, new Vector2(10f * side, -10f), new Vector2(72f, 48f), colour);
            TextMeshProUGUI name = UiFactory.Text(tag.transform, "Name", new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), 30f,
                TextAlignmentOptions.Center, tag.rectTransform.sizeDelta);
            name.text = id;
            name.color = UiFactory.Ink;
            panel.Score = UiFactory.Text(plate, "Score", anchor, new Vector2(96f * side, -10f), 40f, align, new Vector2(230f, 48f), outlined: true);

            panel.Kit = UiFactory.Rect(plate, "Kit", anchor, new Vector2(10f * side, -66f), new Vector2(PanelSize.x - 20f, SlotSize));
            panel.WeaponSlot = Slot(panel.Kit, "Weapon", anchor, 0f, side, out panel.WeaponIcon);
            panel.WeaponHits = UiFactory.Text(panel.Kit, "Hits", anchor, new Vector2((SlotSize + 8f) * side, 0f), 30f, align,
                new Vector2(TntSlotX - SlotSize - 12f, SlotSize), outlined: true);
            panel.TntSlot = Slot(panel.Kit, "TNT", anchor, TntSlotX, side, out panel.TntIcon);
            panel.TntIcon.sprite = dynamiteIcon;
            panel.Tnt = UiFactory.Text(panel.Kit, "Count", anchor, new Vector2((TntSlotX + SlotSize + 8f) * side, 0f), 30f, align,
                new Vector2(56f, SlotSize), outlined: true);
            panel.TntKey = UiFactory.Text(panel.Kit, "Key", anchor, new Vector2((TntSlotX + SlotSize + 62f) * side, 0f), 22f, align,
                new Vector2(70f, SlotSize));
            BuildActionBar(panel, anchor);
            panel.Status = Thin(UiFactory.Text(plate, "Status", anchor, new Vector2(14f * side, -66f), 24f, align,
                new Vector2(PanelSize.x - 28f, SlotSize), outlined: true));
            panel.Status.enableAutoSizing = true; // OUT - FIND A RESURRECT fits the plate
            panel.Status.fontSizeMin = 16f;
            panel.Status.fontSizeMax = 24f;

            panel.Life = UiFactory.Text(_root, id + " Life", anchor, new Vector2((PanelMargin + 10f) * side, -(PanelMargin + PanelSize.y + 4f)), 28f,
                left ? TextAlignmentOptions.Left : TextAlignmentOptions.Right, new Vector2(200f, 36f), outlined: true);
            panel.Life.color = LifeColor;

            panel.Warning = UiFactory.Rect(_root, id + " Warning", new Vector2(0f, 0.5f), new Vector2(WarningInset, 0f), new Vector2(WarningSize, WarningSize));
            Image sign = UiFactory.Image(panel.Warning, "Sign", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WarningSize, WarningSize),
                warningSign != null ? Color.white : new Color(1f, 0.8f, 0.1f));
            sign.sprite = warningSign;
            sign.preserveAspect = true;
            panel.WarningCount = UiFactory.Text(panel.Warning, "Count", new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), 56f,
                TextAlignmentOptions.Center, new Vector2(110f, 70f), outlined: true);
            panel.Warning.gameObject.SetActive(false);

            Image bar = UiFactory.Image(_root, id + " Health", Vector2.zero, Vector2.zero, HealthBarSize, new Color(0f, 0f, 0f, 0.7f));
            panel.HealthBar = bar.rectTransform;
            panel.HealthBar.pivot = new Vector2(0.5f, 0f); // centred above the head
            panel.HealthGroup = bar.gameObject.AddComponent<CanvasGroup>();
            panel.HealthGroup.alpha = HealthIdleAlpha;
            Image fill = UiFactory.Image(bar.transform, "Fill", Vector2.zero, Vector2.zero, Vector2.zero, colour);
            panel.HealthFill = fill.rectTransform;
            Inset(panel.HealthFill, 1f, 2f);

            panel.Toast = UiFactory.Text(_root, id + " Toast", Vector2.zero, Vector2.zero, 30f, TextAlignmentOptions.Center,
                new Vector2(700f, 44f), outlined: true);
            panel.Toast.color = colour;
            panel.Toast.rectTransform.pivot = new Vector2(0.5f, 0f); // centred above the head
            panel.Toast.gameObject.SetActive(false);
            return panel;
        }

        // Under the slots: the action meter, filling with this player's demolition, the action's
        // name on it; full, it glows and names the button.
        private static void BuildActionBar(PlayerPanel panel, Vector2 anchor)
        {
            Image bar = Plate(panel.Kit, "Action", anchor, new Vector2(0f, -(SlotSize + 10f)), new Vector2(PanelSize.x - 20f, ActionBarHeight),
                new Color(0.08f, 0.08f, 0.1f, 0.9f));
            panel.ActionBar = bar.rectTransform;
            panel.ActionBar.pivot = new Vector2(0.5f, 0.5f); // pulses in place
            panel.ActionBar.anchoredPosition += new Vector2((PanelSize.x - 20f) * 0.5f * (anchor.x < 0.5f ? 1f : -1f), -ActionBarHeight * 0.5f);
            Image fill = UiFactory.Image(bar.transform, "Fill", Vector2.zero, Vector2.zero, Vector2.zero, ActionCharging);
            panel.ActionFillImage = fill;
            panel.ActionFill = fill.rectTransform;
            Inset(panel.ActionFill, 0f, 4f);
            panel.ActionLabel = Thin(UiFactory.Text(bar.transform, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, 22f,
                TextAlignmentOptions.Center, new Vector2(PanelSize.x - 30f, ActionBarHeight), outlined: true));
            panel.ActionLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            panel.ActionLabel.enableAutoSizing = true;
            panel.ActionLabel.fontSizeMin = 14f;
            panel.ActionLabel.fontSizeMax = 22f;
        }

        // The UI's riveted plate with its rim at half the menus' size: a HUD panel is a fraction of a menu's.
        private static Image Plate(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color tint)
        {
            Image plate = UiFactory.Image(parent, name, anchor, position, size, tint);
            plate.sprite = UiFactory.PlateSprite;
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = 2f;
            return plate;
        }

        // A light steel slot holding an icon, <paramref name="x"/> in from the corner; pivoted in
        // its middle so it pulses in place.
        private static RectTransform Slot(Transform parent, string name, Vector2 anchor, float x, float side, out Image icon)
        {
            RectTransform slot = Plate(parent, name, anchor, Vector2.zero, new Vector2(SlotSize, SlotSize), SlotTint).rectTransform;
            slot.pivot = new Vector2(0.5f, 0.5f);
            slot.anchoredPosition = new Vector2((x + SlotSize * 0.5f) * side, -SlotSize * 0.5f);
            icon = UiFactory.Image(slot, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SlotSize - 6f, SlotSize - 6f), Color.white);
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            icon.preserveAspect = true;
            return slot;
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
            if (_root == null || !_run.enabled) // a run switched off as an online session ends: its players are going
                return;
            if (_appliedFor.x != Screen.width || _appliedFor.y != Screen.height)
                ApplyScale(); // resized window, rotated phone
            RunState state = _run.State;

            _timer.text = FormatTime(state.Elapsed);
            bool withinPar = RunScore.WithinPar(state.Elapsed, _run.ParTime);
            _par.color = withinPar ? Color.white : OverPar; // past par the stars can't be rainbow any more
            float destroyed = _run.DestroyedFraction;
            _meterFill.anchorMax = new Vector2(Mathf.Clamp01(destroyed), 1f);
            _meterValue.text = $"{Mathf.FloorToInt(destroyed * 100f)}%";
            for (int star = 0; star < _meterStars.Length; star++)
            {
                bool lit = destroyed >= RunScore.StarThreshold(star);
                RainbowTint.Set(_meterStars[star], lit && withinPar, star * RainbowTint.StarPhase);
                if (!(lit && withinPar))
                    _meterStars[star].color = lit ? UiFactory.StarLit : UiFactory.StarDark;
            }
            _center.text = CenterText(state);
            _prompt.gameObject.SetActive(Time.time < _promptUntil && !state.IsOver);
            _prompt.rectTransform.sizeDelta = new Vector2(Mathf.Min(1800f, _root.rect.width - 80f), 100f);
            _signs?.Tick(_run, world => ToCanvas(world) + _root.rect.size * 0.5f);

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
            UpdateHealthBar(panel, player, alive && !state.IsOver);
            panel.Life.text = state.HasFreeRespawn(index) ? "+1 LIFE" : string.Empty;

            panel.Kit.gameObject.SetActive(alive);
            panel.Status.gameObject.SetActive(!alive);
            if (status == PlayerStatus.Rejoining)
                panel.Status.text = $"BACK IN {Mathf.CeilToInt(state.RejoinRemaining(index))}";
            else if (status == PlayerStatus.Out)
                panel.Status.text = state.HasLeft(index) ? "LEFT THE GAME" : "OUT - FIND A RESURRECT";
            if (alive)
            {
                UpdateWeapon(panel, player);
                UpdateTnt(panel, player);
                UpdateAction(panel, player);
            }
            UpdateToast(panel, player);

            float oob = state.OutOfBoundsRemaining(index);
            bool warn = oob >= 0f && alive;
            panel.Warning.gameObject.SetActive(warn);
            if (warn)
            {
                panel.WarningCount.text = Mathf.CeilToInt(oob).ToString();
                Vector2 local = ToCanvas(player.Transform.position + Vector3.up);
                // On the edge the player left by: the left one at their height, the bottom one
                // under them (down a pit), the corner for both.
                float width = _root.rect.width;
                float x = Mathf.Clamp(local.x + width * 0.5f - WarningSize * 0.5f, WarningInset, Mathf.Max(WarningInset, width - WarningSize - WarningInset));
                float half = _root.rect.height * 0.5f;
                float below = Mathf.Max(0f, half - 110f); // sign and count stay on screen
                float above = half - (PanelMargin + PanelSize.y + 90f); // and under P1's panel
                panel.Warning.anchoredPosition = new Vector2(x, Mathf.Clamp(local.y, -below, Mathf.Max(-below, above)));
            }
        }

        // Above the head: faint while nothing happens, fully visible for a moment after the
        // health changes (a hit, a revive), then fading back.
        private void UpdateHealthBar(PlayerPanel panel, RunController.Player player, bool show)
        {
            float current = player.Health.Current;
            if (current != panel.LastHealth)
            {
                if (panel.LastHealth >= 0f)
                    panel.HealthChangedAt = Time.time;
                panel.LastHealth = current;
            }

            panel.HealthBar.gameObject.SetActive(show);
            if (!show)
                return;
            float max = player.Health.Max;
            panel.HealthFill.anchorMax = new Vector2(max > 0f ? Mathf.Clamp01(current / max) : 0f, 1f);
            float fade = Mathf.Clamp01((Time.time - panel.HealthChangedAt - HealthShowSeconds) / HealthFadeSeconds);
            panel.HealthGroup.alpha = Mathf.Lerp(1f, HealthIdleAlpha, fade);
            panel.HealthBar.anchoredPosition = AboveHead(player) + _root.rect.size * 0.5f;
        }

        /// <summary>Where the health bar sits: just above the player's head, relative to the canvas centre.</summary>
        private Vector2 AboveHead(RunController.Player player) =>
            ToCanvas(player.Transform.position + Vector3.up * HeadHeight) + new Vector2(0f, HealthBarGap);

        // The weapon in hand and its hits left, red once it's about to break; pulses on a change.
        private void UpdateWeapon(PlayerPanel panel, RunController.Player player)
        {
            WeaponType weapon = player.Combat.Weapon;
            if (weapon != panel.LastWeapon)
                panel.WeaponPulse = 0.3f;
            panel.LastWeapon = weapon;

            Sprite icon = WeaponIcon(weapon);
            panel.WeaponIcon.sprite = icon;
            panel.WeaponIcon.enabled = icon != null;
            int uses = player.Combat.WeaponUses;
            panel.WeaponHits.text = uses >= 0 ? "x" + uses : string.Empty; // fists never run out
            panel.WeaponHits.color = uses >= 0 && uses <= LowHits ? OverPar : Color.white;

            panel.WeaponPulse = Mathf.Max(0f, panel.WeaponPulse - Time.deltaTime);
            panel.WeaponSlot.localScale = Vector3.one * (1f + panel.WeaponPulse * 0.75f);
        }

        // TNT count with the throw button for this player's device; pulses when it goes up.
        private void UpdateTnt(PlayerPanel panel, RunController.Player player)
        {
            int count = player.Combat.DynamiteCount;
            if (count > panel.LastTnt)
                panel.TntPulse = 0.4f;
            panel.LastTnt = count;

            panel.Tnt.text = "x" + count;
            panel.TntKey.text = count > 0 ? Key(player, "X", "B", string.Empty) : string.Empty;
            float alpha = count > 0 ? 1f : 0.35f;
            panel.TntIcon.color = new Color(1f, 1f, 1f, alpha);
            panel.Tnt.alpha = alpha;

            panel.TntPulse = Mathf.Max(0f, panel.TntPulse - Time.deltaTime);
            panel.TntSlot.localScale = Vector3.one * (1f + panel.TntPulse * 0.75f);
        }

        // The action meter; full and ready, it flashes and shows this player's button.
        private void UpdateAction(PlayerPanel panel, RunController.Player player)
        {
            Actions.ActionUser action = player.Action;
            panel.ActionBar.gameObject.SetActive(action != null);
            if (action == null)
                return;
            bool ready = action.Meter.Full && !action.Busy;
            if (ready && !panel.ActionWasFull)
                panel.ActionPulse = 0.45f;
            panel.ActionWasFull = ready;
            panel.ActionFill.anchorMax = new Vector2(Mathf.Clamp01(action.Meter.Fraction), 1f);
            panel.ActionFillImage.color = ready
                ? Color.Lerp(UiFactory.Gold, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 9f))
                : ActionCharging;
            string key = ready ? Key(player, "C", "Y", "TAP ACTION") : string.Empty;
            panel.ActionLabel.text = key.Length > 0 ? $"{action.Info.Name}  {key}" : action.Info.Name;
            panel.ActionLabel.color = ready ? UiFactory.Ink : Color.white;
            panel.ActionPulse = Mathf.Max(0f, panel.ActionPulse - Time.deltaTime);
            panel.ActionBar.localScale = Vector3.one * (1f + panel.ActionPulse * 0.4f);
        }

        private void OnPickup(PlayerCombat combat, PickupKind kind, int amount)
        {
            int index = combat.PlayerIndex;
            if (index < 0 || index >= _panels.Count)
                return;
            RunController.Player player = _run.Players[index];
            bool hints = !player.Health.IsProxy; // the other device's player: its controls aren't ours to show
            string switchKeys = player.Input != null && player.Input.Style == InputStyle.Touch
                ? "TAP SWAP"
                : Key(player, "Q", "LB", string.Empty) + " " + Key(player, "E", "RB", string.Empty);
            switch (kind)
            {
                case PickupKind.Dynamite:
                    _panels[index].Toast.text = $"+{amount} TNT" + (hints ? $"   {Key(player, "X", "B", "TAP TNT")} TO THROW" : string.Empty);
                    break;
                case PickupKind.Axe:
                case PickupKind.WreckingBall:
                    _panels[index].Toast.text = $"{WeaponName(kind == PickupKind.Axe ? WeaponType.Axe : WeaponType.WreckingBall)} x{amount}" +
                                                (hints ? $"   {switchKeys} TO SWITCH" : string.Empty);
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
            if (!show)
                return;
            Vector2 local = ToCanvas(player.Transform.position + Vector3.up * 3.2f);
            local.y = Mathf.Max(local.y, AboveHead(player).y + HealthBarSize.y + 6f); // clear of the health bar when zoomed out
            ((RectTransform)panel.Toast.transform).anchoredPosition = local + _root.rect.size * 0.5f;
        }

        /// <summary>A button prompt for whatever this player last used: [X] key, (B) pad
        /// button, or the name on the on-screen touch button.</summary>
        private static string Key(RunController.Player player, string keyboard, string gamepad, string touch)
        {
            if (player.Health.IsProxy)
                return string.Empty;
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
                default: return UiFactory.FistSprite;
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
