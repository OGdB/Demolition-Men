using System.Collections;
using Demolition.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// "Press to join" lobby between the main menu and the level. Any keyboard or gamepad
/// joins with A / Space and leaves with B / Esc; Start / Enter continues once someone has
/// joined. Joined devices go to <see cref="Session"/>, which the run uses to give each
/// player their own device.
/// </summary>
[RequireComponent(typeof(JoinView))]
public class JoinScreen : MonoBehaviour
{
    [Tooltip("Shown again when the last player leaves.")]
    [SerializeField] private GameObject menuPanel;
    [Tooltip("Shown once players continue.")]
    [SerializeField] private GameObject nextPanel;
    [SerializeField] private Selectable nextSelected;

    private JoinView _view;
    private bool _open;

    private void Awake()
    {
        _view = GetComponent<JoinView>();
    }

    /// <summary>Called by the menu's Play button.</summary>
    public void Open()
    {
        Session.Clear();
        _open = true;
        _view.Show(true);
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
                    StartCoroutine(Close(menuPanel, null));
                    return;
                }
                Session.Leave(device);
            }
            else if (ContinuePressed(device) && Session.PlayerCount > 0)
            {
                StartCoroutine(Close(nextPanel, nextSelected));
                return;
            }
        }
    }

    // Switch panels a frame later, so the press that closed the lobby doesn't also
    // submit the button selected next.
    private IEnumerator Close(GameObject panel, Selectable select)
    {
        _open = false;
        _view.Show(false);
        yield return null;
        if (panel != null)
            panel.SetActive(true);
        if (select != null)
            select.Select();
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
