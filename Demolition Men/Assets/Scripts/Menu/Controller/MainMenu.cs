using Demolition;
using Demolition.Levels;
using Demolition.Meta;
using Demolition.Online;
using Demolition.Run;
using UnityEngine;

/// <summary>
/// The title screen: the game's name slams into the middle of the screen and rises to the
/// top (<see cref="TitleView"/>), then the join lobby opens by itself. Back from a run it
/// goes straight to the level select. There is no quit button: Esc (B on a gamepad, back
/// on Android) with nobody joined asks whether to quit.
/// </summary>
[RequireComponent(typeof(JoinScreen))]
public class MainMenu : MonoBehaviour
{
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
        _title.PlayIntro(OpenLobby);
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
