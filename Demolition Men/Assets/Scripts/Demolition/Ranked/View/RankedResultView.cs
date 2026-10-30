using Demolition.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Demolition.Ranked
{
    /// <summary>
    /// A ranked run's line on the results screen, left of the stars (the first-clear badge is on their
    /// right): SENDING, then the place on the day's board (and whether it's a new best), or what went
    /// wrong with a SEND AGAIN.
    /// An online partner's says the host sends the run. Added by LevelBootstrap in ranked runs.
    /// </summary>
    public sealed class RankedResultView : MonoBehaviour
    {
        private RunController _run;
        private GameObject _root;
        private TextMeshProUGUI _line;
        private Button _again;

        private void Awake() => _run = GetComponent<RunController>();

        private void OnEnable() => RankedSession.Changed += Refresh;

        private void OnDisable() => RankedSession.Changed -= Refresh;

        private void Update()
        {
            if (_root == null && _run != null && _run.State != null && _run.State.IsOver)
            {
                Build();
                Refresh();
            }
        }

        private void Build()
        {
            // Above the results (20), under the pause menu and the shop.
            Canvas canvas = UiFactory.Canvas(transform, "RankedResult", 21);
            _root = canvas.gameObject;
            var root = (RectTransform)canvas.transform;
            var centre = new Vector2(0.5f, 0.5f);
            _line = UiFactory.Text(root, "Ranked", centre, new Vector2(-505f, 205f), 34f, TextAlignmentOptions.Center, new Vector2(370f, 84f), outlined: true);
            _line.color = UiFactory.Gold;
            _line.textWrappingMode = TextWrappingModes.Normal;
            _line.enableAutoSizing = true;
            _line.fontSizeMin = 20f;
            _line.fontSizeMax = 34f;
            _again = UiFactory.Button(root, "SEND AGAIN", centre, new Vector2(-505f, 128f), new Vector2(300f, 56f), RankedSession.Send);
            _again.gameObject.SetActive(false);
        }

        private void Refresh()
        {
            if (_root == null)
                return;
            _again.gameObject.SetActive(RankedSession.CanResend);
            if (NetRole.IsClient)
            {
                _line.text = "RANKED CO-OP: THE HOST SENDS THE RUN";
                return;
            }
            switch (RankedSession.Status)
            {
                case RankedSession.Phase.Sending:
                    _line.text = "RANKED: SENDING THE RUN...";
                    break;
                case RankedSession.Phase.Sent:
                    SubmitResponse result = RankedSession.Result;
                    _line.text = result.rank <= 0 ? "RANKED: NOT FINISHED, NOT ON THE BOARD"
                        : $"RANKED  #{result.rank} OF {result.of}\n" + (result.best ? "YOUR BEST TODAY!" : "YOUR BEST TODAY STAYS");
                    break;
                case RankedSession.Phase.Failed:
                    _line.text = "RANKED: " + RankedSession.Error;
                    break;
                case RankedSession.Phase.Starting:
                    _line.text = "RANKED: GETTING A NEW ATTEMPT...";
                    break;
                default:
                    _line.text = "RANKED TEST: NOT SENT (THE CONSOLE HAS THE VERDICT)";
                    break;
            }
        }
    }
}
