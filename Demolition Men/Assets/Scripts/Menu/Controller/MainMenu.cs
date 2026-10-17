using Demolition.Levels;
using Demolition.Meta;
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
        if (LevelSelection.ReturnToLevelSelect && Session.PlayerCount > 0)
        {
            _title.ShowRisen();
            LevelSelectView.Open(OpenLobby);
            return;
        }
        Session.Clear();
        _title.PlayIntro(OpenLobby);
    }

    private void OpenLobby() => _join.Open(AskQuit);

    private static void AskQuit() => QuitView.Open(Quit);

    private static void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
