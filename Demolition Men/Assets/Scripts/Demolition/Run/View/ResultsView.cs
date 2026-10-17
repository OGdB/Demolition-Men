using System.Text;
using Demolition.Ads;
using Demolition.Levels;
using Demolition.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Demolition.Run
{
    /// <summary>End-of-run screen: grade, score breakdown per player, time against par, the
    /// cash banked (doubled by a rewarded ad on phones), and Next / Retry / Menu. Built in
    /// code when the run ends.</summary>
    [RequireComponent(typeof(RunController))]
    public sealed class ResultsView : MonoBehaviour
    {
        private static readonly Color Gold = new Color(1f, 0.85f, 0.3f);

        /// <summary>Jump buttons double as Submit: don't let a held jump skip the results.</summary>
        private const float InputDelay = 0.6f;

        private RunController _run;
        private bool _shown;
        private bool _leaving;
        private float _readyAt;
        private CanvasGroup _group;
        private Button _first;
        private TextMeshProUGUI _cash;
        private Button _double;

        private void Awake()
        {
            _run = GetComponent<RunController>();
        }

        private void Update()
        {
            if (!_shown && _run.State.IsOver)
                Show();
            if (_shown && !_group.interactable && !_leaving && Time.unscaledTime >= _readyAt)
            {
                _group.interactable = true;
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(_first.gameObject);
            }
        }

        // Each exit fires once, after any ad between runs.
        private void Leave(System.Action action)
        {
            if (_leaving)
                return;
            _leaving = true;
            _group.interactable = false;
            AdManager.BetweenRuns(action);
        }

        private void Show()
        {
            _shown = true;
            RunState state = _run.State;
            RunScore score = _run.Score;
            RunReward reward = _run.Reward;
            bool finished = state.Phase == RunPhase.Finished;

            Canvas canvas = UiFactory.Canvas(transform, "Results", 20);
            var root = (RectTransform)canvas.transform;
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _readyAt = Time.unscaledTime + InputDelay;
            Image dim = UiFactory.Image(UiFactory.Stretch(root, "Dim"), "Background", Vector2.zero, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.75f));
            dim.rectTransform.anchorMax = Vector2.one;
            var centre = new Vector2(0.5f, 0.5f);

            TextMeshProUGUI title = UiFactory.Text(root, "Title", centre, new Vector2(0f, 390f), 96f, TextAlignmentOptions.Center, new Vector2(1400f, 120f));
            title.text = finished ? "DEMOLISHED!" : "WRECKED";
            title.color = finished ? Gold : new Color(1f, 0.4f, 0.3f);

            string tag = reward.FirstClear ? "FIRST CLEAR!" : reward.NewBestGrade ? "NEW BEST GRADE!" : reward.NewBestScore ? "NEW BEST SCORE!" : string.Empty;
            TextMeshProUGUI badge = UiFactory.Text(root, "Badge", centre, new Vector2(0f, 310f), 40f, TextAlignmentOptions.Center, new Vector2(1000f, 50f));
            badge.text = tag;
            badge.color = new Color(0.5f, 1f, 0.5f);

            if (finished)
            {
                TextMeshProUGUI grade = UiFactory.Text(root, "Grade", centre, new Vector2(-520f, 60f), 260f, TextAlignmentOptions.Center, new Vector2(300f, 300f));
                grade.text = RunScore.GradeFor(_run.DestroyedFraction).ToString();
                grade.color = Gold;
            }

            var lines = new StringBuilder();
            lines.AppendLine($"TIME  {RunHud.FormatTime(state.Elapsed)}   (PAR {RunHud.FormatTime(_run.ParTime)})");
            lines.AppendLine($"DESTRUCTION  {Mathf.FloorToInt(_run.DestroyedFraction * 100f)}%");
            for (int i = 0; i < score.PlayerCount; i++)
                lines.AppendLine($"P{i + 1}  {score.Points(i):N0}");
            if (score.Unattributed > 0)
                lines.AppendLine($"COLLATERAL  {score.Unattributed:N0}");
            if (finished)
                lines.AppendLine($"SPEED BONUS  x{RunScore.SpeedMultiplier(state.Elapsed, _run.ParTime):0.00}");
            lines.Append($"<size=130%>TOTAL  {_run.FinalScore:N0}</size>");

            TextMeshProUGUI body = UiFactory.Text(root, "Body", centre, new Vector2(finished ? 150f : 0f, 60f), 40f,
                finished ? TextAlignmentOptions.Left : TextAlignmentOptions.Center, new Vector2(900f, 420f));
            body.text = lines.ToString();

            _cash = UiFactory.Text(root, "Cash", centre, new Vector2(0f, -190f), 52f, TextAlignmentOptions.Center, new Vector2(1200f, 70f), outlined: true);
            _cash.color = Gold;
            ShowCash();

            var size = new Vector2(300f, 90f);
            bool next = finished && _run.HasNextLevel;
            Button first = null;
            if (next)
                first = UiFactory.Button(root, "NEXT", centre, new Vector2(-360f, -320f), size, () => Leave(_run.NextLevel));
            Button retry = UiFactory.Button(root, "RETRY", centre, new Vector2(next ? 0f : -180f, -320f), size, () => Leave(_run.Retry));
            UiFactory.Button(root, "MENU", centre, new Vector2(next ? 360f : 180f, -320f), size, () => Leave(_run.QuitToMenu));

            if (reward.Cash > 0 && AdManager.CanOfferReward)
                _double = UiFactory.Button(root, "WATCH AD: 2x CASH", centre, new Vector2(0f, -430f), new Vector2(640f, 80f), DoubleCash);

            _first = first != null ? first : retry;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void ShowCash() => _cash.text = $"+${_run.Reward.Cash:N0}    BANK ${Profile.Data.cash:N0}";

        private void DoubleCash()
        {
            _double.interactable = false;
            AdManager.ShowRewarded(earned =>
            {
                if (this == null)
                    return; // left the scene while the ad played
                if (earned)
                    _run.DoubleReward();
                if (_double != null)
                    _double.gameObject.SetActive(false);
                ShowCash();
            });
        }
    }
}
