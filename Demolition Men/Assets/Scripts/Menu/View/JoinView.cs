using Demolition;
using Demolition.Run;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Join lobby overlay: one line per player slot plus the controls. Reads <see cref="Session"/>.</summary>
public class JoinView : MonoBehaviour
{
    private GameObject _root;
    private readonly TextMeshProUGUI[] _slots = new TextMeshProUGUI[Session.MaxPlayers];
    private TextMeshProUGUI _hint;

    public void Show(bool visible)
    {
        if (_root == null)
            Build();
        _root.SetActive(visible);
    }

    private void Build()
    {
        // A root canvas: nested under the menu's canvas it would inherit that canvas's rect and scaling.
        Canvas canvas = UiFactory.Canvas(null, "JoinScreen", 30);
        _root = canvas.gameObject;
        var root = (RectTransform)canvas.transform;
        var centre = new Vector2(0.5f, 0.5f);

        UiFactory.Image(UiFactory.Stretch(root, "Dim"), "Background", Vector2.zero, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.8f))
            .rectTransform.anchorMax = Vector2.one;

        TextMeshProUGUI title = UiFactory.Text(root, "Title", centre, new Vector2(0f, 300f), 80f, TextAlignmentOptions.Center, new Vector2(1400f, 110f));
        title.text = "WHO'S DEMOLISHING?";
        title.color = new Color(1f, 0.85f, 0.3f);

        for (int i = 0; i < _slots.Length; i++)
            _slots[i] = UiFactory.Text(root, $"P{i + 1}", centre, new Vector2(0f, 110f - i * 110f), 56f, TextAlignmentOptions.Center, new Vector2(1400f, 80f));

        _hint = UiFactory.Text(root, "Hint", centre, new Vector2(0f, -300f), 32f, TextAlignmentOptions.Center, new Vector2(1600f, 50f));
        _hint.text = "JOIN: A / SPACE     LEAVE: B / ESC     PLAY: START / ENTER";
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
        }
    }

    private static string DeviceName(InputDevice device)
    {
        if (device is Keyboard)
            return "KEYBOARD";
        int index = 0;
        for (int i = 0; i < Gamepad.all.Count; i++)
            if (Gamepad.all[i] == device)
                index = i + 1;
        return $"GAMEPAD {index}";
    }
}
