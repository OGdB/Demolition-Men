using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Demolition.Run
{
    /// <summary>End-of-run screen: grade, score breakdown per player, time against par,
    /// and Retry / Menu. Built in code when the run ends.</summary>
    [RequireComponent(typeof(RunController))]
    public sealed class ResultsView : MonoBehaviour
    {
        private RunController _run;
        private bool _shown;

        private void Awake()
        {
            _run = GetComponent<RunController>();
        }

        private void Update()
        {
            if (!_shown && _run.State.IsOver)
                Show();
        }

        private void Show()
        {
            _shown = true;
            RunState state = _run.State;
            RunScore score = _run.Score;
            bool finished = state.Phase == RunPhase.Finished;

            Canvas canvas = UiFactory.Canvas(transform, "Results", 20);
            var root = (RectTransform)canvas.transform;
            Image dim = UiFactory.Image(UiFactory.Stretch(root, "Dim"), "Background", Vector2.zero, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.75f));
            dim.rectTransform.anchorMax = Vector2.one;
            var centre = new Vector2(0.5f, 0.5f);

            TextMeshProUGUI title = UiFactory.Text(root, "Title", centre, new Vector2(0f, 330f), 96f, TextAlignmentOptions.Center, new Vector2(1400f, 120f));
            title.text = finished ? "DEMOLISHED!" : "WRECKED";
            title.color = finished ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.4f, 0.3f);

            if (finished)
            {
                TextMeshProUGUI grade = UiFactory.Text(root, "Grade", centre, new Vector2(-520f, 40f), 260f, TextAlignmentOptions.Center, new Vector2(300f, 300f));
                grade.text = RunScore.GradeFor(_run.DestroyedFraction).ToString();
                grade.color = new Color(1f, 0.85f, 0.3f);
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

            TextMeshProUGUI body = UiFactory.Text(root, "Body", centre, new Vector2(finished ? 150f : 0f, 30f), 40f,
                finished ? TextAlignmentOptions.Left : TextAlignmentOptions.Center, new Vector2(900f, 420f));
            body.text = lines.ToString();

            Button retry = UiFactory.Button(root, "RETRY", centre, new Vector2(-180f, -300f), new Vector2(300f, 80f), _run.Retry);
            UiFactory.Button(root, "MENU", centre, new Vector2(180f, -300f), new Vector2(300f, 80f), _run.QuitToMenu);

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(retry.gameObject);
        }
    }
}
