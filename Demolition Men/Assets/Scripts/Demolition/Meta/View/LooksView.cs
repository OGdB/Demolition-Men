using System;
using System.Collections.Generic;
using Demolition.Levels;
using Demolition.Run;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Demolition.Meta
{
    /// <summary>The crew's look items (<see cref="LookCatalog"/>), a tab per slot (OUTFIT, SIGN,
    /// CHAIN, BRACELETS, TATTOO; the shoulder buttons switch), the slot's items in two columns:
    /// each with both players wearing it with what else they wear, how it's had (cash, full job
    /// sheets, REMOVE ADS), BUY for one on sale, and per player a button that puts it on or, worn,
    /// takes it off. Opens from the skill shop; Back (Esc / B) closes.</summary>
    public sealed class LooksView : MonoBehaviour
    {
        // shortcut: two columns of eight fit the panel; past sixteen items in a slot this needs paging or a scroll list.
        private const int RowsPerColumn = 8;
        private const float RowHeight = 82f, ColumnX = 420f;
        private static readonly string[] SlotNames = { "OUTFIT", "SIGN", "CHAIN", "BRACELETS", "TATTOO" };

        private GameObject _previousSelection;
        private Action _unblock;
        private Action _closed;
        private TextMeshProUGUI _cash;
        private RectTransform _root, _grid;
        private readonly List<Button> _tabs = new List<Button>();
        private LookSlot _slot;
        private int _players;
        private readonly List<Action> _refresh = new List<Action>();
        private readonly List<Texture2D> _portraits = new List<Texture2D>();

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
            DropPortraits();
            _unblock?.Invoke();
            _closed?.Invoke();
            if (_previousSelection != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_previousSelection);
        }

        private void Update()
        {
            bool back = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            int step = 0;
            foreach (Gamepad pad in Gamepad.all)
            {
                back |= pad.buttonEast.wasPressedThisFrame;
                step += (pad.rightShoulder.wasPressedThisFrame ? 1 : 0) - (pad.leftShoulder.wasPressedThisFrame ? 1 : 0);
            }
            if (back)
                Destroy(gameObject);
            else if (step != 0)
                Show((LookSlot)(((int)_slot + step % LookCatalog.SlotCount + LookCatalog.SlotCount) % LookCatalog.SlotCount), true);
        }

        private void Build()
        {
            Canvas canvas = UiFactory.Canvas(transform, "LooksCanvas", 45);
            _unblock = UiFactory.Modal(canvas);
            _root = (RectTransform)canvas.transform;
            UiFactory.Backdrop(_root, new Color(0f, 0f, 0f, 0.75f));
            var centre = new Vector2(0.5f, 0.5f);
            UiFactory.TitledPanel(_root, new Vector2(0f, 10f), new Vector2(1700f, 990f), "CREW LOOKS", 80f);
            for (int i = 0; i < LookCatalog.SlotCount; i++)
            {
                var slot = (LookSlot)i;
                _tabs.Add(UiFactory.Button(_root, SlotNames[i], centre, new Vector2((i - 2) * 310f, 368f), new Vector2(290f, 58f), () => Show(slot, false)));
            }
            _players = NetRole.IsOnline ? 1 : Mathf.Max(1, Session.PlayerCount);
            UiFactory.Button(_root, "BACK", centre, new Vector2(560f, -400f), new Vector2(300f, 80f), () => Destroy(gameObject));
            _cash = UiFactory.Text(_root, "Cash", centre, new Vector2(-400f, -400f), 40f, TextAlignmentOptions.Center, new Vector2(600f, 60f), outlined: true);
            _cash.color = UiFactory.Gold;
            TextMeshProUGUI hint = UiFactory.Text(_root, "Hint", centre, new Vector2(150f, -400f), 24f, TextAlignmentOptions.Center, new Vector2(500f, 60f));
            hint.text = "LOOKS ONLY: NOBODY HITS HARDER\nFOR A NICER CHAIN";
            hint.color = new Color(0.8f, 0.8f, 0.8f);
            Show(LookSlot.Outfit, false);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_tabs[0].gameObject);
        }

        /// <summary>One slot's items; <paramref name="select"/> puts the selection on its tab.</summary>
        private void Show(LookSlot slot, bool select)
        {
            _slot = slot;
            if (_grid != null)
                Destroy(_grid.gameObject);
            DropPortraits();
            _grid = UiFactory.Rect(_root, "Grid", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1700f, 990f));
            _refresh.Clear();
            List<LookInfo> items = LookCatalog.InSlot(slot);
            for (int i = 0; i < items.Count; i++)
                Row(items[i], new Vector2((i / RowsPerColumn == 0 ? -1f : 1f) * ColumnX, 284f - i % RowsPerColumn * RowHeight));
            for (int i = 0; i < _tabs.Count; i++)
                UiFactory.Label(_tabs[i], i == (int)slot ? $"> {SlotNames[i]} <" : SlotNames[i], 30f);
            if (select && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_tabs[(int)slot].gameObject);
            Refresh();
        }

        private void Row(LookInfo look, Vector2 at)
        {
            var centre = new Vector2(0.5f, 0.5f);
            for (int p = 0; p < 2; p++)
                Preview(look, p, at + new Vector2(-360f + p * 74f, 0f));
            TextMeshProUGUI name = UiFactory.Text(_grid, look.Id, centre, at + new Vector2(-52f, 16f), 30f, TextAlignmentOptions.Left, new Vector2(345f, 40f));
            name.text = look.Name;
            TextMeshProUGUI how = UiFactory.Text(_grid, look.Id + " How", centre, at + new Vector2(-52f, -18f), 21f, TextAlignmentOptions.Left, new Vector2(345f, 28f));
            how.color = new Color(0.8f, 0.8f, 0.8f);
            how.text = look.Source == LookSource.Shop ? $"${look.Price:N0}"
                : look.Source == LookSource.JobSheets ? $"FULL JOB SHEETS {Mathf.Min(Profile.Data.FullSheets(), look.Sheets)}/{look.Sheets}"
                : "COMES WITH REMOVE ADS";

            Button buy = UiFactory.Button(_grid, "BUY", centre, at + new Vector2(265f, 0f), new Vector2(270f, 60f), () =>
            {
                if (LookCatalog.TryBuy(Profile.Data, look))
                {
                    Profile.Save();
                    Refresh();
                }
            });
            var wears = new List<Button>();
            float width = _players > 1 ? 130f : 270f;
            for (int p = 0; p < _players; p++)
            {
                int player = p;
                Button wear = UiFactory.Button(_grid, "P" + (p + 1), centre, at + new Vector2(130f + width / 2f + p * 140f, 0f), new Vector2(width, 60f), () => Toggle(player, look));
                wears.Add(wear);
                _refresh.Add(() => UiFactory.Label(wear, LookCatalog.Worn(Profile.Data, player, look.Slot) == look ? $"> P{player + 1} <" : $"P{player + 1}", 26f));
            }
            _refresh.Add(() =>
            {
                bool owned = LookCatalog.Owns(Profile.Data, look);
                buy.gameObject.SetActive(!owned && look.Source == LookSource.Shop);
                buy.interactable = Profile.Data.cash >= look.Price;
                UiFactory.Label(buy, $"BUY ${look.Price:N0}", 28f);
                foreach (Button wear in wears)
                    wear.gameObject.SetActive(owned);
            });
        }

        /// <summary>Player <paramref name="player"/> standing in the item and what else they wear: their idle frame, dressed.</summary>
        private void Preview(LookInfo look, int player, Vector2 at)
        {
            GameObject[] prefabs = GameConfig.Instance != null ? GameConfig.Instance.playerPrefabs : null;
            SpriteRenderer source = prefabs != null && player < prefabs.Length && prefabs[player] != null
                ? prefabs[player].GetComponentInChildren<SpriteRenderer>(true) : null;
            if (source == null || source.sprite == null)
                return;
            LookInfo[] items = LookCatalog.Wearing(Profile.Data, player);
            items[(int)look.Slot] = look;
            Texture2D portrait = LookSkin.Portrait(source.sprite, LookCatalog.Combine(items), player == 1);
            if (portrait == null)
                return;
            _portraits.Add(portrait);
            RectTransform rt = UiFactory.Rect(_grid, look.Id + " P" + (player + 1), new Vector2(0.5f, 0.5f), at, new Vector2(72f, 72f));
            var image = rt.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.texture = portrait;
        }

        private void DropPortraits()
        {
            foreach (Texture2D portrait in _portraits)
                Destroy(portrait);
            _portraits.Clear();
        }

        private void Toggle(int player, LookInfo look)
        {
            if (LookCatalog.Worn(Profile.Data, player, look.Slot) == look)
                LookCatalog.TakeOff(Profile.Data, player, look.Slot);
            else if (!LookCatalog.Wear(Profile.Data, player, look))
                return;
            Profile.Save();
            Refresh();
        }

        private void Refresh()
        {
            _cash.text = $"BANK ${Profile.Data.cash:N0}";
            foreach (Action refresh in _refresh)
                refresh();
        }
    }
}
