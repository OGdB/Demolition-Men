using Demolition;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>A message with an OK button over the menu (why an online session ended).
/// Esc / B / Enter close it too.</summary>
public class NoticeView : MonoBehaviour
{
    private System.Action _unblock;
    private GameObject _previousSelection;
    private int _openedFrame;

    public static void Show(string title, string message)
    {
        UiFactory.EnsureEventSystem();
        new GameObject("Notice").AddComponent<NoticeView>().Build(title, message);
    }

    private void Build(string title, string message)
    {
        _openedFrame = Time.frameCount;
        _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        Canvas canvas = UiFactory.Canvas(transform, "NoticeCanvas", 60);
        _unblock = UiFactory.Modal(canvas);
        var root = (RectTransform)canvas.transform;
        UiFactory.Backdrop(root, new Color(0f, 0f, 0f, 0.6f));
        var centre = new Vector2(0.5f, 0.5f);
        UiFactory.Panel(root, Vector2.zero, new Vector2(1100f, 420f));
        UiFactory.Title(root, title, centre, new Vector2(0f, 166f), 64f, 1000f);
        var text = UiFactory.Text(root, "Message", centre, new Vector2(0f, 30f), 38f, TMPro.TextAlignmentOptions.Center, new Vector2(980f, 140f));
        text.textWrappingMode = TMPro.TextWrappingModes.Normal;
        text.text = message;
        Button ok = UiFactory.Button(root, "OK", centre, new Vector2(0f, -120f), new Vector2(280f, 96f), Close);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(ok.gameObject);
    }

    private void Update()
    {
        if (Time.frameCount == _openedFrame)
            return;
        bool close = Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame);
        foreach (Gamepad pad in Gamepad.all)
            close |= pad.buttonEast.wasPressedThisFrame;
        if (close)
            Close();
    }

    private void Close() => Destroy(gameObject);

    private void OnDestroy()
    {
        _unblock?.Invoke();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_previousSelection);
    }
}
