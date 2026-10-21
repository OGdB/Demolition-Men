using System;
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
    /// screen shake, effects (lights and post-processing), touch controls (mode, size, opacity). Every row is a pair of − / +
    /// buttons (or one toggle), so it works the same with a mouse, a gamepad and a thumb.
    /// CREDITS opens <see cref="CreditsView"/>. Back (Esc / B) closes it; changes save immediately.
    /// </summary>
    public sealed class SettingsView : MonoBehaviour
    {
        private static readonly string[] TouchModes = { "AUTO", "ON", "OFF" };
        private const float RowStep = 86f;

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

            float y = 300f;
            Button first = Stepper(root, ref y, "VOLUME", () => Percent(s.masterVolume), d => s.masterVolume = Step(s.masterVolume, d, 0.1f, 0f, 1f));
            Stepper(root, ref y, "MUSIC", () => Percent(s.musicVolume), d => s.musicVolume = Step(s.musicVolume, d, 0.1f, 0f, 1f));
            Toggle(root, ref y, "SCREEN SHAKE", () => s.screenShake ? "ON" : "OFF", () => s.screenShake = !s.screenShake);
            Toggle(root, ref y, "EFFECTS", () => s.effects ? "ON" : "OFF", () => s.effects = !s.effects);
            Stepper(root, ref y, "HUD SIZE", () => s.hudScale > 0f ? Percent(s.HudScale(Platform.IsMobile, RunHud.SafeAspect())) : "AUTO",
                d => s.hudScale = HudStep(s, d));
            Toggle(root, ref y, "TOUCH CONTROLS", () => TouchModes[Mathf.Clamp(s.touchControls, 0, 2)], () => s.touchControls = (s.touchControls + 1) % 3);
            Stepper(root, ref y, "BUTTON SIZE", () => Percent(s.touchScale), d => s.touchScale = Step(s.touchScale, d, 0.1f, 0.6f, 1.6f));
            Stepper(root, ref y, "BUTTON OPACITY", () => Percent(s.touchOpacity), d => s.touchOpacity = Step(s.touchOpacity, d, 0.1f, 0.2f, 1f));

            UiFactory.Button(root, "BACK", centre, new Vector2(-200f, y - 40f), new Vector2(360f, 90f), Close);
            UiFactory.Button(root, "CREDITS", centre, new Vector2(200f, y - 40f), new Vector2(360f, 90f), () => CreditsView.Open(transform));
            _refresh?.Invoke();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        private Button Stepper(RectTransform root, ref float y, string label, Func<string> value, Action<int> change)
        {
            TextMeshProUGUI text = Row(root, y, label);
            var centre = new Vector2(0.5f, 0.5f);
            Button minus = UiFactory.Button(root, "-", centre, new Vector2(130f, y), new Vector2(90f, 80f), () => Changed(() => change(-1)));
            TextMeshProUGUI shown = UiFactory.Text(root, label + " Value", centre, new Vector2(290f, y), 40f, TextAlignmentOptions.Center, new Vector2(200f, 80f));
            UiFactory.Button(root, "+", centre, new Vector2(450f, y), new Vector2(90f, 80f), () => Changed(() => change(1)));
            _refresh += () => shown.text = value();
            y -= RowStep;
            return minus;
        }

        private void Toggle(RectTransform root, ref float y, string label, Func<string> value, Action toggle)
        {
            Row(root, y, label);
            Button button = UiFactory.Button(root, "Toggle", new Vector2(0.5f, 0.5f), new Vector2(290f, y), new Vector2(410f, 80f), () => Changed(toggle));
            TextMeshProUGUI shown = button.GetComponentInChildren<TextMeshProUGUI>();
            _refresh += () => shown.text = value();
            y -= RowStep;
        }

        private static TextMeshProUGUI Row(RectTransform root, float y, string label)
        {
            TextMeshProUGUI text = UiFactory.Text(root, label, new Vector2(0.5f, 0.5f), new Vector2(-330f, y), 40f, TextAlignmentOptions.Right, new Vector2(620f, 80f));
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

        private static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
