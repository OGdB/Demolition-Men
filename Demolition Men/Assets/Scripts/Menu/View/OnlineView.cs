using System;
using Demolition;
using Demolition.Meta;
using Demolition.Online;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Online co-op from the lobby (docs/multiplayer.md §6), one local player per device.
/// HOST opens a room on the relay and shows its code, big enough to read on a TV; JOIN
/// takes a code from an on-screen keypad (touch, gamepad) or the keyboard. Connected, the
/// host picks a level (the level select) and the client waits here, with the shop.
/// Esc / B goes back a step; leaving a room ends the session.
/// </summary>
public class OnlineView : MonoBehaviour
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // the relay's codes: no 0/O, 1/I
    private const int CodeLength = 6;

    private enum Step { Choose, Hosting, Joining, Connecting, Waiting }

    private Action _back;
    private Action _hostConnected;
    private Step _step;
    private string _typed = string.Empty;
    private int _changedFrame;
    private RectTransform _root;
    private RectTransform _page;
    private TextMeshProUGUI _status;
    private TextMeshProUGUI _code;

    public static bool IsOpen { get; private set; }

    /// <param name="back">Closed without a session.</param>
    /// <param name="hostConnected">As host, the other player joined: pick a level.</param>
    public static void Open(Action back, Action hostConnected) => Create(back, hostConnected, Step.Choose);

    /// <summary>Client back in the menu between runs: wait for the host's choice.</summary>
    public static void OpenWaiting(Action back) => Create(back, null, Step.Waiting);

    private static void Create(Action back, Action hostConnected, Step step)
    {
        UiFactory.EnsureEventSystem();
        var view = new GameObject("Online").AddComponent<OnlineView>();
        view._back = back;
        view._hostConnected = hostConnected;
        view.Build();
        view.Show(step);
    }

    private void OnEnable()
    {
        IsOpen = true;
        OnlineSession.CodeReady += OnCode;
        OnlineSession.Connected += OnConnected;
        OnlineSession.Ended += OnEnded;
    }

    private void OnDisable()
    {
        IsOpen = false;
        OnlineSession.CodeReady -= OnCode;
        OnlineSession.Connected -= OnConnected;
        OnlineSession.Ended -= OnEnded;
        if (Keyboard.current != null)
            Keyboard.current.onTextInput -= OnText;
    }

    private void Build()
    {
        Canvas canvas = UiFactory.Canvas(transform, "OnlineCanvas", 30);
        _root = (RectTransform)canvas.transform;
        UiFactory.Panel(_root, new Vector2(0f, -110f), new Vector2(1300f, 760f));
        UiFactory.Title(_root, "PLAY ONLINE", new Vector2(0.5f, 0.5f), new Vector2(0f, 226f), 64f, 1100f);
        _status = UiFactory.Text(_root, "Status", new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), 34f, TextAlignmentOptions.Center, new Vector2(1150f, 50f));
        _status.color = new Color(1f, 0.55f, 0.45f);
    }

    // ---------------- Steps ----------------

    private void Show(Step step)
    {
        _step = step;
        _changedFrame = Time.frameCount;
        if (_page != null)
            Destroy(_page.gameObject);
        _page = UiFactory.Stretch(_root, "Page");
        _page.pivot = new Vector2(0.5f, 0.5f);
        if (Keyboard.current != null)
            Keyboard.current.onTextInput -= OnText;
        var centre = new Vector2(0.5f, 0.5f);
        var size = new Vector2(320f, 100f);
        Button first;
        switch (step)
        {
            case Step.Choose:
                Line("Pick", 30f, "ONE PLAYER PER DEVICE. PC, PHONE AND BROWSER CAN PLAY TOGETHER.", 30f);
                first = UiFactory.Button(_page, "HOST", centre, new Vector2(-360f, -100f), size, StartHosting);
                UiFactory.Button(_page, "JOIN", centre, new Vector2(0f, -100f), size, () => Show(Step.Joining));
                UiFactory.Button(_page, "BACK", centre, new Vector2(360f, -100f), size, Back);
                break;
            case Step.Hosting:
                Line("Tell", 40f, "ROOM CODE FOR PLAYER 2:", 36f);
                _code = Line("Code", -70f, OnlineSession.Current != null && OnlineSession.Current.Code != null ? Spaced(OnlineSession.Current.Code) : "CONNECTING...", 120f);
                _code.color = UiFactory.Gold;
                first = UiFactory.Button(_page, "CANCEL", centre, new Vector2(0f, -300f), size, Back);
                break;
            case Step.Joining:
                _code = Line("Code", 40f, string.Empty, 90f);
                _code.color = UiFactory.Gold;
                first = Keypad();
                if (Keyboard.current != null)
                    Keyboard.current.onTextInput += OnText;
                RefreshTyped();
                break;
            case Step.Connecting:
                Line("Joining", -20f, "JOINING " + Spaced(_typed) + "...", 44f);
                first = UiFactory.Button(_page, "CANCEL", centre, new Vector2(0f, -250f), size, Back);
                break;
            default: // Waiting
                Line("Wait", 20f, "CONNECTED. THE HOST IS PICKING A LEVEL.", 44f);
                first = UiFactory.Button(_page, "SHOP", centre, new Vector2(-180f, -200f), size, () => ShopView.Open(transform));
                UiFactory.Button(_page, "LEAVE", centre, new Vector2(180f, -200f), size, Back);
                break;
        }
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    private TextMeshProUGUI Line(string name, float y, string text, float size)
    {
        TextMeshProUGUI line = UiFactory.Text(_page, name, new Vector2(0.5f, 0.5f), new Vector2(0f, y), size, TextAlignmentOptions.Center,
            new Vector2(1200f, size * 1.4f), outlined: true);
        line.text = text;
        return line;
    }

    // 8 x 4 letter keys, then DELETE / JOIN / BACK: works with a thumb, a gamepad and a mouse.
    private Button Keypad()
    {
        var centre = new Vector2(0.5f, 0.5f);
        Button first = null;
        for (int i = 0; i < Alphabet.Length; i++)
        {
            char c = Alphabet[i];
            var key = UiFactory.Button(_page, c.ToString(), centre, new Vector2(-490f + (i % 8) * 140f, -60f - (i / 8) * 86f),
                new Vector2(124f, 76f), () => Type(c));
            if (first == null)
                first = key;
        }
        UiFactory.Button(_page, "DELETE", centre, new Vector2(-400f, -420f), new Vector2(280f, 76f), Delete);
        UiFactory.Button(_page, "JOIN", centre, new Vector2(0f, -420f), new Vector2(280f, 76f), StartJoining);
        UiFactory.Button(_page, "BACK", centre, new Vector2(400f, -420f), new Vector2(280f, 76f), () => Show(Step.Choose));
        return first;
    }

    private void Update()
    {
        if (Time.frameCount == _changedFrame || ShopView.IsOpen)
            return;
        Keyboard kb = Keyboard.current;
        bool back = kb != null && kb.escapeKey.wasPressedThisFrame;
        foreach (Gamepad pad in Gamepad.all)
            back |= pad.buttonEast.wasPressedThisFrame;
        if (back)
        {
            if (_step == Step.Joining)
                Show(Step.Choose);
            else
                Back();
            return;
        }
        if (_step == Step.Joining && kb != null)
        {
            if (kb.backspaceKey.wasPressedThisFrame)
                Delete();
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                StartJoining();
        }
    }

    private void OnText(char c)
    {
        if (_step != Step.Joining || Alphabet.IndexOf(char.ToUpperInvariant(c)) < 0)
            return;
        Type(char.ToUpperInvariant(c));
        // Typing: Enter should join, not press whichever key is selected.
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void Type(char c)
    {
        if (_typed.Length < CodeLength)
            _typed += c;
        RefreshTyped();
    }

    private void Delete()
    {
        if (_typed.Length > 0)
            _typed = _typed.Substring(0, _typed.Length - 1);
        RefreshTyped();
    }

    private void RefreshTyped() => _code.text = Spaced(_typed.PadRight(CodeLength, '_'));

    private static string Spaced(string code) => string.Join(" ", code.ToCharArray());

    // ---------------- Session ----------------

    private void StartHosting()
    {
        _status.text = string.Empty;
        OnlineSession.Host();
        Show(Step.Hosting);
    }

    private void StartJoining()
    {
        if (_typed.Length < CodeLength)
        {
            _status.text = "THE CODE HAS 6 CHARACTERS";
            return;
        }
        _status.text = string.Empty;
        OnlineSession.Join(_typed);
        Show(Step.Connecting);
    }

    private void OnCode(string code)
    {
        if (_step == Step.Hosting && _code != null)
            _code.text = Spaced(code);
    }

    private void OnConnected()
    {
        if (OnlineSession.Current != null && OnlineSession.Current.IsHost)
        {
            Action picked = _hostConnected;
            Destroy(gameObject);
            picked?.Invoke();
            return;
        }
        Show(Step.Waiting);
    }

    private void OnEnded(string reason)
    {
        _status.text = OnlineSession.TakeEndReason() ?? reason; // shown here: the menu mustn't show it again later
        Show(_step == Step.Connecting ? Step.Joining : Step.Choose);
    }

    // One step back; leaving a room ends the session.
    private void Back()
    {
        if (_step == Step.Choose)
        {
            Destroy(gameObject);
            _back?.Invoke();
            return;
        }
        OnlineSession.Leave();
        Show(Step.Choose);
    }
}
