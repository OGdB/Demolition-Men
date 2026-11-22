using System;
using Demolition.Ads;
using Demolition.Run;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Demolition.Meta
{
    /// <summary>
    /// Settings overlay built in code, usable from the menu and the pause screen: volumes,
    /// screen shake, vibration (<see cref="RunHaptics"/>; a row only where a gamepad or the phone's
    /// motor can buzz), effects (lights and post-processing), quality (<see cref="Quality"/>: AUTO, LOW,
    /// HIGH), touch controls (mode, size, opacity). The volumes are sliders (drag, or left / right),
    /// every other row a pair of − / + buttons or one toggle, so it works the same with a mouse, a
    /// gamepad and a thumb.
    /// CREDITS opens <see cref="CreditsView"/>; PRIVACY (EEA/UK phones) reopens the ad consent form.
    /// Back (Esc / B) closes it; changes save immediately.
    /// </summary>
    public sealed class SettingsView : MonoBehaviour
    {
        private static readonly string[] TouchModes = { "AUTO", "ON", "OFF" };
        // Nine rows; with VIBRATION there are ten on the same plate, so they sit closer.
        private float _rowStep = 80f;
        private float _rowHeight = 74f;

        private GameObject _previousSelection;
        private System.Action _unblock;
        private Action _refresh;

        public static bool IsOpen { get; private set; }

        public static void Open(Transform parent)
        {
            if (IsOpen)
                return;
            var go = new GameObject("Settings");
            go.transform.SetParent(parent, false);
            go.AddComponent<SettingsView>();
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
            if (back && !CreditsView.IsOpen)
                Close();
        }

        private void Close() => Destroy(gameObject);

        private void Build()
        {
            GameSettings s = Profile.Data.settings;
            Canvas canvas = UiFactory.Canvas(transform, "SettingsCanvas", 40);
            _unblock = UiFactory.Modal(canvas);
            var root = (RectTransform)canvas.transform;
            Image dim = UiFactory.Image(UiFactory.Stretch(root, "Dim"), "Background", Vector2.zero, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.7f));
            dim.rectTransform.anchorMax = Vector2.one;
            dim.raycastTarget = true; // block clicks to whatever is behind
            var centre = new Vector2(0.5f, 0.5f);

            UiFactory.TitledPanel(root, new Vector2(0f, -20f), new Vector2(1300f, 1000f), "SETTINGS", 80f);

            bool vibration = PhoneMotor.Supported || (RunHaptics.PadsRumble && Gamepad.all.Count > 0);
            if (vibration)
            {
                _rowStep = 72f;
                _rowHeight = 68f;
            }

            float y = 300f;
            Selectable first = Volume(root, ref y, "VOLUME", () => s.masterVolume, v => s.masterVolume = v);
            Volume(root, ref y, "MUSIC", () => s.musicVolume, v => s.musicVolume = v);
            Toggle(root, ref y, "SCREEN SHAKE", () => s.screenShake ? "ON" : "OFF", () => s.screenShake = !s.screenShake);
            if (vibration)
                Toggle(root, ref y, "VIBRATION", () => s.vibration ? "ON" : "OFF", () => s.vibration = !s.vibration);
            Toggle(root, ref y, "EFFECTS", () => s.effects ? "ON" : "OFF", () => s.effects = !s.effects);
            Toggle(root, ref y, "QUALITY", () => QualityLabel(s), () => s.quality = (s.quality + 1) % 3);
            Stepper(root, ref y, "HUD SIZE", () => s.hudScale > 0f ? Percent(s.HudScale(Platform.IsMobile, RunHud.SafeAspect())) : "AUTO",
                d => s.hudScale = HudStep(s, d));
            Toggle(root, ref y, "TOUCH CONTROLS", () => TouchModes[Mathf.Clamp(s.touchControls, 0, 2)], () => s.touchControls = (s.touchControls + 1) % 3);
            Stepper(root, ref y, "BUTTON SIZE", () => Percent(s.touchScale), d => s.touchScale = Step(s.touchScale, d, 0.1f, 0.6f, 1.6f));
            Stepper(root, ref y, "BUTTON OPACITY", () => Percent(s.touchOpacity), d => s.touchOpacity = Step(s.touchOpacity, d, 0.1f, 0.2f, 1f));

            // PRIVACY reopens the ad consent form, shown only where the law asks for it (EEA, UK).
            IAdConsent consent = AdManager.Consent;
            bool privacy = consent != null && consent.PrivacyOptionsRequired;
            float x = privacy ? 400f : 200f;
            UiFactory.Button(root, "BACK", centre, new Vector2(-x, y - 40f), new Vector2(360f, 90f), Close);
            UiFactory.Button(root, "CREDITS", centre, new Vector2(privacy ? 0f : x, y - 40f), new Vector2(360f, 90f), () => CreditsView.Open(transform));
            if (privacy)
                UiFactory.Button(root, "PRIVACY", centre, new Vector2(x, y - 40f), new Vector2(360f, 90f), () => consent.ShowPrivacyOptions(null));
            _refresh?.Invoke();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        private Button Stepper(RectTransform root, ref float y, string label, Func<string> value, Action<int> change)
        {
            TextMeshProUGUI text = Row(root, y, label);
            var centre = new Vector2(0.5f, 0.5f);
            Button minus = UiFactory.Button(root, "-", centre, new Vector2(130f, y), new Vector2(90f, _rowHeight), () => Changed(() => change(-1)));
            TextMeshProUGUI shown = UiFactory.Text(root, label + " Value", centre, new Vector2(290f, y), 40f, TextAlignmentOptions.Center, new Vector2(200f, _rowHeight));
            UiFactory.Button(root, "+", centre, new Vector2(450f, y), new Vector2(90f, _rowHeight), () => Changed(() => change(1)));
            _refresh += () => shown.text = value();
            y -= _rowStep;
            return minus;
        }

        // A volume as a slider with its percentage: heard while it moves, saved once it stops.
        private Selectable Volume(RectTransform root, ref float y, string label, Func<float> value, Action<float> set)
        {
            Row(root, y, label);
            var centre = new Vector2(0.5f, 0.5f);
            // In the buttons' column (85 to 495): the slider, then its value.
            TextMeshProUGUI shown = UiFactory.Text(root, label + " Value", centre, new Vector2(437f, y), 40f, TextAlignmentOptions.Center, new Vector2(116f, _rowHeight));
            Slider slider = UiFactory.Slider(root, label, centre, new Vector2(230f, y), new Vector2(290f, _rowHeight), value(), v =>
            {
                set(v);
                shown.text = Percent(v);
                AppBootstrap.ApplyVolume();
            }, Profile.NotifySettingsChanged);
            _refresh += () =>
            {
                slider.SetValueWithoutNotify(value());
                shown.text = Percent(value());
            };
            y -= _rowStep;
            return slider;
        }

        private void Toggle(RectTransform root, ref float y, string label, Func<string> value, Action toggle)
        {
            Row(root, y, label);
            Button button = UiFactory.Button(root, "Toggle", new Vector2(0.5f, 0.5f), new Vector2(290f, y), new Vector2(410f, _rowHeight), () => Changed(toggle));
            TextMeshProUGUI shown = button.GetComponentInChildren<TextMeshProUGUI>();
            _refresh += () => shown.text = value();
            y -= _rowStep;
        }

        private TextMeshProUGUI Row(RectTransform root, float y, string label)
        {
            TextMeshProUGUI text = UiFactory.Text(root, label, new Vector2(0.5f, 0.5f), new Vector2(-330f, y), 40f, TextAlignmentOptions.Right, new Vector2(620f, _rowHeight));
            text.text = label;
            return text;
        }

        private void Changed(Action change)
        {
            change();
            _refresh?.Invoke();
            Profile.NotifySettingsChanged();
        }

        // AUTO (0) sits below the smallest size: "-" at 80% goes back to AUTO, "+" from AUTO
        // starts one step above the automatic size.
        // Never past what this screen fits, so the shown size is the size you get.
        private static float HudStep(GameSettings s, int direction)
        {
            float max = Mathf.Max(0.8f, GameSettings.HudScaleCap(RunHud.SafeAspect()));
            if (s.hudScale <= 0f)
                return direction < 0 ? 0f : Step(s.HudScale(Platform.IsMobile, RunHud.SafeAspect()), 1, 0.1f, 0.8f, max);
            if (direction < 0 && s.hudScale <= 0.8f)
                return 0f;
            return Step(Mathf.Min(s.hudScale, max), direction, 0.1f, 0.8f, max);
        }

        private static float Step(float value, int direction, float step, float min, float max) =>
            Mathf.Clamp(Mathf.Round((value + direction * step) * 100f) / 100f, min, max);

        // AUTO shows what the device picked.
        private static string QualityLabel(GameSettings s) => (QualityChoice)s.quality switch
        {
            QualityChoice.Low => "LOW",
            QualityChoice.High => "HIGH",
            _ => Quality.Auto == QualityTier.Low ? "AUTO: LOW" : "AUTO: HIGH",
        };

        private static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
