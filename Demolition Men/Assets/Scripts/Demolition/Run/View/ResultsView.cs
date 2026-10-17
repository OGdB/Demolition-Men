using System.Collections;
using System.Text;
using Demolition.Ads;
using Demolition.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Demolition.Run
{
    /// <summary>End-of-run screen, built in code when the run ends. It plays a short reveal:
    /// the title slams down, the demolition meter fills and lights a star at 40, 60 and 80%
    /// (finished runs only), the total and the cash count up, then any record stamp lands.
    /// Any press skips the reveal. Then Next / Retry / Shop / Menu, and the rewarded
    /// "2x cash" on phones.</summary>
    [RequireComponent(typeof(RunController))]
    public sealed class ResultsView : MonoBehaviour
    {
        private static readonly Color Wrecked = new Color(1f, 0.4f, 0.3f);

        /// <summary>Jump buttons double as Submit: don't let a held jump skip the results.</summary>
        private const float InputDelay = 0.6f;
        /// <summary>Attack and jump mashed across the finish must not skip the reveal.</summary>
        private const float SkipDelay = 1f;
        /// <summary>A skip press must not land on a button that wakes right under it.</summary>
        private const float WakeDelay = 0.4f;
        private const float MeterWidth = 1000f;

        private RunController _run;
        private bool _shown;
        private bool _leaving;
        private bool _woken;
        private bool _revealed;
        private bool _skip;
        private float _shownAt;
        private float _revealedAt;
        private CanvasGroup _group;
        private RectTransform _content;
        private Button _first;
        private Button _double;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _meterText;
        private RectTransform _meterFill;
        private readonly Image[] _stars = new Image[RunScore.MaxStars];
        private TextMeshProUGUI _total;
        private TextMeshProUGUI _cash;
        private TextMeshProUGUI _bank;
        private TextMeshProUGUI _badge;

        private void Awake()
        {
            _run = GetComponent<RunController>();
        }

        private void Update()
        {
            if (!_shown && _run.State.IsOver)
                Show();
            if (!_shown)
                return;
            if (!_revealed && Time.unscaledTime >= _shownAt + SkipDelay && UiFactory.SkipPressed())
                _skip = true;
            // Its own flag: a modal ad turning the group off must not count as "not woken yet".
            if (_revealed && !_woken && !_leaving && Time.unscaledTime >= Mathf.Max(_shownAt + InputDelay, _revealedAt + WakeDelay))
            {
                _woken = true;
                _group.interactable = true;
                _group.blocksRaycasts = true;
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
            _shownAt = Time.unscaledTime;
            bool finished = _run.State.Phase == RunPhase.Finished;

            Canvas canvas = UiFactory.Canvas(transform, "Results", 20);
            var root = (RectTransform)canvas.transform;
            _group = canvas.gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false; // a tap that skips the reveal must not press the button under it
            UiFactory.Image(UiFactory.Stretch(root, "Dim"), "Background", Vector2.zero, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.7f))
                .rectTransform.anchorMax = Vector2.one;
            _content = UiFactory.Stretch(root, "Content");
            _content.pivot = new Vector2(0.5f, 0.5f); // scale from the middle
            var centre = new Vector2(0.5f, 0.5f);

            UiFactory.Panel(_content, new Vector2(0f, 10f), new Vector2(1400f, 880f));
            _title = UiFactory.Title(_content, finished ? "DEMOLISHED!" : "WRECKED", centre, new Vector2(0f, 406f), 110f);
            if (!finished)
                _title.color = Wrecked;

            for (int i = 0; i < _stars.Length; i++)
            {
                bool middle = i == 1;
                _stars[i] = UiFactory.Star(_content, "Star " + i, centre, new Vector2((i - 1) * 230f, middle ? 258f : 240f), middle ? 190f : 160f, false);
            }

            _meterText = UiFactory.Text(_content, "Demolition", centre, new Vector2(0f, 122f), 40f, TextAlignmentOptions.Center, new Vector2(800f, 50f), outlined: true);
            BuildMeter(_content, new Vector2(0f, 75f));

            var stats = new StringBuilder();
            RunScore score = _run.Score;
            stats.AppendLine($"TIME  {RunHud.FormatTime(_run.State.Elapsed)}   <alpha=#99>PAR {RunHud.FormatTime(_run.ParTime)}</alpha>");
            for (int i = 0; i < score.PlayerCount; i++)
                stats.AppendLine($"P{i + 1}  {score.Points(i):N0}");
            if (score.Unattributed > 0)
                stats.AppendLine($"COLLATERAL  {score.Unattributed:N0}");
            if (finished)
                stats.AppendLine($"SPEED BONUS  x{RunScore.SpeedMultiplier(_run.State.Elapsed, _run.ParTime):0.00}");
            TextMeshProUGUI body = UiFactory.Text(_content, "Stats", centre, new Vector2(-300f, -115f), 36f, TextAlignmentOptions.Left, new Vector2(560f, 230f));
            body.text = stats.ToString();

            TextMeshProUGUI totalLabel = UiFactory.Text(_content, "TotalLabel", centre, new Vector2(330f, -20f), 32f, TextAlignmentOptions.Center, new Vector2(520f, 44f));
            totalLabel.text = "TOTAL";
            _total = UiFactory.Text(_content, "Total", centre, new Vector2(330f, -85f), 80f, TextAlignmentOptions.Center, new Vector2(560f, 96f), outlined: true);
            _cash = UiFactory.Text(_content, "Cash", centre, new Vector2(330f, -160f), 52f, TextAlignmentOptions.Center, new Vector2(560f, 64f), outlined: true);
            _cash.color = UiFactory.Gold;
            _bank = UiFactory.Text(_content, "Bank", centre, new Vector2(330f, -212f), 30f, TextAlignmentOptions.Center, new Vector2(560f, 40f));

            RunReward reward = _run.Reward;
            _badge = UiFactory.Text(_content, "Badge", centre, new Vector2(500f, 270f), 44f, TextAlignmentOptions.Center, new Vector2(380f, 60f), outlined: true);
            _badge.text = reward.FirstClear ? "FIRST CLEAR!" : reward.NewBestGrade ? "MORE STARS!" : reward.NewBestScore ? "NEW BEST!" : string.Empty;
            _badge.color = new Color(0.5f, 1f, 0.5f);
            _badge.rectTransform.localEulerAngles = new Vector3(0f, 0f, -10f);

            BuildButtons(finished, reward);
            ShowCount(0, 0);
            _meterText.text = "DEMOLITION  0%";
            _badge.gameObject.SetActive(false);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            StartCoroutine(Reveal(finished));
        }

        private void BuildMeter(RectTransform parent, Vector2 position)
        {
            const float height = 40f;
            var centre = new Vector2(0.5f, 0.5f);
            Image frame = UiFactory.Image(parent, "Meter", centre, position, new Vector2(MeterWidth, height), UiFactory.Ink);
            Image back = UiFactory.Image(frame.transform, "Back", Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.18f, 0.19f, 0.22f));
            back.rectTransform.anchorMax = Vector2.one;
            back.rectTransform.offsetMin = new Vector2(4f, 4f);
            back.rectTransform.offsetMax = new Vector2(-4f, -4f);
            Image fill = UiFactory.Image(back.transform, "Fill", Vector2.zero, Vector2.zero, Vector2.zero, Color.white);
            fill.sprite = UiFactory.HazardSprite;
            fill.type = Image.Type.Tiled;
            _meterFill = fill.rectTransform;
            _meterFill.anchorMax = new Vector2(0f, 1f);

            for (int star = 0; star < RunScore.MaxStars; star++)
            {
                float at = RunScore.StarThreshold(star);
                UiFactory.Image(frame.transform, "Tick " + star, new Vector2(at, 0.5f), Vector2.zero, new Vector2(4f, height + 12f), Color.white)
                    .rectTransform.pivot = new Vector2(0.5f, 0.5f);
                TextMeshProUGUI mark = UiFactory.Text(frame.transform, "Mark " + star, new Vector2(at, 0.5f), new Vector2(0f, -height * 0.5f - 22f), 26f,
                    TextAlignmentOptions.Center, new Vector2(100f, 32f));
                mark.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                mark.text = $"{Mathf.RoundToInt(at * 100f)}%";
                mark.color = new Color(0.75f, 0.75f, 0.78f);
            }
        }

        private void BuildButtons(bool finished, RunReward reward)
        {
            var centre = new Vector2(0.5f, 0.5f);
            var size = new Vector2(280f, 90f);
            if (NetRole.IsClient)
            {
                // Online the host picks NEXT / RETRY / MENU and this device follows; MENU here leaves.
                TextMeshProUGUI waiting = UiFactory.Text(_content, "HostPicks", centre, new Vector2(-330f, -350f), 34f,
                    TextAlignmentOptions.Center, new Vector2(560f, 90f), outlined: true);
                waiting.text = "THE HOST PICKS\nWHAT'S NEXT";
                _first = UiFactory.Button(_content, "SHOP", centre, new Vector2(160f, -350f), size, () => ShopView.Open(transform, ShowCash));
                UiFactory.Button(_content, "LEAVE", centre, new Vector2(470f, -350f), size, () => Leave(_run.QuitToMenu));
                return;
            }
            bool next = finished && _run.HasNextLevel;
            float x = next ? -465f : -310f;
            Button first = null;
            if (next)
            {
                first = UiFactory.Button(_content, "NEXT", centre, new Vector2(x, -350f), size, () => Leave(_run.NextLevel));
                x += 310f;
            }
            Button retry = UiFactory.Button(_content, "RETRY", centre, new Vector2(x, -350f), size, () => Leave(_run.Retry));
            UiFactory.Button(_content, "SHOP", centre, new Vector2(x + 310f, -350f), size, () => ShopView.Open(transform, ShowCash));
            UiFactory.Button(_content, "MENU", centre, new Vector2(x + 620f, -350f), size, () => Leave(_run.QuitToMenu));

            if (reward.Cash > 0 && AdManager.CanOfferReward)
                _double = UiFactory.Button(_content, "WATCH AD: 2x CASH", centre, new Vector2(0f, -490f), new Vector2(640f, 80f), DoubleCash);
            _first = first != null ? first : retry;
        }

        // ---------------- Reveal ----------------

        private IEnumerator Reveal(bool finished)
        {
            float destroyed = _run.BankedFraction;
            int stars = finished ? RunScore.Stars(RunScore.GradeFor(destroyed)) : 0;

            yield return Tween(0.25f, t =>
            {
                float s = Mathf.Lerp(0.9f, 1f, EaseOut(t));
                _content.localScale = new Vector3(s, s, 1f);
                _group.alpha = t;
            });

            yield return Tween(0.18f, t =>
            {
                float s = Mathf.Lerp(2.2f, 1f, t * t);
                _title.rectTransform.localScale = new Vector3(s, s, 1f);
            });
            if (!_skip)
            {
                if (finished)
                    UiAudio.Slam();
                else
                    UiAudio.Fail();
                yield return Tween(0.25f, t => _content.anchoredPosition = Random.insideUnitCircle * 14f * (1f - t));
            }
            _content.anchoredPosition = Vector2.zero;

            int lit = 0;
            int lastPercent = 0;
            yield return Tween(0.4f + 1.2f * destroyed, t =>
            {
                float shown = destroyed * EaseOut(t);
                SetMeter(shown);
                int percent = Mathf.FloorToInt(shown * 100f);
                if (!_skip && percent >= lastPercent + 2)
                {
                    lastPercent = percent;
                    UiAudio.Tick(0.8f + shown);
                }
                while (lit < stars && shown >= RunScore.StarThreshold(lit))
                    StartCoroutine(PopStar(lit++));
            });
            while (lit < stars)
                StartCoroutine(PopStar(lit++));

            int total = _run.BankedScore;
            int cash = _run.Reward.Cash;
            float lastCoin = 0f;
            yield return Tween(cash > 0 ? 0.8f : 0.5f, t =>
            {
                float e = EaseOut(t);
                ShowCount(Mathf.RoundToInt(total * e), Mathf.RoundToInt(cash * e));
                if (!_skip && cash > 0 && t - lastCoin > 0.12f)
                {
                    lastCoin = t;
                    UiAudio.Coin();
                }
            });
            ShowCash();

            if (_badge.text.Length > 0)
            {
                _badge.gameObject.SetActive(true);
                yield return Tween(0.15f, t =>
                {
                    float s = Mathf.Lerp(2.5f, 1f, t * t);
                    _badge.rectTransform.localScale = new Vector3(s, s, 1f);
                });
                if (!_skip)
                    UiAudio.Slam();
            }
            _revealed = true;
            _revealedAt = Time.unscaledTime;
        }

        private IEnumerator PopStar(int index)
        {
            Image star = _stars[index];
            star.color = UiFactory.StarLit;
            if (!_skip)
                UiAudio.Star(index);
            yield return Tween(0.3f, t =>
            {
                // Overshoot, then settle: a stamp, not a fade.
                float s = 1f + 0.8f * (1f - t) * Mathf.Cos(t * Mathf.PI * 1.5f);
                star.rectTransform.localScale = new Vector3(s, s, 1f);
            });
        }

        private IEnumerator Tween(float seconds, System.Action<float> apply) => UiFactory.Tween(seconds, apply, () => _skip);

        private static float EaseOut(float t) => UiFactory.EaseOut(t);

        private void SetMeter(float fraction)
        {
            _meterFill.anchorMax = new Vector2(fraction, 1f);
            _meterText.text = $"DEMOLITION  {Mathf.FloorToInt(fraction * 100f)}%";
        }

        private void ShowCount(int total, int cash)
        {
            _total.text = total.ToString("N0");
            _cash.text = $"+${cash:N0}";
            _bank.text = $"BANK ${Profile.Data.cash:N0}";
        }

        private void ShowCash() => ShowCount(_run.BankedScore, _run.Reward.Cash);

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
