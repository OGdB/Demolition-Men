using System;
using System.Collections.Generic;
using Demolition.Run;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Demolition.Meta
{
    /// <summary>The crew's looks (<see cref="LookCatalog"/>): each with how it's had (cash, full
    /// job sheets, REMOVE ADS), BUY for one on sale, and per player WEAR once owned; CREW puts the
    /// crew's own look back on. Opens from the skill shop; Back (Esc / B) closes.</summary>
    public sealed class LooksView : MonoBehaviour
    {
        private const float RowHeight = 90f;

        private GameObject _previousSelection;
        private Action _unblock;
        private Action _closed;
        private TextMeshProUGUI _cash;
        private readonly List<Action> _refresh = new List<Action>();

        public static bool IsOpen { get; private set; }

        /// <param name="closed">Called after it closes (the shop shows the new balance).</param>
        public static void Open(Transform parent, Action closed = null)
        {
            if (IsOpen)
                return;
            var go = new GameObject("Looks");
            go.transform.SetParent(parent, false);
            go.AddComponent<LooksView>()._closed = closed;
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
            _closed?.Invoke();
            if (_previousSelection != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_previousSelection);
        }

        private void Update()
        {
            bool back = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            foreach (Gamepad pad in Gamepad.all)
                back |= pad.buttonEast.wasPressedThisFrame;
            if (back)
                Destroy(gameObject);
        }

        private void Build()
        {
            Canvas canvas = UiFactory.Canvas(transform, "LooksCanvas", 45);
            _unblock = UiFactory.Modal(canvas);
            var root = (RectTransform)canvas.transform;
            UiFactory.Backdrop(root, new Color(0f, 0f, 0f, 0.75f));
            var centre = new Vector2(0.5f, 0.5f);
            UiFactory.TitledPanel(root, new Vector2(0f, 10f), new Vector2(1700f, 990f), "CREW LOOKS", 80f);
            TextMeshProUGUI hint = UiFactory.Text(root, "Hint", centre, new Vector2(0f, 380f), 28f, TextAlignmentOptions.Center, new Vector2(1500f, 40f));
            hint.text = LookCatalog.All.Count > 0 ? "Looks only: nobody hits harder for a nicer vest." : "NEW LOOKS ARE ON THEIR WAY";
            hint.color = new Color(0.8f, 0.8f, 0.8f);

            int players = NetRole.IsOnline ? 1 : Mathf.Max(1, Session.PlayerCount);
            Button first = null;
            float y = 300f;
            foreach (LookInfo look in LookCatalog.All)
            {
                Button button = Row(root, look, y, players);
                if (first == null && button != null)
                    first = button;
                y -= RowHeight;
            }
            for (int p = 0; p < players; p++)
            {
                int player = p;
                Button crew = UiFactory.Button(root, $"P{p + 1}: CREW", centre, new Vector2(-560f + p * 280f, -400f), new Vector2(260f, 80f), () => Wear(player, null));
                if (first == null)
                    first = crew;
            }
            UiFactory.Button(root, "BACK", centre, new Vector2(560f, -400f), new Vector2(300f, 80f), () => Destroy(gameObject));
            _cash = UiFactory.Text(root, "Cash", centre, new Vector2(150f, -400f), 40f, TextAlignmentOptions.Center, new Vector2(500f, 60f), outlined: true);
            _cash.color = UiFactory.Gold;

            Refresh();
            if (EventSystem.current != null && first != null)
                EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        private Button Row(RectTransform root, LookInfo look, float y, int players)
        {
            var centre = new Vector2(0.5f, 0.5f);
            TextMeshProUGUI name = UiFactory.Text(root, look.Id, centre, new Vector2(-300f, y + 16f), 34f, TextAlignmentOptions.Left, new Vector2(1000f, 44f));
            name.text = look.Name;
            TextMeshProUGUI how = UiFactory.Text(root, look.Id + " How", centre, new Vector2(-300f, y - 20f), 23f, TextAlignmentOptions.Left, new Vector2(1000f, 32f));
            how.color = new Color(0.8f, 0.8f, 0.8f);
            how.text = look.Source == LookSource.Shop ? $"${look.Price:N0}"
                : look.Source == LookSource.JobSheets ? $"FINISH EVERY JOB ON {look.Sheets} JOB SHEETS"
                : "COMES WITH REMOVE ADS";

            Button buy = UiFactory.Button(root, "BUY", centre, new Vector2(470f + (players - 1) * 95f, y), new Vector2(170f + (players - 1) * 190f, 68f), () =>
            {
                if (LookCatalog.TryBuy(Profile.Data, look))
                {
                    Profile.Save();
                    Refresh();
                }
            });
            var wears = new List<Button>();
            for (int p = 0; p < players; p++)
            {
                int player = p;
                Button wear = UiFactory.Button(root, "P" + (p + 1), centre, new Vector2(470f + p * 190f, y), new Vector2(170f, 68f), () => Wear(player, look));
                wears.Add(wear);
                _refresh.Add(() => UiFactory.Label(wear, LookCatalog.Worn(Profile.Data, player) == look ? $"> P{player + 1} <" : $"P{player + 1}", 34f));
            }
            _refresh.Add(() =>
            {
                bool owned = LookCatalog.Owns(Profile.Data, look);
                buy.gameObject.SetActive(!owned && look.Source == LookSource.Shop);
                buy.interactable = Profile.Data.cash >= look.Price;
                UiFactory.Label(buy, $"${look.Price:N0}", 30f);
                foreach (Button wear in wears)
                    wear.gameObject.SetActive(owned);
            });
            bool owns = LookCatalog.Owns(Profile.Data, look);
            return owns ? wears[0] : look.Source == LookSource.Shop ? buy : null; // null: nothing to press yet
        }

        private void Wear(int player, LookInfo look)
        {
            if (LookCatalog.Wear(Profile.Data, player, look))
            {
                Profile.Save();
                Refresh();
            }
        }

        private void Refresh()
        {
            _cash.text = $"BANK ${Profile.Data.cash:N0}";
            foreach (Action refresh in _refresh)
                refresh();
        }
    }
}
