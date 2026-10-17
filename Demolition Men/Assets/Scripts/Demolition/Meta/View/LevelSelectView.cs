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
    /// <summary>
    /// The job board between the menu and a run: one column per theme with its levels
    /// (best grade and score, locked until the previous level is finished), plus the
    /// training yard, the daily challenge, the skill shop and the settings. Built in code.
    /// </summary>
    public sealed class LevelSelectView : MonoBehaviour
    {
        private static readonly Color Gold = new Color(1f, 0.85f, 0.3f);

        private Action _back;
        private TextMeshProUGUI _cash;
        private readonly List<Action> _refresh = new List<Action>();
        private Button _first;

        public static bool IsOpen { get; private set; }

        public static void Open(Action back)
        {
            if (IsOpen)
                return;
            UiFactory.EnsureEventSystem();
            var go = new GameObject("LevelSelect");
            go.AddComponent<LevelSelectView>()._back = back;
        }

        private void Start()
        {
            IsOpen = true;
            Build();
            Profile.SettingsChanged += Refresh;
        }

        private void OnDestroy()
        {
            IsOpen = false;
            Profile.SettingsChanged -= Refresh;
        }

        private void Update()
        {
            if (SettingsView.IsOpen || ShopView.IsOpen)
                return;
            bool back = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            foreach (Gamepad pad in Gamepad.all)
                back |= pad.buttonEast.wasPressedThisFrame;
            if (back)
                Close();
        }

        private void Close()
        {
            LevelSelection.ReturnToLevelSelect = false;
            Destroy(gameObject);
            _back?.Invoke();
        }

        private void Build()
        {
            GameConfig config = GameConfig.Instance;
            Canvas canvas = UiFactory.Canvas(transform, "LevelSelectCanvas", 30);
            var root = (RectTransform)canvas.transform;
            UiFactory.Backdrop(root, new Color(0.06f, 0.06f, 0.08f, 0.96f));
            RectTransform safe = UiFactory.SafeArea(root);
            var centre = new Vector2(0.5f, 0.5f);

            TextMeshProUGUI title = UiFactory.Text(safe, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -30f), 80f, TextAlignmentOptions.Center, new Vector2(1200f, 100f));
            title.text = "PICK A JOB";
            title.color = Gold;
            _cash = UiFactory.Text(safe, "Cash", Vector2.one, new Vector2(-40f, -40f), 44f, TextAlignmentOptions.Right, new Vector2(600f, 60f));
            _cash.color = Gold;

            IReadOnlyList<CampaignLevel> campaign = config != null ? config.Campaign : new List<CampaignLevel>();
            var themes = new List<LevelTheme>();
            foreach (CampaignLevel level in campaign)
                if (!themes.Contains(level.Theme))
                    themes.Add(level.Theme);

            float columnWidth = 540f;
            float x0 = -(themes.Count - 1) * columnWidth * 0.5f;
            for (int t = 0; t < themes.Count; t++)
            {
                float x = x0 + t * columnWidth;
                TextMeshProUGUI name = UiFactory.Text(root, themes[t].displayName, centre, new Vector2(x, 300f), 48f, TextAlignmentOptions.Center, new Vector2(500f, 60f));
                name.text = themes[t].displayName;
                float y = 200f;
                foreach (CampaignLevel level in campaign)
                {
                    if (level.Theme != themes[t])
                        continue;
                    LevelButton(root, level, new Vector2(x, y));
                    y -= 122f;
                }
            }

            var size = new Vector2(320f, 100f);
            float row = -380f;
            Button training = UiFactory.Button(root, "TRAINING", centre, new Vector2(-700f, row), size,
                () =>
                {
                    LevelSelection.Clear();
                    RunController.LoadScene(config.tutorialScene);
                });
            Button daily = UiFactory.Button(root, "DAILY", centre, new Vector2(-350f, row), size, () => LevelSelection.PlayDaily(DateTime.Today));
            _refresh.Add(() => UiFactory.Label(daily, $"DAILY\n<size=60%>{Best(Daily.Id(DateTime.Today))}</size>", 36f));
            UiFactory.Button(root, "SHOP", centre, new Vector2(0f, row), size, () => ShopView.Open(transform));
            UiFactory.Button(root, "SETTINGS", centre, new Vector2(350f, row), size, () => SettingsView.Open(transform));
            UiFactory.Button(root, "BACK", centre, new Vector2(700f, row), size, Close);

            Refresh();
            if (_first == null)
                _first = training;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_first.gameObject);
        }

        private void LevelButton(RectTransform root, CampaignLevel level, Vector2 position)
        {
            Button button = UiFactory.Button(root, level.Spec.name, new Vector2(0.5f, 0.5f), position, new Vector2(500f, 110f),
                () => LevelSelection.PlayCampaign(level.Index));
            _refresh.Add(() =>
            {
                SaveData save = Profile.Data;
                bool unlocked = level.Index == 0 || save.IsFinished(GameConfig.Instance.Campaign[level.Index - 1].Spec.id);
                button.interactable = unlocked;
                LevelRecord record = save.Level(level.Spec.id);
                string grade = record != null && record.bestGrade >= 0 ? ((Grade)record.bestGrade).ToString() : "-";
                string line = !unlocked ? "LOCKED"
                    : record != null && record.finished ? $"GRADE {grade}   BEST {record.bestScore:N0}"
                    : "NEW";
                UiFactory.Label(button, $"{level.Index + 1}. {level.Spec.name}\n<size=60%>{line}</size>", 38f);
                if (unlocked && (record == null || !record.finished) && _first == null)
                    _first = button; // the next job to do
            });
        }

        private static string Best(string id)
        {
            LevelRecord record = Profile.Data.Level(id);
            return record != null && record.bestScore > 0 ? $"BEST {record.bestScore:N0}" : "NEW SEED TODAY";
        }

        private void Refresh()
        {
            _cash.text = $"BANK ${Profile.Data.cash:N0}";
            foreach (Action refresh in _refresh)
                refresh();
        }

        /// <summary>The shop spent cash: show the new balance.</summary>
        public void OnCashChanged() => Refresh();
    }
}
