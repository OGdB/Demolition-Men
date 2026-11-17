using System;
using Demolition;
using Demolition.Levels;
using Demolition.Meta;
using Demolition.Online;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Online co-op from the lobby (docs/multiplayer.md §6), one local player per device.
/// HOST opens a room on the relay and shows its code, big enough to read on a TV (COPY, or
/// clicking the code, puts it on the clipboard to send to a friend; SHARE, once the game has
/// a play URL, sends the room's <see cref="InviteLink"/>: Android's share sheet, the clipboard
/// elsewhere); JOIN
/// takes a code from an on-screen keypad (touch, gamepad) or the keyboard. Connected, the
/// host picks a level (the level select) and the client waits here, with the shop.
/// Esc / B goes back a step; leaving a room ends the session. A browser build opened with an
/// invite link starts here, joining (<see cref="OpenInvite"/>).
/// </summary>
public class OnlineView : MonoBehaviour
{
    private const string Alphabet = RoomCode.Alphabet;
    private const int CodeLength = RoomCode.Length;

    private enum Step { Choose, Hosting, Joining, Connecting, Waiting }

    private Action _back;
    private Action _hostConnected;
    private Step _step;
    private bool _invited; // opened by an invite link, and its room not joined yet
    private bool _inviteCounted;
    private string _typed = string.Empty;
    private int _changedFrame;
    private RectTransform _root;
    private RectTransform _page;
    private TextMeshProUGUI _status;
    private TextMeshProUGUI _code;
    private TextMeshProUGUI _copyLabel, _shareLabel, _shareHint;

    public static bool IsOpen { get; private set; }

    /// <param name="back">Closed without a session.</param>
    /// <param name="hostConnected">As host, the other player joined: pick a level.</param>
    public static void Open(Action back, Action hostConnected) => Create(back, hostConnected, Step.Choose);

    /// <summary>Client back in the menu between runs: wait for the host's choice.</summary>
    public static void OpenWaiting(Action back) => Create(back, null, Step.Waiting);

    /// <summary>The browser build was opened with an invite link: join its room. In, the guest
    /// is where one who typed the code would be; a room that can't be joined closes this for
    /// <paramref name="back"/> (the lobby) and says why.</summary>
    public static void OpenInvite(string code, Action back)
    {
        OnlineView view = Create(back, null, Step.Connecting, code);
        view._invited = true;
        OnlineSession.Join(code);
    }

    private static OnlineView Create(Action back, Action hostConnected, Step step, string typed = "")
    {
        UiFactory.EnsureEventSystem();
        var view = new GameObject("Online").AddComponent<OnlineView>();
        view._back = back;
        view._hostConnected = hostConnected;
        view._typed = typed;
        view.Build();
        view.Show(step);
        return view;
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
        UiFactory.TitledPanel(_root, new Vector2(0f, -110f), new Vector2(1300f, 760f), "PLAY ONLINE", 64f, 1100f);
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
                Line("Pick", -60f, "ONE PLAYER PER DEVICE. PC, PHONE AND BROWSER CAN PLAY TOGETHER.", 30f);
                first = UiFactory.Button(_page, "HOST", centre, new Vector2(-360f, -200f), size, StartHosting);
                UiFactory.Button(_page, "JOIN", centre, new Vector2(0f, -200f), size, () => Show(Step.Joining));
                UiFactory.Button(_page, "BACK", centre, new Vector2(360f, -200f), size, Back);
                break;
            case Step.Hosting:
                Line("Tell", 40f, "ROOM CODE FOR PLAYER 2:", 36f);
                _code = Line("Code", -70f, OnlineSession.Current != null && OnlineSession.Current.Code != null ? Spaced(OnlineSession.Current.Code) : "CONNECTING...", 120f);
                _code.color = UiFactory.Gold;
                _code.raycastTarget = true;
                Button tap = _code.gameObject.AddComponent<Button>(); // the code itself copies too
                tap.transition = Selectable.Transition.None;
                tap.navigation = new Navigation { mode = Navigation.Mode.None }; // gamepads have COPY
                // With somewhere to send a friend (GameConfig.playUrl), SHARE sits between the two.
                bool share = InviteLink.Page(PlayUrl, OnlineSession.Version) != null;
                first = UiFactory.Button(_page, "COPY", centre, new Vector2(share ? -360f : -180f, -300f), size, CopyCode);
                _copyLabel = first.GetComponentInChildren<TextMeshProUGUI>();
                tap.onClick.AddListener(CopyCode);
                _shareLabel = null;
                _inviteCounted = false;
                if (share)
                {
                    _shareHint = Line("Link", -190f, "SHARE SENDS A LINK. PLAYER 2 OPENS IT AND PLAYS IN A BROWSER.", 30f);
                    _shareLabel = UiFactory.Button(_page, "SHARE", centre, new Vector2(0f, -300f), size, ShareLink).GetComponentInChildren<TextMeshProUGUI>();
                }
                UiFactory.Button(_page, "CANCEL", centre, new Vector2(share ? 360f : 180f, -300f), size, Back);
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

    private static string PlayUrl => GameConfig.Instance != null ? GameConfig.Instance.playUrl : null;

    private void CopyCode()
    {
        string code = OnlineSession.Current != null ? OnlineSession.Current.Code : null;
        if (string.IsNullOrEmpty(code))
            return; // still connecting
        Platform.Copy(code);
        Copied(_copyLabel);
    }

    // The room's invite link: to the share sheet where there is one, else to the clipboard.
    private void ShareLink()
    {
        string link = InviteLink.Build(PlayUrl, OnlineSession.Version, OnlineSession.Current != null ? OnlineSession.Current.Code : null);
        if (link == null)
            return; // still connecting
        if (!Platform.Share(link))
        {
            Platform.Copy(link);
            Copied(_shareLabel);
        }
        // A game in the background (a phone in the chat app, a hidden browser tab) answers no joiner.
        _shareHint.text = "SENT? WAIT HERE: PLAYER 2 GETS IN WHILE THE GAME IS OPEN.";
        if (!_inviteCounted) // a room counts once, however many chats its link goes to
            CounterSender.Invited();
        _inviteCounted = true;
    }

    // One clipboard: only the button pressed last says COPIED.
    private void Copied(TextMeshProUGUI label)
    {
        _copyLabel.text = "COPY";
        if (_shareLabel != null)
            _shareLabel.text = "SHARE";
        label.text = "COPIED";
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
        _invited = false; // in: from here on a guest like any other
        Show(Step.Waiting);
    }

    private void OnEnded(string reason)
    {
        reason = OnlineSession.TakeEndReason() ?? reason; // shown here: the menu mustn't show it again later
        if (_invited)
        {
            // The link's room couldn't be joined (a stale code, a full room, another version, no relay):
            // a friend with a link has no code to type, so the lobby, where the game still plays.
            Destroy(gameObject);
            _back?.Invoke();
            NoticeView.Show("COULDN'T JOIN", reason);
            return;
        }
        _status.text = reason;
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
        _invited = false;
        OnlineSession.Leave();
        Show(Step.Choose);
    }
}
