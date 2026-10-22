using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Demolition.Run
{
    /// <summary>
    /// The tutorial's hints as signs in the level (<see cref="TutorialTrail"/>): the current
    /// one floats over the front player and travels with them through its stretch, its tail
    /// pointing at them; a passed one stays standing on a striped post at the boundary where
    /// the next lesson starts. Drawn on the HUD canvas at world positions, so the text stays
    /// sharp and readable at any zoom. Button names follow the devices the players use.
    /// Made and ticked by <see cref="RunHud"/>; reads the run, changes nothing.
    /// </summary>
    public sealed class TutorialSigns
    {
        private sealed class Sign
        {
            public string Template;
            public string Text;
            public RectTransform Root;
            public CanvasGroup Group;
            public RectTransform Plate;
            public TextMeshProUGUI Label;
            public RectTransform Tail;
            public RectTransform Post;
            public float FootY = float.NaN; // world y the sign hovers over, eased
            public float Age;
        }

        private const float MaxTextWidth = 720f;
        private const float FontSize = 28f;
        private static readonly Vector2 Padding = new Vector2(28f, 16f);
        private const float Rim = 14f;          // the plate's rim at pixelsPerUnitMultiplier 2
        private const float StripeHeight = 12f;
        private const float TailHeight = 16f;   // the tail's tip hangs this far below the plate
        private const float PostWidth = 14f;
        private const float HeadHeight = 1.9f;  // world units from the feet to the top of the head
        private const float HealthBarClear = 44f; // canvas units on top of that: the health bar and a gap
        private const float TopReserve = 150f;  // the timer, meter and score panels
        private const float PassedAlpha = 0.8f;
        private const float MinFit = 0.6f;      // a sign shrinks this far to stay inside a narrow stretch (zoomed out)
        private const float PopSeconds = 0.3f;
        private const float FollowSpeed = 5f;
        private static readonly Color Face = new Color(0.168f, 0.185f, 0.218f, 0.97f); // the plate's face, tinted
        private static readonly string HeadingColor = "#" + ColorUtility.ToHtmlStringRGB(UiFactory.Gold);

        private readonly RectTransform _root;
        private readonly List<Sign> _signs = new List<Sign>();
        private readonly List<float> _groundY = new List<float>();
        private readonly TutorialTrail _trail;
        private readonly List<InputStyle> _styles = new List<InputStyle>();

        /// <summary>Make it before the rest of the HUD, so that draws over the signs.</summary>
        public TutorialSigns(RectTransform root, IReadOnlyList<TutorialPrompt> prompts)
        {
            _root = root;
            var ordered = new List<TutorialPrompt>(prompts);
            ordered.Sort((a, b) => a.Position.x.CompareTo(b.Position.x));
            var markers = new List<float>();
            foreach (TutorialPrompt prompt in ordered)
            {
                markers.Add(prompt.Position.x);
                _groundY.Add(prompt.Position.y);
                _signs.Add(Build(prompt.Message));
            }
            _trail = new TutorialTrail(markers);
        }

        private Sign Build(string template)
        {
            var sign = new Sign { Template = template ?? string.Empty };
            sign.Root = UiFactory.Rect(_root, "Sign", Vector2.zero, Vector2.zero, Vector2.zero);
            sign.Root.pivot = new Vector2(0.5f, 0f); // the tail's tip: it pops up from there
            sign.Group = sign.Root.gameObject.AddComponent<CanvasGroup>();
            sign.Group.blocksRaycasts = false;

            // A hazard-striped post down to the ground, once the sign stays behind.
            Image post = UiFactory.Image(sign.Root, "Post", new Vector2(0.5f, 0f), new Vector2(0f, TailHeight), new Vector2(PostWidth, 0f), UiFactory.Ink);
            post.rectTransform.pivot = new Vector2(0.5f, 1f);
            Image stripes = UiFactory.Image(post.transform, "Stripes", Vector2.zero, Vector2.zero, Vector2.zero, Color.white);
            stripes.rectTransform.anchorMax = Vector2.one;
            stripes.rectTransform.offsetMin = new Vector2(3f, 0f);
            stripes.rectTransform.offsetMax = new Vector2(-3f, 0f);
            stripes.sprite = UiFactory.HazardSprite;
            stripes.type = Image.Type.Tiled;
            stripes.pixelsPerUnitMultiplier = 2f;
            sign.Post = post.rectTransform;

            // A diamond half under the plate is the tail: outline, then the face.
            sign.Tail = UiFactory.Rect(sign.Root, "Tail", new Vector2(0.5f, 0f), new Vector2(0f, TailHeight), Vector2.zero);
            Diamond(sign.Tail, 30f, UiFactory.Ink);
            Diamond(sign.Tail, 22f, Face);

            Image plate = UiFactory.Image(sign.Root, "Plate", new Vector2(0.5f, 0f), new Vector2(0f, TailHeight), Vector2.zero, UiFactory.PanelColor);
            plate.sprite = UiFactory.PlateSprite;
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = 2f;
            sign.Plate = plate.rectTransform;
            Image strip = UiFactory.Stripe(plate.transform, "Stripe", StripeHeight);
            strip.pixelsPerUnitMultiplier = 2f;
            strip.rectTransform.offsetMin = new Vector2(Rim, -Rim - StripeHeight);
            strip.rectTransform.offsetMax = new Vector2(-Rim, -Rim);

            sign.Label = UiFactory.Text(plate.transform, "Text", new Vector2(0.5f, 0f), new Vector2(0f, Rim + Padding.y), FontSize,
                TextAlignmentOptions.Center, new Vector2(MaxTextWidth, FontSize), outlined: true);
            sign.Label.textWrappingMode = TextWrappingModes.Normal;
            sign.Root.gameObject.SetActive(false);
            return sign;
        }

        private static void Diamond(RectTransform parent, float size, Color color)
        {
            Image square = UiFactory.Image(parent, "Diamond", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), color);
            square.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        /// <param name="toCanvas">World point → anchored position on the HUD root.</param>
        public void Tick(RunController run, Func<Vector3, Vector2> toCanvas)
        {
            bool over = run.State.IsOver;
            bool anyone = Front(run, out Vector3 front);
            if (anyone && _trail.Reach(front.x) && !over)
            {
                _signs[_trail.Current].Age = 0f;
                UiAudio.Tick(0.8f);
            }
            UpdateStyles(run);
            float unit = (toCanvas(Vector3.right) - toCanvas(Vector3.zero)).x; // canvas units per world unit, at this zoom
            float topLimit = _root.rect.height - TopReserve;

            for (int i = 0; i < _signs.Count; i++)
            {
                Sign sign = _signs[i];
                bool shown = !over && _trail.IsShown(i) && unit > 0f;
                if (sign.Root.gameObject.activeSelf != shown)
                    sign.Root.gameObject.SetActive(shown);
                if (!shown)
                    continue;

                Layout(sign);
                bool current = i == _trail.Current;
                float width = sign.Plate.sizeDelta.x / unit; // world units, at this zoom
                float fit = Mathf.Clamp(_trail.Width(i) / width, MinFit, 1f);
                float x = _trail.SignX(i, front.x, width * fit * 0.5f);

                // It hovers over the ground of its stretch, or over the front player while they
                // are higher up; once passed it keeps the height it had.
                float footY = anyone ? Mathf.Max(_groundY[i], front.y) : _groundY[i];
                if (float.IsNaN(sign.FootY))
                    sign.FootY = footY;
                else if (current && anyone)
                    sign.FootY = Mathf.Lerp(sign.FootY, footY, Time.deltaTime * FollowSpeed);

                Vector2 at = toCanvas(new Vector3(x, sign.FootY + HeadHeight, 0f)) + new Vector2(0f, HealthBarClear);
                float height = (TailHeight + sign.Plate.sizeDelta.y) * fit;
                at.y = Mathf.Min(at.y, topLimit - height); // under the timer and the score, not off the top
                sign.Root.anchoredPosition = at;

                sign.Tail.gameObject.SetActive(current);
                sign.Post.gameObject.SetActive(!current);
                if (current)
                {
                    float halfPlate = sign.Plate.sizeDelta.x * 0.5f - 24f;
                    float toPlayer = anyone ? (toCanvas(front).x - at.x) / fit : 0f;
                    sign.Tail.anchoredPosition = new Vector2(Mathf.Clamp(toPlayer, -halfPlate, halfPlate), TailHeight);
                }
                else
                {
                    // At the boundary, under the plate; down to the ground of the next stretch.
                    float halfPlate = sign.Plate.sizeDelta.x * 0.5f - PostWidth;
                    int next = Mathf.Min(i + 1, _groundY.Count - 1);
                    Vector2 foot = toCanvas(new Vector3(_trail.End(i), _groundY[next], 0f));
                    sign.Post.anchoredPosition = new Vector2(Mathf.Clamp((foot.x - at.x) / fit, -halfPlate, halfPlate), TailHeight);
                    sign.Post.sizeDelta = new Vector2(PostWidth, Mathf.Max(0f, (at.y - foot.y) / fit + TailHeight));
                }

                sign.Age += Time.deltaTime;
                float t = Mathf.Clamp01(sign.Age / PopSeconds);
                sign.Root.localScale = Vector3.one * (fit * (current ? BackOut(t) : 1f));
                sign.Group.alpha = current ? Mathf.Clamp01(t * 3f) : PassedAlpha;
            }
        }

        // The furthest player still in play; false while nobody is.
        private static bool Front(RunController run, out Vector3 front)
        {
            front = Vector3.zero;
            bool any = false;
            for (int i = 0; i < run.Players.Count; i++)
            {
                if (run.State.Status(i) != PlayerStatus.Alive)
                    continue;
                Vector3 p = run.Players[i].Transform.position;
                if (!any || p.x > front.x)
                    front = p;
                any = true;
            }
            return any;
        }

        // The buttons on the signs follow what this device's players last used: one style
        // for a solo player, both a keyboard and a gamepad player's in co-op. A player who
        // hasn't touched their controls yet adds none, unless nobody has.
        private void UpdateStyles(RunController run)
        {
            _styles.Clear();
            PlayerInputReader first = null;
            for (int i = 0; i < run.Players.Count; i++)
            {
                if (run.Players[i].Health.IsProxy)
                    continue;
                PlayerInputReader input = run.Players[i].Input;
                first ??= input;
                if (input.Used)
                    _styles.Add(input.Style);
            }
            if (_styles.Count == 0 && first != null)
                _styles.Add(first.Style);
        }

        // Size the plate around the text; only when the text changes (another device).
        private void Layout(Sign sign)
        {
            string text = Compose(InputGlyphs.Format(sign.Template, _styles));
            if (text == sign.Text)
                return;
            sign.Text = text;
            sign.Label.text = text;
            Vector2 size = sign.Label.GetPreferredValues(text, MaxTextWidth, 0f);
            float width = Mathf.Min(MaxTextWidth, size.x);
            // Centred in the full width it was measured at, so it wraps the same way.
            sign.Label.rectTransform.sizeDelta = new Vector2(MaxTextWidth, size.y);
            sign.Plate.sizeDelta = new Vector2(width + 2f * (Rim + Padding.x), size.y + 2f * (Rim + Padding.y) + StripeHeight);
        }

        // The first line is the heading: bigger, in gold.
        private static string Compose(string message)
        {
            int newline = message.IndexOf('\n');
            if (newline < 0)
                return message;
            return $"<size=130%><color={HeadingColor}>{message.Substring(0, newline)}</color></size>{message.Substring(newline)}";
        }

        // 0 → 1 with a small overshoot.
        private static float BackOut(float t)
        {
            float u = t - 1f;
            return 1f + 2.7f * u * u * u + 1.7f * u * u;
        }
    }
}
