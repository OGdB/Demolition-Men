using System;
using System.Collections;
using Demolition;
using Demolition.Levels;
using Demolition.Meta;
using Demolition.Online;
using Demolition.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// "Press to join" lobby under the title, before the level select (the training yard on a
/// new profile's first play). Any keyboard or gamepad joins with A / Space and leaves with
/// B / Esc; Start / Enter continues once someone has joined. B / Esc from a device that
/// hasn't joined is "back", which the menu turns into the quit question. On a phone the touchscreen joins as
/// player 1 straight away and the on-screen START button continues (a gamepad can still
/// join as player 2). Joined devices go to <see cref="Session"/>, which the run uses to
/// give each player their own device.
/// </summary>
[RequireComponent(typeof(JoinView))]
public class JoinScreen : MonoBehaviour
{
    private JoinView _view;
    private Action _back;
    private bool _open;
    private int _openedFrame;

    private void Awake()
    {
        _view = GetComponent<JoinView>();
    }

    /// <summary>Show the lobby; players already joined stay joined (back from the level select).</summary>
    /// <param name="back">B / Esc with nobody joined.</param>
    public void Open(Action back)
    {
        _back = back;
        if (Platform.IsTouch && Touchscreen.current != null)
            Session.Join(Touchscreen.current);
        _open = true;
        _openedFrame = Time.frameCount;
        _view.Show(true, Continue, GoOnline);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void Update()
    {
        // Not on the frame it opened: the press that skipped the title or closed the level
        // select must not also join or leave someone.
        if (!_open || Time.frameCount == _openedFrame || QuitView.IsOpen)
            return;

        foreach (InputDevice device in InputSystem.devices)
        {
            if (JoinPressed(device))
            {
                Session.Join(device);
            }
            else if (LeavePressed(device))
            {
                // A joined device leaves; any other (nobody joined, a phone's touch-joined
                // player, Android back) asks to quit.
                if (!Session.IsJoined(device))
                {
                    _back?.Invoke();
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

    // A brand-new profile goes straight to the training yard; everyone else picks a job.
    private void Continue()
    {
        if (!_open || Session.PlayerCount == 0)
            return;
        if (Profile.Data.runsPlayed == 0 && GameConfig.Instance != null)
        {
            _open = false;
            LevelSelection.Clear();
            RunController.LoadScene(GameConfig.Instance.tutorialScene);
            return;
        }
        StartCoroutine(Close(() => LevelSelectView.Open(() => Open(_back))));
    }

    private void GoOnline()
    {
        if (_open && Session.PlayerCount == 1)
            StartCoroutine(Close(() => OnlineView.Open(() => Open(_back),
                () => LevelSelectView.Open(() => { OnlineSession.Leave(); Open(_back); }))));
    }

    // Switch panels a frame later, so the press that closed the lobby doesn't also
    // press whatever is selected next.
    private IEnumerator Close(Action then)
    {
        _open = false;
        _view.Show(false, null);
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
