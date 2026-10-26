using System;
using System.Collections.Generic;
using Demolition.Meta;
using Demolition.Run;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Demolition.Actions
{
    /// <summary>The loadout between levels: every action with what it does, and per player the
    /// one they carry into the next level. Opens from the skill shop; Back (Esc / B) closes.
    /// Player 2's column shows in local co-op; online each device picks for its own player.</summary>
    public sealed class ActionsView : MonoBehaviour
    {
        private const float RowHeight = 80f;

        private GameObject _previousSelection;
        private System.Action _unblock;
        private readonly List<Action> _refresh = new List<Action>();

        public static bool IsOpen { get; private set; }

        public static void Open(Transform parent)
        {
            if (IsOpen)
                return;
            var go = new GameObject("Actions");
            go.transform.SetParent(parent, false);
            go.AddComponent<ActionsView>();
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
                Destroy(gameObject);
        }

        private void Build()
        {
            Canvas canvas = UiFactory.Canvas(transform, "ActionsCanvas", 45);
            _unblock = UiFactory.Modal(canvas);
            var root = (RectTransform)canvas.transform;
            UiFactory.Backdrop(root, new Color(0f, 0f, 0f, 0.75f));
            var centre = new Vector2(0.5f, 0.5f);
            UiFactory.TitledPanel(root, new Vector2(0f, 10f), new Vector2(1700f, 990f), "CREW ACTIONS", 80f);
            TextMeshProUGUI hint = UiFactory.Text(root, "Hint", centre, new Vector2(0f, 380f), 28f, TextAlignmentOptions.Center, new Vector2(1500f, 40f));
            hint.text = "Smash things to fill the meter, then let it rip. Each player carries one into a level.";
            hint.color = new Color(0.8f, 0.8f, 0.8f);

            int players = NetRole.IsOnline ? 1 : Mathf.Max(1, Session.PlayerCount);
            Button first = null;
            float y = 315f;
            foreach (ActionInfo info in ActionCatalog.All)
            {
                Button pick = Row(root, info, y, players);
                if (first == null)
                    first = pick;
                y -= RowHeight;
            }
            UiFactory.Button(root, "BACK", centre, new Vector2(0f, y - 25f), new Vector2(360f, 80f), () => Destroy(gameObject));

            Refresh();
            if (EventSystem.current != null && first != null)
                EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        private Button Row(RectTransform root, ActionInfo info, float y, int players)
        {
            var centre = new Vector2(0.5f, 0.5f);
            TextMeshProUGUI name = UiFactory.Text(root, info.Id, centre, new Vector2(-300f, y + 16f), 34f, TextAlignmentOptions.Left, new Vector2(1000f, 44f));
            name.text = info.Name;
            TextMeshProUGUI description = UiFactory.Text(root, info.Id + " Text", centre, new Vector2(-300f, y - 20f), 23f, TextAlignmentOptions.Left, new Vector2(1000f, 32f));
            description.text = info.Description;
            description.color = new Color(0.8f, 0.8f, 0.8f);

            Button first = null;
            for (int p = 0; p < players; p++)
            {
                int player = p;
                Button pick = UiFactory.Button(root, "P" + (p + 1), centre, new Vector2(470f + p * 190f, y), new Vector2(170f, 68f), () =>
                {
                    ActionCatalog.Equip(Profile.Data, player, info.Kind);
                    Profile.Save();
                    Refresh();
                });
                if (first == null)
                    first = pick;
                _refresh.Add(() =>
                {
                    bool carried = ActionCatalog.Equipped(Profile.Data, player) == info.Kind;
                    UiFactory.Label(pick, carried ? $"> P{player + 1} <" : $"P{player + 1}", 34f);
                });
            }
            return first;
        }

        private void Refresh()
        {
            foreach (Action refresh in _refresh)
                refresh();
        }
    }
}
