using System;
using TMPro;
using UnityEngine;

namespace Demolition.Ads
{
    /// <summary>Stand-in ads for development builds: a full-screen card that closes after a
    /// few seconds, so the ad flow can be tested without an SDK.</summary>
    public sealed class SimulatedAdService : IAdService
    {
        private const float Seconds = 2.5f;

        public bool IsInterstitialReady => true;
        public bool IsRewardedReady => true;

        public void ShowInterstitial(Action closed) => Show("AD BREAK", "(simulated interstitial)", () => closed?.Invoke());

        public void ShowRewarded(Action<bool> finished) => Show("THANKS FOR WATCHING", "(simulated rewarded ad)", () => finished?.Invoke(true));

        private static void Show(string title, string subtitle, Action done)
        {
            Canvas canvas = UiFactory.Canvas(null, "SimulatedAd", 100);
            UnityEngine.Object.DontDestroyOnLoad(canvas.gameObject);
            var root = (RectTransform)canvas.transform;
            UiFactory.Image(UiFactory.Stretch(root, "Back"), "Fill", Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.1f, 0.1f, 0.2f, 1f))
                .rectTransform.anchorMax = Vector2.one;
            var centre = new Vector2(0.5f, 0.5f);
            TextMeshProUGUI text = UiFactory.Text(root, "Title", centre, new Vector2(0f, 40f), 90f, TextAlignmentOptions.Center, new Vector2(1600f, 120f));
            text.text = title;
            TextMeshProUGUI sub = UiFactory.Text(root, "Subtitle", centre, new Vector2(0f, -60f), 40f, TextAlignmentOptions.Center, new Vector2(1600f, 60f));
            sub.text = subtitle;
            canvas.gameObject.AddComponent<AdCard>().Begin(Seconds, done);
        }

        private sealed class AdCard : MonoBehaviour
        {
            private float _until;
            private Action _done;

            public void Begin(float seconds, Action done)
            {
                _until = Time.unscaledTime + seconds;
                _done = done;
            }

            private void Update()
            {
                if (Time.unscaledTime < _until)
                    return;
                Destroy(gameObject);
                _done?.Invoke();
                _done = null;
            }
        }
    }
}
