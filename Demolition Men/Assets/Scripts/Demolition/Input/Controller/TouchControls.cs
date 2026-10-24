using Demolition.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Demolition
{
    /// <summary>
    /// On-screen controls for phones and tablets, built in code: a floating stick on the
    /// left half, JUMP / HIT / TNT / SWAP under the right thumb, a pause button, and two
    /// fingers anywhere else pinch-zoom the camera. Shown
    /// only when <see cref="Platform.IsTouch"/>; hidden while paused or after the run.
    /// Drives <see cref="TouchInputSource.Shared"/>.
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        private const float StickRadius = 120f;
        private static readonly Vector2 PauseCorner = new Vector2(-16f, -16f);

        /// <summary>Button size multiplier and opacity, from the settings.</summary>
        public static float Scale = 1f;
        public static float Opacity = 0.55f;

        private GameObject _root;
        private RectTransform _scaled;
        private CanvasGroup _group;
        private RectTransform _pause;
        private RunHud _hud;

        private void Start()
        {
            _hud = GetComponent<RunHud>();
            Meta.Profile.SettingsChanged += OnSettingsChanged;
            if (Platform.IsTouch)
                Build();
        }

        private void OnDestroy()
        {
            Meta.Profile.SettingsChanged -= OnSettingsChanged;
            TouchInputSource.Shared.Release();
        }

        // TOUCH CONTROLS changed in the pause menu: show or remove them right away.
        private void OnSettingsChanged()
        {
            if (Platform.IsTouch && _root == null)
            {
                Build();
            }
            else if (!Platform.IsTouch && _root != null)
            {
                Destroy(_root);
                _root = null;
                TouchInputSource.Shared.Release();
            }
        }

        private void LateUpdate()
        {
            if (_root == null)
                return;
            RunController run = RunController.Current;
            bool show = !PauseController.MenuOpen && (run == null || run.State == null || !run.State.IsOver);
            if (_root.activeSelf != show)
                _root.SetActive(show);
            _group.alpha = Opacity;
            _scaled.localScale = Vector3.one * Scale;
            // In co-op P2's panel holds the top-right corner: pause sits under it.
            float below = _hud != null ? _hud.TopRightDepth / _pause.parent.lossyScale.y : 0f;
            _pause.anchoredPosition = PauseCorner + Vector2.down * below;
        }

        private void Build()
        {
            TouchInputSource source = TouchInputSource.Shared;
            // 1.25 under the canvas's Expand matching keeps the old size on 20:9 phones and 4:3 tablets.
            Canvas canvas = UiFactory.Canvas(transform, "TouchControls", 15, 1.25f);
            _root = canvas.gameObject;
            _group = _root.AddComponent<CanvasGroup>();
            RectTransform safe = UiFactory.SafeArea(canvas.transform);

            // Everywhere the controls aren't: made first, so they sit on top and keep their touches.
            Image pinch = UiFactory.Image(safe, "PinchZone", Vector2.zero, Vector2.zero, Vector2.zero, Color.clear);
            pinch.rectTransform.anchorMax = Vector2.one;
            pinch.raycastTarget = true;
            pinch.gameObject.AddComponent<TouchPinch>().Configure(source);

            // Stick zone: the lower-left of the screen.
            Image zone = UiFactory.Image(safe, "StickZone", Vector2.zero, Vector2.zero, Vector2.zero, Color.clear);
            zone.rectTransform.anchorMax = new Vector2(0.45f, 0.75f);
            zone.raycastTarget = true;
            Image stickBase = Disc(zone.transform, "Base", Vector2.zero, new Vector2(240f, 240f), new Color(1f, 1f, 1f, 0.25f));
            stickBase.rectTransform.anchoredPosition = new Vector2(230f, 210f);
            Image knob = Disc(stickBase.transform, "Knob", new Vector2(0.5f, 0.5f), new Vector2(110f, 110f), new Color(1f, 1f, 1f, 0.7f));
            knob.rectTransform.anchoredPosition = Vector2.zero;
            zone.gameObject.AddComponent<TouchStick>().Configure(source, stickBase.rectTransform, knob.rectTransform, StickRadius);

            // Buttons in a thumb arc around the bottom-right corner; scaled together.
            _scaled = UiFactory.Rect(safe, "Buttons", Vector2.right, Vector2.zero, new Vector2(700f, 600f));
            Button(_scaled, "JUMP", new Vector2(-170f, 170f), 220f, held => source.SetJump(held));
            Button(_scaled, "HIT", new Vector2(-420f, 130f), 200f, held => source.SetAttack(held));
            Button(_scaled, "TNT", new Vector2(-400f, 360f), 140f, held => { if (held) source.PressDynamite(); });
            Button(_scaled, "SWAP", new Vector2(-170f, 410f), 130f, held => { if (held) source.PressCycle(); });

            UnityEngine.UI.Button pause = UiFactory.Button(safe, "II", Vector2.one, PauseCorner, new Vector2(96f, 80f),
                () => FindAnyObjectByType<PauseController>()?.SetPaused(true));
            pause.gameObject.name = "Pause";
            _pause = (RectTransform)pause.transform;
        }

        private static void Button(RectTransform parent, string label, Vector2 position, float size, System.Action<bool> changed)
        {
            Image image = Disc(parent, label, Vector2.right, new Vector2(size, size), new Color(0.1f, 0.1f, 0.1f, 0.8f));
            image.rectTransform.anchoredPosition = position;
            TextMeshProUGUI text = UiFactory.Text(image.transform, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, size * 0.22f,
                TextAlignmentOptions.Center, new Vector2(size, size), outlined: true);
            text.text = label;
            image.gameObject.AddComponent<TouchButton>().Configure(changed);
        }

        private static Image Disc(Transform parent, string name, Vector2 anchor, Vector2 size, Color color)
        {
            Image image = UiFactory.Image(parent, name, anchor, Vector2.zero, size, color);
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.sprite = PrimitiveSprite.Disc();
            image.raycastTarget = true;
            return image;
        }
    }
}
