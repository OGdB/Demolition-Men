using Demolition;
using Demolition.Levels;
using Demolition.Meta;
using Demolition.Online;
using Demolition.Run;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The title screen: the game's name slams into the middle of the screen and rises to the
/// top (<see cref="TitleView"/>), then the join lobby opens by itself. Back from a run it
/// goes straight to the level select. There is no quit button: Esc (B on a gamepad, back
/// on Android) with nobody joined asks whether to quit. A browser build opened with an invite
/// link (<see cref="InviteLink"/>) skips the title and the lobby and joins the link's room.
/// A brand-new profile skips them too and opens in the Tutorial (<see cref="ColdOpen"/>,
/// <see cref="ColdOpenDirector"/>; <c>GameConfig.coldOpen</c> turns that off).
/// </summary>
[RequireComponent(typeof(JoinScreen))]
public class MainMenu : MonoBehaviour
{
    private static bool _inviteRead;

    private JoinScreen _join;
    private TitleView _title;

    private void Awake()
    {
        _join = GetComponent<JoinScreen>();
        _title = gameObject.AddComponent<TitleView>();
    }

    private void Start()
    {
        // Online between runs: the host picks the next level, the client waits for it.
        if (NetRole.IsOnline)
        {
            _title.ShowRisen();
            if (NetRole.IsHost)
                LevelSelectView.Open(LeaveOnline);
            else
                OnlineView.OpenWaiting(LeaveOnline);
            return;
        }
        if (LevelSelection.ReturnToLevelSelect && Session.PlayerCount > 0)
        {
            _title.ShowRisen();
            LevelSelectView.Open(OpenLobby);
            ShowEndedSession();
            return;
        }
        Session.Clear();
        string invite = TakeInvite();
        if (invite != null && Session.Join(DefaultDevice()))
        {
            _title.ShowRisen();
            OnlineView.OpenInvite(invite, OpenLobby);
            return;
        }
        GameConfig config = GameConfig.Instance;
        if (config != null && ColdOpen.Starts(Profile.Data, config.tutorialScene, config.coldOpen, invite != null,
                LevelSelection.ReturnToLevelSelect) && Session.Join(DefaultDevice()))
        {
            LevelSelection.Clear();
            ColdOpenDirector.Play(config.tutorialScene);
            return;
        }
        _title.PlayIntro(OpenLobby);
    }

    // The room in the address this build was opened with, the first time the menu loads only:
    // leaving that room, or coming back here after it, must not join it again.
    // shortcut: the address keeps its ?r=, so reloading the page tries the room again; strip it with history.replaceState if that bothers.
    private static string TakeInvite()
    {
        if (_inviteRead)
            return null;
        _inviteRead = true;
        return InviteLink.Code(Application.absoluteURL);
    }

    // Who plays when no lobby asked (a guest by link, the cold open): the touchscreen on a phone, else the
    // keyboard. The cold open then follows whichever device is pressed (ColdOpenDirector).
    private static InputDevice DefaultDevice()
    {
        InputDevice device = Platform.IsTouch ? Touchscreen.current : null;
        device ??= Keyboard.current;
        return device ?? Gamepad.current;
    }

    private void OpenLobby() => _join.Open(AskQuit);

    private void LeaveOnline()
    {
        OnlineSession.Leave();
        OpenLobby();
    }

    // An online session that ended mid-run brought this device back here: say why.
    private static void ShowEndedSession()
    {
        string reason = OnlineSession.TakeEndReason();
        if (reason != null)
            NoticeView.Show("ONLINE GAME OVER", reason);
    }

    private void OnEnable() => OnlineSession.Ended += OnSessionEnded;

    private void OnDisable() => OnlineSession.Ended -= OnSessionEnded;

    // The host in the level select when the client leaves: tell it (the client's own view shows it).
    private void OnSessionEnded(string reason)
    {
        if (!OnlineView.IsOpen)
            NoticeView.Show("ONLINE GAME OVER", OnlineSession.TakeEndReason() ?? reason);
    }

    private static void AskQuit() => QuitView.Open(Quit);

    private static void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
