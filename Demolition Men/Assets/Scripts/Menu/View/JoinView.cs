using System.Collections;
using Demolition;
using Demolition.Meta;
using Demolition.Ranked;
using Demolition.Run;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Join lobby panel under the title: one line per player slot plus the controls,
/// and START / ONLINE buttons for mouse and touch. With the touchscreen joined (a phone) it
/// is a play menu instead: big PLAY and PLAY ONLINE buttons, and a player row only while a
/// gamepad is there to join as P2. Both have SETTINGS, so the volume can be set before the
/// first run, and the NAME this device goes by on the ranked boards (<see cref="PlayerIdentity"/>).
/// Fades in. Reads <see cref="Session"/>.</summary>
public class JoinView : MonoBehaviour
{
    private GameObject _root;
    private CanvasGroup _group;
    private System.Action _continue;
    private System.Action _goOnline;

    // Keyboards and gamepads (PC, TV).
    private GameObject _lobby;
    private readonly TextMeshProUGUI[] _slots = new TextMeshProUGUI[Session.MaxPlayers];
    private Button _start;
    private Button _online;

    // The touchscreen as P1 (phone, tablet).
    private GameObject _touch;
    private TextMeshProUGUI _pads;
    private Button _playOnline;
    private TextMeshProUGUI _onlineNote;

    // The name, on both panels.
    private readonly TMP_InputField[] _names = new TMP_InputField[2];
    private int _typedFrame = -1;

    /// <summary>A name is being typed (or was, this frame or last): its keys are the field's, not the lobby's.</summary>
    public bool Typing
    {
        get
        {
            foreach (TMP_InputField field in _names)
                if (field != null && field.isFocused)
                    _typedFrame = Time.frameCount;
            return _typedFrame >= 0 && Time.frameCount - _typedFrame <= 1;
        }
    }

    public void Show(bool visible, System.Action onContinue, System.Action onOnline = null)
    {
        _continue = onContinue;
        _goOnline = onOnline;
        if (_root == null)
            Build();
        _root.SetActive(visible);
        foreach (TMP_InputField field in _names)
            field.SetTextWithoutNotify(PlayerIdentity.Name);
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

        RectTransform lobby = UiFactory.Stretch(root, "Lobby");
        _lobby = lobby.gameObject;
        UiFactory.TitledPanel(lobby, new Vector2(0f, -90f), new Vector2(1200f, 640f), "WHO'S DEMOLISHING?", 64f, 1100f);

        _names[0] = NameRow(lobby, 150f);
        for (int i = 0; i < _slots.Length; i++)
            _slots[i] = UiFactory.Text(lobby, $"P{i + 1}", centre, new Vector2(0f, 50f - i * 90f), 56f, TextAlignmentOptions.Center, new Vector2(1100f, 80f), outlined: true);

        TextMeshProUGUI hint = UiFactory.Text(lobby, "Hint", centre, new Vector2(0f, -170f), 30f, TextAlignmentOptions.Center, new Vector2(1100f, 80f));
        hint.text = "JOIN: A / SPACE     LEAVE: B / ESC\nPLAY: START / ENTER     SETTINGS: SELECT / TAB";
        hint.color = new Color(0.8f, 0.8f, 0.82f);

        _start = UiFactory.Button(lobby, "START", centre, new Vector2(-380f, -310f), new Vector2(340f, 100f), () => _continue?.Invoke());
        _online = UiFactory.Button(lobby, "ONLINE", centre, new Vector2(0f, -310f), new Vector2(340f, 100f), () => _goOnline?.Invoke());
        // Settings at the scene root: under the menu's canvas (this object) it would take that canvas's scaling.
        UiFactory.Button(lobby, "SETTINGS", centre, new Vector2(380f, -310f), new Vector2(340f, 100f), () => SettingsView.Open(null));

        RectTransform touch = UiFactory.Stretch(root, "Touch");
        _touch = touch.gameObject;
        UiFactory.TitledPanel(touch, new Vector2(0f, -90f), new Vector2(1200f, 560f), "READY TO DEMOLISH?", 64f, 1100f);

        _names[1] = NameRow(touch, 105f);
        _pads = UiFactory.Text(touch, "Players", centre, new Vector2(0f, 30f), 40f, TextAlignmentOptions.Center, new Vector2(1100f, 60f), outlined: true);

        Button play = UiFactory.Button(touch, "PLAY", centre, new Vector2(-270f, -90f), new Vector2(480f, 150f), () => _continue?.Invoke());
        UiFactory.Label(play, "PLAY", 60f);
        _playOnline = UiFactory.Button(touch, "PLAY ONLINE", centre, new Vector2(270f, -90f), new Vector2(480f, 150f), () => _goOnline?.Invoke());
        UiFactory.Label(_playOnline, "PLAY ONLINE", 56f);

        _onlineNote = UiFactory.Text(touch, "OnlineNote", centre, new Vector2(270f, -225f), 28f, TextAlignmentOptions.Center, new Vector2(600f, 80f));
        _onlineNote.text = "CO-OP WITH A FRIEND ON\nANOTHER PHONE, PC OR BROWSER";

        UiFactory.Button(touch, "SETTINGS", centre, new Vector2(-270f, -235f), new Vector2(360f, 90f), () => SettingsView.Open(null));
    }

    // NAME and its field: what this device is called on the ranked boards. A name that isn't
    // allowed (too short, or on PlayerName's list) goes back to the one it had.
    private TMP_InputField NameRow(RectTransform panel, float y)
    {
        var centre = new Vector2(0.5f, 0.5f);
        TextMeshProUGUI label = UiFactory.Text(panel, "NameLabel", centre, new Vector2(-330f, y), 40f, TextAlignmentOptions.Right,
            new Vector2(220f, 70f), outlined: true);
        label.text = "NAME";
        TMP_InputField field = UiFactory.InputField(panel, "Name", centre, new Vector2(110f, y), new Vector2(620f, 72f), 40f, PlayerName.MaxLength);
        ((TextMeshProUGUI)field.placeholder).text = "TYPE YOUR NAME";
        field.onValidateInput = (text, index, c) => PlayerName.Accept(c);
        field.onEndEdit.AddListener(typed =>
        {
            _typedFrame = Time.frameCount;
            PlayerIdentity.Rename(typed);
            foreach (TMP_InputField other in _names)
                other.SetTextWithoutNotify(PlayerIdentity.Name);
        });
        return field;
    }

    private void LateUpdate()
    {
        if (_root == null || !_root.activeSelf)
            return;
        bool touch = Touchscreen.current != null && Session.IsJoined(Touchscreen.current);
        _lobby.SetActive(!touch);
        _touch.SetActive(touch);
        if (touch)
            ShowTouch();
        else
            ShowLobby();
    }

    private void ShowLobby()
    {
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

    // Local co-op on a phone needs a gamepad for P2: without one there's nobody to call.
    private void ShowTouch()
    {
        InputDevice second = Session.DeviceFor(1);
        _pads.gameObject.SetActive(second != null || Gamepad.all.Count > 0);
        string gold = ColorUtility.ToHtmlStringRGB(UiFactory.Gold);
        _pads.text = second != null
            ? $"<color=#{gold}>P1  TOUCH     P2  {DeviceName(second)}"
            : $"<color=#{gold}>P1  TOUCH</color>     <alpha=#66>P2  PRESS A TO JOIN";
        _playOnline.interactable = Session.PlayerCount == 1;
        _onlineNote.color = new Color(0.8f, 0.8f, 0.82f, _playOnline.interactable ? 1f : 0.4f);
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
