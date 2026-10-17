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
    /// (best stars and score, locked until the previous level is finished), plus the
    /// training yard, the daily challenge, the skill shop and the settings. Built in code.
    /// </summary>
    public sealed class LevelSelectView : MonoBehaviour
    {
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
            UiFactory.Stripe(root, "TopStripe", 32f);
            RectTransform bottom = UiFactory.Stripe(root, "BottomStripe", 32f).rectTransform;
            bottom.anchorMin = Vector2.zero;
            bottom.anchorMax = new Vector2(1f, 0f);
            bottom.pivot = new Vector2(0.5f, 0f);
            RectTransform safe = UiFactory.SafeArea(root);
            var centre = new Vector2(0.5f, 0.5f);

            UiFactory.Title(safe, "PICK A JOB", new Vector2(0.5f, 1f), new Vector2(0f, -44f), 80f, 1000f);
            _cash = UiFactory.Text(safe, "Cash", Vector2.one, new Vector2(-40f, -56f), 44f, TextAlignmentOptions.Right, new Vector2(600f, 60f), outlined: true);
            _cash.color = UiFactory.Gold;

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
                TextMeshProUGUI name = UiFactory.Text(root, themes[t].displayName, centre, new Vector2(x, 300f), 48f, TextAlignmentOptions.Center, new Vector2(500f, 60f), outlined: true);
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
            UiFactory.Button(root, "SHOP", centre, new Vector2(0f, row), size, () => ShopView.Open(transform, Refresh));
            UiFactory.Button(root, "SETTINGS", centre, new Vector2(350f, row), size, () => SettingsView.Open(transform));
            UiFactory.Button(root, "BACK", centre, new Vector2(700f, row), size, Close);

            if (NetRole.IsOnline)
                training.gameObject.SetActive(false); // training is local only

            Refresh();
            if (_first == null)
                _first = NetRole.IsOnline ? daily : training;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_first.gameObject);
        }

        private void LevelButton(RectTransform root, CampaignLevel level, Vector2 position)
        {
            Button button = UiFactory.Button(root, level.Spec.name, new Vector2(0.5f, 0.5f), position, new Vector2(500f, 110f),
                () => LevelSelection.PlayCampaign(level.Index));
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Left;
            label.margin = new Vector4(16f, 0f, 140f, 0f); // the stars sit on the right
            label.enableAutoSizing = true;
            label.fontSizeMin = 22f;
            label.fontSizeMax = 38f;
            var stars = new Image[RunScore.MaxStars];
            for (int i = 0; i < stars.Length; i++)
                stars[i] = UiFactory.Star(button.transform, "Star " + i, new Vector2(1f, 0.5f), new Vector2(-96f + i * 38f, 0f), 36f, false);
            _refresh.Add(() =>
            {
                SaveData save = Profile.Data;
                bool unlocked = level.Index == 0 || save.IsFinished(GameConfig.Instance.Campaign[level.Index - 1].Spec.id);
                button.interactable = unlocked;
                LevelRecord record = save.Level(level.Spec.id);
                bool finished = record != null && record.finished;
                int earned = finished ? RunScore.Stars((Grade)record.bestGrade) : 0;
                for (int i = 0; i < stars.Length; i++)
                {
                    stars[i].enabled = unlocked;
                    stars[i].color = i < earned ? UiFactory.StarLit : UiFactory.StarDark;
                }
                string line = !unlocked ? "LOCKED" : finished ? $"BEST {record.bestScore:N0}" : "NEW";
                UiFactory.Label(button, $"{level.Index + 1}. {level.Spec.name}\n<size=60%>{line}</size>");
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
    }
}
