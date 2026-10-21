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
            UiFactory.Image(UiFactory.Stretch(root, "Dim"), "Background", Vector2.zero, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f))
                .rectTransform.anchorMax = Vector2.one;
            var centre = new Vector2(0.5f, 0.5f);

            UiFactory.TitledPanel(root, new Vector2(0f, -10f), new Vector2(760f, 640f), "PAUSED", 96f);

            var size = new Vector2(480f, 96f);
            _resume = UiFactory.Button(root, "RESUME", centre, new Vector2(0f, 120f), size, () => _pause.SetPaused(false));
            // Online only the host restarts (for both); the client's MENU leaves the session.
            UiFactory.Button(root, "RESTART", centre, new Vector2(0f, 0f), size, _run.Retry).interactable = !NetRole.IsClient;
            UiFactory.Button(root, "SETTINGS", centre, new Vector2(0f, -120f), size, () => SettingsView.Open(transform));
            UiFactory.Button(root, NetRole.IsClient ? "LEAVE" : "MENU", centre, new Vector2(0f, -240f), size, _run.QuitToMenu);
        }
    }
}
