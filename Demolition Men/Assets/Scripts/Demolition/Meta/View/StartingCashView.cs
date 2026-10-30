using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Demolition.Meta
{
    /// <summary>The crew's starting cash (<see cref="Economy.StartingCash"/>), once, over the results of
    /// the Tutorial's first finish: the amount counts up, and it says what the shop sells and where
    /// it is. OPEN SHOP or LATER (Esc / B). Built in code.</summary>
    public sealed class StartingCashView : MonoBehaviour
    {
        /// <summary>A press mashed at the results must not land on a button before this is read.</summary>
        private const float WakeDelay = 0.8f;

        private int _amount;
        private Action _openShop, _later, _after;
        private Action _unblock;
        private CanvasGroup _group;
        private TextMeshProUGUI _shown;
        private bool _awake;
        private Button _first;

        public static void Open(Transform parent, int amount, Action openShop, Action later)
        {
            var go = new GameObject("StartingCash");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<StartingCashView>();
            view._amount = amount;
            view._openShop = openShop;
            view._later = later;
        }

        private void Start()
        {
            Build();
            StartCoroutine(Reveal());
        }

        private void OnDestroy()
        {
            _unblock?.Invoke();
            _after?.Invoke(); // after the undo, so a shop opened here blocks what's underneath itself
        }

        private void Update()
        {
            if (!_awake)
                return;
            bool back = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            foreach (Gamepad pad in Gamepad.all)
                back |= pad.buttonEast.wasPressedThisFrame;
            if (back)
                Close(_later);
        }

        private void Close(Action after)
        {
            _after = after;
            Destroy(gameObject);
        }

        private void Build()
        {
            Canvas canvas = UiFactory.Canvas(transform, "StartingCashCanvas", 30);
            _unblock = UiFactory.Modal(canvas);
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;
            var root = (RectTransform)canvas.transform;
            UiFactory.Backdrop(root, new Color(0f, 0f, 0f, 0.75f));
            var centre = new Vector2(0.5f, 0.5f);
            UiFactory.TitledPanel(root, new Vector2(0f, 10f), new Vector2(1400f, 880f), "STARTING CAPITAL", 80f);

            _shown = UiFactory.Text(root, "Amount", centre, new Vector2(0f, 285f), 100f, TextAlignmentOptions.Center, new Vector2(800f, 120f), outlined: true);
            _shown.color = UiFactory.Gold;
            TextMeshProUGUI intro = UiFactory.Text(root, "Intro", centre, new Vector2(0f, 195f), 38f, TextAlignmentOptions.Center, new Vector2(1280f, 50f));
            intro.text = "You're hired! Spend it in the SHOP, your pick:";

            Line(root, "UPGRADES", "Hit harder, last longer, run faster", 100f);
            Line(root, "ACTIONS", "New special moves for the crew", 15f);
            Line(root, "LOOKS", "Outfits and signs, just for show", -70f);

            TextMeshProUGUI where = UiFactory.Text(root, "Where", centre, new Vector2(0f, -185f), 34f, TextAlignmentOptions.Center, new Vector2(1280f, 100f));
            where.text = "Every job pays cash for what you wreck.\nThe SHOP is after every job and in the level select.";
            where.color = new Color(0.8f, 0.8f, 0.8f);

            var size = new Vector2(440f, 100f);
            _first = UiFactory.Button(root, "OPEN SHOP", centre, new Vector2(-240f, -335f), size, () => Close(_openShop));
            UiFactory.Button(root, "LATER", centre, new Vector2(240f, -335f), size, () => Close(_later));
        }

        private static void Line(RectTransform root, string name, string what, float y)
        {
            var centre = new Vector2(0.5f, 0.5f);
            TextMeshProUGUI heading = UiFactory.Text(root, name, centre, new Vector2(-400f, y), 44f, TextAlignmentOptions.Left, new Vector2(380f, 60f), outlined: true);
            heading.text = name;
            heading.color = UiFactory.Gold;
            TextMeshProUGUI text = UiFactory.Text(root, name + " Text", centre, new Vector2(190f, y), 36f, TextAlignmentOptions.Left, new Vector2(800f, 60f));
            text.text = what;
        }

        private IEnumerator Reveal()
        {
            float start = Time.unscaledTime, lastCoin = 0f;
            UiAudio.Slam();
            yield return UiFactory.Tween(0.7f, t =>
            {
                _shown.text = $"+${Mathf.RoundToInt(_amount * UiFactory.EaseOut(t)):N0}";
                if (t - lastCoin > 0.15f)
                {
                    lastCoin = t;
                    UiAudio.Coin();
                }
            }, () => false);
            while (Time.unscaledTime < start + WakeDelay)
                yield return null;
            _awake = true;
            _group.interactable = true;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_first.gameObject);
        }
    }
}
