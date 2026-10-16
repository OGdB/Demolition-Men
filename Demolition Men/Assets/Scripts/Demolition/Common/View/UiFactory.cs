using TMPro;
using UnityEngine;
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

        public static Canvas Canvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
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
