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
    /// CHAIN, BRACELETS, TATTOO; the shoulder buttons switch). The slot's items are a grid of tiles,
    /// each with Player 1 wearing it and its price or who wears it. The tile picked (tapped, or
    /// reached with a gamepad) shows large beside them, big enough to judge on a phone: both players
    /// wearing it with what else they wear, how it's had (cash, full job sheets, REMOVE ADS), BUY for
    /// one on sale, and per player a button that puts it on or takes it off. Submit on a tile goes
    /// to those buttons. Opens from the skill shop; Back (Esc / B) closes.</summary>
    public sealed class LooksView : MonoBehaviour
    {
        // A tile picked by what reaches it: the pointer alone doesn't, or going for BUY would pick the tiles it crosses.
        private sealed class TileFocus : MonoBehaviour, ISelectHandler, ISubmitHandler
        {
            public Action Pick, Submit;
            public void OnSelect(BaseEventData eventData) => Pick();
            public void OnSubmit(BaseEventData eventData) => Submit();
        }

        // shortcut: four rows of four fit beside the large view; past sixteen items in a slot this needs paging or a scroll list.
        private const int Columns = 4;
        private const float TileSize = 165f, TileStep = 173f, RowStep = 168f, TileThumb = 128f;
        private const float GridX = -737f, GridY = 240f, StageX = 355f, StageSize = 448f;
        private static readonly Color Stage = new Color(0.46f, 0.52f, 0.6f);
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
        private readonly Texture2D[] _large = new Texture2D[2];
        private readonly RawImage[] _stage = new RawImage[2];
        private readonly List<Button> _wears = new List<Button>();
        private LookInfo _picked;
        private RectTransform _pickedMark;
        private TextMeshProUGUI _name, _how;
        private Button _buy;

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
                _tabs.Add(UiFactory.Button(_root, SlotNames[i], centre, new Vector2((i - 2) * 310f, 362f), new Vector2(290f, 70f), () => Show(slot, false)));
            }
            _players = NetRole.IsOnline ? 1 : Mathf.Max(1, Session.PlayerCount);
            UiFactory.Button(_root, "BACK", centre, new Vector2(560f, -400f), new Vector2(300f, 80f), () => Destroy(gameObject));
            _cash = UiFactory.Text(_root, "Cash", centre, new Vector2(-400f, -400f), 40f, TextAlignmentOptions.Center, new Vector2(600f, 60f), outlined: true);
            _cash.color = UiFactory.Gold;
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
            _picked = null;
            _grid = UiFactory.Rect(_root, "Grid", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1700f, 990f));
            _refresh.Clear();
            for (int i = 0; i < _tabs.Count; i++)
                UiFactory.Label(_tabs[i], i == (int)slot ? $"> {SlotNames[i]} <" : SlotNames[i], 30f);
            List<LookInfo> items = LookCatalog.InSlot(slot);
            if (items.Count == 0)
                return;
            _pickedMark = UiFactory.Image(_grid, "Picked", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TileSize + 14f, TileSize + 14f), UiFactory.Gold).rectTransform;
            _pickedMark.GetComponent<Image>().sprite = UiFactory.PlateSprite;
            _pickedMark.GetComponent<Image>().type = Image.Type.Sliced;
            LookInfo worn = LookCatalog.Worn(Profile.Data, 0, slot);
            LookInfo first = worn != null ? worn : items[0];
            BuildStage();
            for (int i = 0; i < items.Count; i++)
                Tile(items[i], new Vector2(GridX + i % Columns * TileStep, GridY - i / Columns * RowStep), items[i] == first);
            if (select && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_tabs[(int)slot].gameObject);
            Refresh();
        }

        private void Tile(LookInfo look, Vector2 at, bool picked)
        {
            var centre = new Vector2(0.5f, 0.5f);
            Button tile = UiFactory.Button(_grid, look.Id, centre, at, new Vector2(TileSize, TileSize), () => Pick(look, at));
            UiFactory.Label(tile, "");
            UiFactory.Image(tile.transform, "Stage", centre, new Vector2(0f, 14f), new Vector2(TileThumb, TileThumb), Stage);
            Texture2D thumb = Dressed(look, 0);
            if (thumb != null)
            {
                _portraits.Add(thumb);
                Portrait(tile.transform, "Thumb", new Vector2(0f, 14f), TileThumb).texture = thumb;
            }
            TextMeshProUGUI tag = UiFactory.Text(tile.transform, "Tag", centre, new Vector2(0f, -66f), 23f, TextAlignmentOptions.Center, new Vector2(TileSize - 8f, 30f), outlined: true);
            tag.fontSharedMaterial = UiFactory.ThinOutline;
            var focus = tile.gameObject.AddComponent<TileFocus>();
            focus.Pick = () => Pick(look, at);
            focus.Submit = SelectStageButton;
            _refresh.Add(() =>
            {
                string wearers = "";
                for (int p = 0; p < _players; p++)
                    if (LookCatalog.Worn(Profile.Data, p, look.Slot) == look)
                        wearers += $"P{p + 1} ";
                bool owned = LookCatalog.Owns(Profile.Data, look);
                tag.color = owned ? UiFactory.Gold : Color.white;
                tag.text = owned ? wearers.TrimEnd() : Short(look);
            });
            if (picked)
                Pick(look, at);
        }

        /// <summary>The large view: the picked item on both players, how it's had, and its buttons.</summary>
        private void BuildStage()
        {
            var centre = new Vector2(0.5f, 0.5f);
            _name = UiFactory.Text(_grid, "Name", centre, new Vector2(StageX, 292f), 44f, TextAlignmentOptions.Center, new Vector2(920f, 56f), outlined: true);
            _name.color = UiFactory.Gold;
            for (int p = 0; p < 2; p++)
            {
                var at = new Vector2(StageX + (p * 2 - 1) * (StageSize / 2f + 6f), 36f);
                UiFactory.Image(_grid, "Stage P" + (p + 1), centre, at, new Vector2(StageSize, StageSize), Stage);
                _stage[p] = Portrait(_grid, "Large P" + (p + 1), at, StageSize);
            }
            _how = UiFactory.Text(_grid, "How", centre, new Vector2(StageX, -212f), 28f, TextAlignmentOptions.Center, new Vector2(920f, 36f));
            _how.color = new Color(0.8f, 0.8f, 0.8f);
            var button = new Vector2(StageX, -295f);
            _buy = UiFactory.Button(_grid, "BUY", centre, button, new Vector2(620f, 84f), () =>
            {
                if (LookCatalog.TryBuy(Profile.Data, _picked))
                {
                    Profile.Save();
                    Refresh();
                    SelectStageButton(); // BUY just went
                }
            });
            _wears.Clear();
            float width = _players > 1 ? StageSize - 30f : 620f; // room for the selected one to grow
            for (int p = 0; p < _players; p++)
            {
                int player = p;
                var at = _players > 1 ? new Vector2(StageX + (p * 2 - 1) * (StageSize / 2f + 6f), button.y) : button;
                Button wear = UiFactory.Button(_grid, "P" + (p + 1), centre, at, new Vector2(width, 84f), () => Toggle(player));
                _wears.Add(wear);
                _refresh.Add(() => UiFactory.Label(wear, LookCatalog.Worn(Profile.Data, player, _picked.Slot) == _picked ? $"TAKE OFF P{player + 1}" : $"PUT ON P{player + 1}", 34f));
            }
            _refresh.Add(() =>
            {
                bool owned = LookCatalog.Owns(Profile.Data, _picked);
                _buy.gameObject.SetActive(!owned && _picked.Source == LookSource.Shop);
                _buy.interactable = Profile.Data.cash >= _picked.Price;
                UiFactory.Label(_buy, $"BUY ${_picked.Price:N0}", 36f);
                foreach (Button wear in _wears)
                    wear.gameObject.SetActive(owned);
                _how.text = owned ? "YOURS"
                    : _picked.Source == LookSource.Shop ? "LOOKS ONLY: NOBODY HITS HARDER FOR A NICER CHAIN"
                    : _picked.Source == LookSource.JobSheets ? $"FULL JOB SHEETS {Mathf.Min(Profile.Data.FullSheets(), _picked.Sheets)}/{_picked.Sheets}"
                    : "COMES WITH REMOVE ADS";
            });
        }

        private void Pick(LookInfo look, Vector2 tile)
        {
            if (look == _picked)
                return;
            _picked = look;
            _pickedMark.anchoredPosition = tile;
            _name.text = look.Name;
            for (int p = 0; p < 2; p++)
            {
                Destroy(_large[p]);
                _large[p] = Dressed(look, p);
                _stage[p].texture = _large[p];
                _stage[p].enabled = _large[p] != null;
            }
            Refresh();
        }

        private void SelectStageButton()
        {
            if (EventSystem.current == null)
                return;
            if (_buy.gameObject.activeSelf && _buy.interactable)
                EventSystem.current.SetSelectedGameObject(_buy.gameObject);
            else if (_wears.Count > 0 && _wears[0].gameObject.activeSelf)
                EventSystem.current.SetSelectedGameObject(_wears[0].gameObject);
        }

        private static string Short(LookInfo look) =>
            look.Source == LookSource.Shop ? $"${look.Price:N0}"
            : look.Source == LookSource.JobSheets ? $"JOBS {Mathf.Min(Profile.Data.FullSheets(), look.Sheets)}/{look.Sheets}"
            : "NO ADS";

        private static RawImage Portrait(Transform parent, string name, Vector2 at, float size)
        {
            RectTransform rt = UiFactory.Rect(parent, name, new Vector2(0.5f, 0.5f), at, new Vector2(size, size));
            var image = rt.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Player <paramref name="player"/> standing in the item and what else they wear: their
        /// idle frame, dressed; the caller destroys it. Null without the prefab or a readable sheet.</summary>
        private static Texture2D Dressed(LookInfo look, int player)
        {
            GameObject[] prefabs = GameConfig.Instance != null ? GameConfig.Instance.playerPrefabs : null;
            SpriteRenderer source = prefabs != null && player < prefabs.Length && prefabs[player] != null
                ? prefabs[player].GetComponentInChildren<SpriteRenderer>(true) : null;
            if (source == null || source.sprite == null)
                return null;
            LookInfo[] items = LookCatalog.Wearing(Profile.Data, player);
            items[(int)look.Slot] = look;
            return LookSkin.Portrait(source.sprite, LookCatalog.Combine(items), player == 1);
        }

        private void DropPortraits()
        {
            foreach (Texture2D portrait in _portraits)
                Destroy(portrait);
            _portraits.Clear();
            for (int p = 0; p < _large.Length; p++)
            {
                Destroy(_large[p]);
                _large[p] = null;
            }
        }

        private void Toggle(int player)
        {
            if (LookCatalog.Worn(Profile.Data, player, _picked.Slot) == _picked)
                LookCatalog.TakeOff(Profile.Data, player, _picked.Slot);
            else if (!LookCatalog.Wear(Profile.Data, player, _picked))
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
