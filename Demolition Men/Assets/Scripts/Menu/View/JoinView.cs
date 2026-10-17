using System.Collections;
using Demolition;
using Demolition.Run;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Join lobby panel under the title: one line per player slot plus the controls,
/// and a START button for mouse and touch. Fades in. Reads <see cref="Session"/>.</summary>
public class JoinView : MonoBehaviour
{
    private GameObject _root;
    private CanvasGroup _group;
    private readonly TextMeshProUGUI[] _slots = new TextMeshProUGUI[Session.MaxPlayers];
    private Button _start;
    private Button _online;
    private System.Action _continue;
    private System.Action _goOnline;

    public void Show(bool visible, System.Action onContinue, System.Action onOnline = null)
    {
        _continue = onContinue;
        _goOnline = onOnline;
        if (_root == null)
            Build();
        _root.SetActive(visible);
        if (visible)
            StartCoroutine(UiFactory.Tween(0.3f, t => _group.alpha = t, () => false));
    }

    private void Build()
    {
        UiFactory.EnsureEventSystem();
        // A root canvas: nested under the menu's canvas it would inherit that canvas's rect and scaling.
        Canvas canvas = UiFactory.Canvas(null, "JoinScreen", 30);
        _root = canvas.gameObject;
        _group = _root.AddComponent<CanvasGroup>();
        var root = (RectTransform)canvas.transform;
        var centre = new Vector2(0.5f, 0.5f);

        UiFactory.Panel(root, new Vector2(0f, -90f), new Vector2(1200f, 640f));
        UiFactory.Title(root, "WHO'S DEMOLISHING?", centre, new Vector2(0f, 186f), 64f, 1100f);

        for (int i = 0; i < _slots.Length; i++)
            _slots[i] = UiFactory.Text(root, $"P{i + 1}", centre, new Vector2(0f, 80f - i * 100f), 56f, TextAlignmentOptions.Center, new Vector2(1100f, 80f), outlined: true);

        TextMeshProUGUI hint = UiFactory.Text(root, "Hint", centre, new Vector2(0f, -170f), 30f, TextAlignmentOptions.Center, new Vector2(1100f, 50f));
        hint.text = "JOIN: A / SPACE     LEAVE: B / ESC     PLAY: START / ENTER";
        hint.color = new Color(0.8f, 0.8f, 0.82f);

        _start = UiFactory.Button(root, "START", centre, new Vector2(-200f, -310f), new Vector2(360f, 100f), () => _continue?.Invoke());
        _online = UiFactory.Button(root, "ONLINE", centre, new Vector2(200f, -310f), new Vector2(360f, 100f), () => _goOnline?.Invoke());
    }

    private void LateUpdate()
    {
        if (_root == null || !_root.activeSelf)
            return;
        for (int i = 0; i < _slots.Length; i++)
        {
            InputDevice device = Session.DeviceFor(i);
            _slots[i].text = device != null
                ? $"P{i + 1}  {DeviceName(device)}"
                : $"<alpha=#66>P{i + 1}  PRESS A / SPACE TO JOIN";
            _slots[i].color = device != null ? UiFactory.Gold : Color.white;
        }
        _start.interactable = Session.PlayerCount > 0;
        _online.interactable = Session.PlayerCount == 1; // one player per device online
    }

    private static string DeviceName(InputDevice device)
    {
        if (device is Keyboard)
            return "KEYBOARD";
        if (device is Touchscreen)
            return "TOUCH";
        int index = 0;
        for (int i = 0; i < Gamepad.all.Count; i++)
            if (Gamepad.all[i] == device)
                index = i + 1;
        return $"GAMEPAD {index}";
    }
}
