using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Demolition
{
    /// <summary>Small helpers for views that build their uGUI in code.</summary>
    public static class UiFactory
    {
        private static Material _outline;

        /// <summary>TMP's bundled outline preset: dark edge so text reads over the bright sky.</summary>
        public static Material Outline =>
            _outline != null ? _outline : _outline = Resources.Load<Material>("Fonts & Materials/LiberationSans SDF - Outline");

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
            scaler.matchWidthOrHeight = 0.5f;
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
            if (outlined && Outline != null)
            {
                text.fontSharedMaterial = Outline;
                text.fontStyle = FontStyles.Bold;
            }
            return text;
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
            // The button tints a white image, so the block colours below are the final colours.
            Image background = Image(parent, label, anchor, position, size, Color.white);
            background.raycastTarget = true;
            var button = background.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
            colors.highlightedColor = new Color(1f, 0.8f, 0.2f);
            colors.selectedColor = new Color(1f, 0.8f, 0.2f);
            colors.pressedColor = new Color(0.8f, 0.6f, 0.1f);
            button.colors = colors;
            button.onClick.AddListener(onClick);

            TextMeshProUGUI text = Text(background.transform, "Label", new Vector2(0.5f, 0.5f), Vector2.zero,
                size.y * 0.5f, TextAlignmentOptions.Center, size);
            text.text = label;
            return button;
        }
    }
}
