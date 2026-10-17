using Demolition.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Demolition.Run
{
    /// <summary>Pause overlay built in code: Resume, Restart, Settings, Menu. Shown while
    /// <see cref="PauseController.IsPaused"/>.</summary>
    [RequireComponent(typeof(PauseController))]
    public sealed class PauseView : MonoBehaviour
    {
        private PauseController _pause;
        private RunController _run;
        private GameObject _root;
        private Button _resume;

        private void Awake()
        {
            _pause = GetComponent<PauseController>();
            _run = GetComponent<RunController>();
            PauseController.Changed += OnChanged;
        }

        private void OnDestroy() => PauseController.Changed -= OnChanged;

        private void OnChanged(bool paused)
        {
            if (_root == null)
            {
                if (!paused)
                    return; // unpaused while the scene is torn down: nothing to hide
                Build();
            }
            _root.SetActive(paused);
            if (paused && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_resume.gameObject);
        }

        private void Build()
        {
            Canvas canvas = UiFactory.Canvas(transform, "Pause", 25);
            _root = canvas.gameObject;
            var root = (RectTransform)canvas.transform;
            UiFactory.Image(UiFactory.Stretch(root, "Dim"), "Background", Vector2.zero, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.75f))
                .rectTransform.anchorMax = Vector2.one;
            var centre = new Vector2(0.5f, 0.5f);

            TextMeshProUGUI title = UiFactory.Text(root, "Title", centre, new Vector2(0f, 300f), 96f, TextAlignmentOptions.Center, new Vector2(1200f, 120f));
            title.text = "PAUSED";
            title.color = new Color(1f, 0.85f, 0.3f);

            var size = new Vector2(480f, 96f);
            _resume = UiFactory.Button(root, "RESUME", centre, new Vector2(0f, 140f), size, () => _pause.SetPaused(false));
            UiFactory.Button(root, "RESTART", centre, new Vector2(0f, 20f), size, _run.Retry);
            UiFactory.Button(root, "SETTINGS", centre, new Vector2(0f, -100f), size, () => SettingsView.Open(transform));
            UiFactory.Button(root, "MENU", centre, new Vector2(0f, -220f), size, _run.QuitToMenu);
        }
    }
}
