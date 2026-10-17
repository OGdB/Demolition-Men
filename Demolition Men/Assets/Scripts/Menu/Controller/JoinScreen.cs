using System.Collections;
using Demolition;
using Demolition.Levels;
using Demolition.Meta;
using Demolition.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// "Press to join" lobby between the main menu and the level select. Any keyboard or
/// gamepad joins with A / Space and leaves with B / Esc; Start / Enter continues once
/// someone has joined. On a phone the touchscreen joins as player 1 straight away and the
/// on-screen START / BACK buttons drive the lobby (a gamepad can still join as player 2).
/// Joined devices go to <see cref="Session"/>, which the run uses to give each player
/// their own device.
/// </summary>
[RequireComponent(typeof(JoinView))]
public class JoinScreen : MonoBehaviour
{
    [Tooltip("Shown again when the lobby or the level select is closed.")]
    [SerializeField] private GameObject menuPanel;

    private JoinView _view;
    private bool _open;

    private void Awake()
    {
        _view = GetComponent<JoinView>();
    }

    // Back from a run: straight to the level select with the same players.
    private void Start()
    {
        if (LevelSelection.ReturnToLevelSelect && Session.PlayerCount > 0)
        {
            menuPanel.SetActive(false);
            LevelSelectView.Open(BackToMenu);
        }
    }

    /// <summary>Called by the menu's Play button.</summary>
    public void Open()
    {
        Session.Clear();
        if (Platform.IsTouch && Touchscreen.current != null)
            Session.Join(Touchscreen.current);
        _open = true;
        _view.Show(true, Continue, Back);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void Update()
    {
        if (!_open)
            return;

        foreach (InputDevice device in InputSystem.devices)
        {
            if (JoinPressed(device))
            {
                Session.Join(device);
            }
            else if (LeavePressed(device))
            {
                if (Session.PlayerCount == 0)
                {
                    Back();
                    return;
                }
                Session.Leave(device);
            }
            else if (ContinuePressed(device) && Session.PlayerCount > 0)
            {
                Continue();
                return;
            }
        }
    }

    private void Continue()
    {
        if (_open && Session.PlayerCount > 0)
            StartCoroutine(Close(() => LevelSelectView.Open(BackToMenu)));
    }

    private void Back()
    {
        if (_open)
            StartCoroutine(Close(BackToMenu));
    }

    private void BackToMenu()
    {
        Session.Clear();
        menuPanel.SetActive(true);
    }

    // Switch panels a frame later, so the press that closed the lobby doesn't also
    // press whatever is selected next.
    private IEnumerator Close(System.Action then)
    {
        _open = false;
        _view.Show(false, null, null);
        yield return null;
        then();
    }

    private static bool JoinPressed(InputDevice device)
    {
        switch (device)
        {
            case Gamepad pad: return pad.buttonSouth.wasPressedThisFrame;
            case Keyboard kb: return kb.spaceKey.wasPressedThisFrame;
            default: return false;
        }
    }

    private static bool LeavePressed(InputDevice device)
    {
        switch (device)
        {
            case Gamepad pad: return pad.buttonEast.wasPressedThisFrame;
            case Keyboard kb: return kb.escapeKey.wasPressedThisFrame;
            default: return false;
        }
    }

    private static bool ContinuePressed(InputDevice device)
    {
        switch (device)
        {
            case Gamepad pad: return pad.startButton.wasPressedThisFrame;
            case Keyboard kb: return kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            default: return false;
        }
    }
}
