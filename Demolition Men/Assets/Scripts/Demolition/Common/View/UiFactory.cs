using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Demolition
{
    /// <summary>Small helpers for views that build their uGUI in code, and the game's UI
    /// look: the stencil display font, riveted steel plates for buttons and panels,
    /// hazard stripes, stars (sprites in <c>Resources/UI</c>, made by <c>tools/artpipe/ui.py</c>).</summary>
    public static class UiFactory
    {
        public static readonly Color Gold = new Color(1f, 0.85f, 0.3f);
        public static readonly Color Ink = new Color(0.06f, 0.06f, 0.09f);
        public static readonly Color Steel = new Color(0.36f, 0.4f, 0.45f);
        public static readonly Color PanelColor = new Color(0.2f, 0.22f, 0.26f, 0.97f);
        public static readonly Color StarLit = new Color(1f, 0.82f, 0.25f);
        public static readonly Color StarDark = new Color(0.22f, 0.23f, 0.27f, 0.9f);
        private static readonly Color Highlight = new Color(1f, 0.78f, 0.2f);

        private static TMP_FontAsset _font;
        private static bool _fontLoaded;
        private static Material _outline;
        private static Material _heading;
        private static Sprite _plate, _hazard, _star;

        /// <summary>Black Ops One (SIL OFL, <c>Resources/Fonts</c>) as a dynamic TMP font
        /// made at runtime; null keeps TMP's default font. Glyphs it lacks (arrows) come
        /// from TMP's default font as the fallback.</summary>
        public static TMP_FontAsset DisplayFont
        {
            get
            {
                if (!_fontLoaded)
                {
                    _fontLoaded = true;
                    var ttf = Resources.Load<UnityEngine.Font>("Fonts/BlackOpsOne");
                    if (ttf != null)
                        _font = TMP_FontAsset.CreateFontAsset(ttf);
                }
                return _font;
            }
        }

        /// <summary>Dark edge so text reads over the bright sky and on gold buttons.</summary>
        public static Material Outline => _outline != null ? _outline : _outline = Styled(false);

        /// <summary>Outline plus a drop shadow, for titles.</summary>
        public static Material Heading => _heading != null ? _heading : _heading = Styled(true);

        public static Sprite PlateSprite => _plate != null ? _plate : _plate = Resources.Load<Sprite>("UI/Plate");
        public static Sprite HazardSprite => _hazard != null ? _hazard : _hazard = Resources.Load<Sprite>("UI/Hazard");
        public static Sprite StarSprite => _star != null ? _star : _star = Resources.Load<Sprite>("UI/Star");

        // Only keyword sets the bundled TMP materials already use (outline; outline +
        // underlay), so the shader variants are in every build.
        private static Material Styled(bool shadow)
        {
            TMP_FontAsset font = DisplayFont;
            if (font == null)
                return Resources.Load<Material>(shadow ? "Fonts & Materials/LiberationSans SDF - Drop Shadow" : "Fonts & Materials/LiberationSans SDF - Outline");
            var material = new Material(font.material) { name = shadow ? "Display Heading" : "Display Outline" };
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, shadow ? 0.3f : 0.22f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, Ink);
            if (shadow)
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.6f));
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.7f);
                material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.1f);
            }
            return material;
        }

        /// <summary>UI needs an event system reading the Input System (mouse, touch, gamepad
        /// navigation); runtime-built scenes get one here.</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || Object.FindAnyObjectByType<EventSystem>() != null)
                return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        /// <param name="scale">Bigger than 1 makes everything on the canvas bigger (phones, TVs).</param>
        public static Canvas Canvas(Transform parent, string name, int sortingOrder, float scale = 1f)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f) / Mathf.Max(0.5f, scale);
            // The whole reference rect stays on screen at any aspect (20:9 phones, 4:3
            // tablets): centred layouts never clip, wider screens just get more room.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            return canvas;
        }

        /// <summary>Full-screen colour behind an overlay; it swallows clicks meant for what is underneath.</summary>
        public static Image Backdrop(Transform root, Color color)
        {
            Image image = Image(Stretch(root, "Backdrop"), "Fill", Vector2.zero, Vector2.zero, Vector2.zero, color);
            image.rectTransform.anchorMax = Vector2.one;
            image.raycastTarget = true;
            return image;
        }

        /// <summary>Make every other root canvas non-interactable while <paramref name="top"/>
        /// is open, so gamepad navigation can't wander onto buttons underneath. Returns the
        /// undo, to call when the overlay closes.</summary>
        public static System.Action Modal(Canvas top)
        {
            var groups = new System.Collections.Generic.List<(CanvasGroup group, bool was)>();
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas == top || !canvas.isRootCanvas || canvas.transform.IsChildOf(top.transform))
                    continue;
                CanvasGroup group = canvas.GetComponent<CanvasGroup>();
                if (group == null)
                    group = canvas.gameObject.AddComponent<CanvasGroup>();
                groups.Add((group, group.interactable));
                group.interactable = false;
            }
            return () =>
            {
                foreach ((CanvasGroup group, bool was) in groups)
                    if (group != null)
                        group.interactable = was;
            };
        }

        /// <summary>Change a button's label (and its size).</summary>
        public static TextMeshProUGUI Label(Button button, string text, float fontSize = 0f)
        {
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.text = text;
            if (fontSize > 0f)
                label.fontSize = fontSize;
            return label;
        }

        /// <summary>A rect anchored and pivoted at <paramref name="anchor"/>, offset by <paramref name="position"/>.</summary>
        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            RectTransform rt = Rect(parent, name, Vector2.zero, Vector2.zero, Vector2.zero);
            rt.anchorMax = Vector2.one;
            return rt;
        }

        /// <summary>A full-screen child that stays inside the safe area (notches, overscan):
        /// parent anything anchored to a screen edge here.</summary>
        public static RectTransform SafeArea(Transform canvas)
        {
            RectTransform rt = Stretch(canvas, "SafeArea");
            rt.pivot = new Vector2(0.5f, 0.5f); // local points are centre-relative, like the canvas's own
            rt.gameObject.AddComponent<SafeAreaFitter>();
            return rt;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, Vector2 anchor, Vector2 position,
            float fontSize, TextAlignmentOptions alignment, Vector2? size = null, bool outlined = false)
        {
            RectTransform rt = Rect(parent, name, anchor, position, size ?? new Vector2(600f, fontSize * 1.4f));
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (DisplayFont != null)
                text.font = DisplayFont;
            if (outlined && Outline != null)
            {
                text.fontSharedMaterial = Outline;
                if (DisplayFont == null)
                    text.fontStyle = FontStyles.Bold;
            }
            return text;
        }

        /// <summary>A title: display font, gold, outlined, with a drop shadow.</summary>
        public static TextMeshProUGUI Title(Transform parent, string text, Vector2 anchor, Vector2 position, float fontSize, float width = 1400f)
        {
            TextMeshProUGUI title = Text(parent, "Title", anchor, position, fontSize, TextAlignmentOptions.Center, new Vector2(width, fontSize * 1.3f));
            if (Heading != null)
                title.fontSharedMaterial = Heading;
            title.text = text;
            title.color = Gold;
            return title;
        }

        /// <summary>A riveted steel plate behind an overlay's content, with a hazard-striped
        /// strip along its top. Make it before the content so it draws underneath.</summary>
        public static Image Panel(Transform parent, Vector2 position, Vector2 size, bool stripe = true)
        {
            Image plate = Image(parent, "Panel", new Vector2(0.5f, 0.5f), position, size, PanelColor);
            plate.sprite = PlateSprite;
            plate.type = UnityEngine.UI.Image.Type.Sliced;
            if (stripe)
            {
                Image strip = Stripe(plate.transform, "Stripe", 32f);
                // Inside the plate's 28-unit rim (7 sprite pixels at 4 units each).
                strip.rectTransform.offsetMin = new Vector2(28f, -60f);
                strip.rectTransform.offsetMax = new Vector2(-28f, -28f);
            }
            return plate;
        }

        /// <summary>Yellow and black hazard stripes stretched across the top of <paramref name="parent"/>.</summary>
        public static Image Stripe(Transform parent, string name, float height)
        {
            Image strip = Image(parent, name, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, height), Color.white);
            strip.rectTransform.anchorMin = new Vector2(0f, 1f);
            strip.rectTransform.anchorMax = Vector2.one;
            strip.sprite = HazardSprite;
            strip.type = UnityEngine.UI.Image.Type.Tiled;
            return strip;
        }

        /// <summary>Runs <paramref name="apply"/> from 0 to 1 over unscaled time (menus
        /// animate while a run is paused); once <paramref name="skip"/> says so it jumps to 1.</summary>
        public static IEnumerator Tween(float seconds, System.Action<float> apply, System.Func<bool> skip)
        {
            for (float t = 0f; t < seconds && !skip(); t += Time.unscaledDeltaTime)
            {
                apply(t / seconds);
                yield return null;
            }
            apply(1f);
        }

        public static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        /// <summary>A confirm or back press, a click or a tap this frame: skips an intro or a
        /// reveal. Not any key, so attack keys mashed at the end of a run don't skip it.</summary>
        public static bool SkipPressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
                || kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                return true;
            foreach (Gamepad pad in Gamepad.all)
                if (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame)
                    return true;
            return false;
        }

        public static Image Star(Transform parent, string name, Vector2 anchor, Vector2 position, float size, bool lit)
        {
            Image star = Image(parent, name, anchor, position, new Vector2(size, size), lit ? StarLit : StarDark);
            star.sprite = StarSprite;
            star.preserveAspect = true;
            return star;
        }

        public static Image Image(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rt = Rect(parent, name, anchor, position, size);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Button Button(Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size,
            UnityEngine.Events.UnityAction onClick)
        {
            // The button tints a light steel plate: steel at rest, gold when selected or hovered.
            Image background = Image(parent, label, anchor, position, size, Color.white);
            background.sprite = PlateSprite;
            background.type = UnityEngine.UI.Image.Type.Sliced;
            background.raycastTarget = true;
            var button = background.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Steel;
            colors.highlightedColor = Highlight;
            colors.selectedColor = Highlight;
            colors.pressedColor = new Color(0.8f, 0.55f, 0.1f);
            colors.disabledColor = new Color(0.25f, 0.26f, 0.29f, 0.9f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            button.onClick.AddListener(onClick);

            TextMeshProUGUI text = Text(background.transform, "Label", new Vector2(0.5f, 0.5f), new Vector2(0f, 2f),
                Mathf.Min(size.y * 0.46f, 48f), TextAlignmentOptions.Center, size - new Vector2(24f, 0f), outlined: true);
            text.text = label;
            background.gameObject.AddComponent<UiButtonFeel>();
            return button;
        }
    }
}
