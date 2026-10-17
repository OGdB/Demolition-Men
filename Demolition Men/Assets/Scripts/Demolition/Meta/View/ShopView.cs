using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Demolition.Meta
{
    /// <summary>The skill shop (roadmap D6): banked cash buys the next level of an upgrade.
    /// One row per <see cref="UpgradeCatalog"/> entry. Built in code; Back (Esc / B) closes.</summary>
    public sealed class ShopView : MonoBehaviour
    {
        private static readonly Color Gold = new Color(1f, 0.85f, 0.3f);

        private TextMeshProUGUI _cash;
        private GameObject _previousSelection;
        private System.Action _unblock;
        private readonly List<Action> _refresh = new List<Action>();

        public static bool IsOpen { get; private set; }

        public static void Open(Transform parent)
        {
            if (IsOpen)
                return;
            var go = new GameObject("Shop");
            go.transform.SetParent(parent, false);
            go.AddComponent<ShopView>();
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
            if (back)
                Close();
        }

        private void Close()
        {
            GetComponentInParent<LevelSelectView>()?.OnCashChanged();
            Destroy(gameObject);
        }

        private void Build()
        {
            Canvas canvas = UiFactory.Canvas(transform, "ShopCanvas", 40);
            _unblock = UiFactory.Modal(canvas);
            var root = (RectTransform)canvas.transform;
            UiFactory.Backdrop(root, new Color(0.05f, 0.05f, 0.07f, 0.97f));
            var centre = new Vector2(0.5f, 0.5f);

            TextMeshProUGUI title = UiFactory.Text(root, "Title", centre, new Vector2(0f, 440f), 80f, TextAlignmentOptions.Center, new Vector2(1200f, 100f));
            title.text = "SKILL SHOP";
            title.color = Gold;
            _cash = UiFactory.Text(root, "Cash", centre, new Vector2(0f, 360f), 48f, TextAlignmentOptions.Center, new Vector2(1000f, 60f));
            _cash.color = Gold;

            Button first = null;
            float y = 240f;
            foreach (UpgradeDefinition upgrade in UpgradeCatalog.All)
            {
                Button buy = Row(root, upgrade, y);
                if (first == null)
                    first = buy;
                y -= 125f;
            }
            UiFactory.Button(root, "BACK", centre, new Vector2(0f, y - 30f), new Vector2(360f, 90f), Close);

            Refresh();
            if (EventSystem.current != null && first != null)
                EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        private Button Row(RectTransform root, UpgradeDefinition upgrade, float y)
        {
            var centre = new Vector2(0.5f, 0.5f);
            TextMeshProUGUI name = UiFactory.Text(root, upgrade.Name, centre, new Vector2(-480f, y + 18f), 44f, TextAlignmentOptions.Left, new Vector2(700f, 56f));
            name.text = upgrade.Name;
            TextMeshProUGUI description = UiFactory.Text(root, upgrade.Name + " Text", centre, new Vector2(-480f, y - 28f), 26f, TextAlignmentOptions.Left, new Vector2(700f, 36f));
            description.text = upgrade.Description;
            description.color = new Color(0.8f, 0.8f, 0.8f);
            TextMeshProUGUI level = UiFactory.Text(root, upgrade.Name + " Level", centre, new Vector2(120f, y), 34f, TextAlignmentOptions.Center, new Vector2(420f, 90f));

            Button buy = UiFactory.Button(root, "BUY", centre, new Vector2(560f, y), new Vector2(300f, 96f), () =>
            {
                if (UpgradeCatalog.TryBuy(Profile.Data, upgrade.Id))
                {
                    Profile.Save();
                    Refresh();
                }
            });

            _refresh.Add(() =>
            {
                int current = Profile.Data.UpgradeLevel(upgrade.Id);
                string pips = new string('#', current) + new string('-', upgrade.MaxLevel - current);
                string now = string.Format(upgrade.Format, upgrade.ValueAt(current));
                string next = current < upgrade.MaxLevel ? " > " + string.Format(upgrade.Format, upgrade.ValueAt(current + 1)) : string.Empty;
                level.text = $"[{pips}]\n<size=75%>{now}{next}</size>";
                int cost = upgrade.CostOfNext(current);
                buy.interactable = cost >= 0 && Profile.Data.cash >= cost;
                UiFactory.Label(buy, cost < 0 ? "MAX" : $"${cost:N0}", 40f);
            });
            return buy;
        }

        private void Refresh()
        {
            _cash.text = $"BANK ${Profile.Data.cash:N0}";
            foreach (Action refresh in _refresh)
                refresh();
        }
    }
}
