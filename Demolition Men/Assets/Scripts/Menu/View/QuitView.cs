using System;
using Demolition;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>"Quit game?" over the menu, the only way out (there is no quit button): YES
/// quits, NO or Esc / B goes back. Built in code; NO is selected so a stray press stays.</summary>
public class QuitView : MonoBehaviour
{
    private Action _quit;
    private Action _unblock;
    private GameObject _previousSelection;

    public static bool IsOpen { get; private set; }

    public static void Open(Action quit)
    {
        if (IsOpen)
            return;
        UiFactory.EnsureEventSystem();
        new GameObject("Quit").AddComponent<QuitView>()._quit = quit;
    }

    private void Awake()
    {
        IsOpen = true;
        _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        Build();
    }

    private void OnDestroy()
    {
        IsOpen = false;
        _unblock?.Invoke();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_previousSelection);
    }

    private void Update()
    {
        bool back = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        foreach (Gamepad pad in Gamepad.all)
            back |= pad.buttonEast.wasPressedThisFrame;
        if (back)
            Close();
    }

    private void Close() => Destroy(gameObject);

    private void Build()
    {
        Canvas canvas = UiFactory.Canvas(transform, "QuitCanvas", 60);
        _unblock = UiFactory.Modal(canvas);
        var root = (RectTransform)canvas.transform;
        UiFactory.Backdrop(root, new Color(0f, 0f, 0f, 0.6f));
        var centre = new Vector2(0.5f, 0.5f);

        UiFactory.Panel(root, Vector2.zero, new Vector2(860f, 420f));
        UiFactory.Title(root, "QUIT GAME?", centre, new Vector2(0f, 166f), 72f, 800f);
        UiFactory.Text(root, "Question", centre, new Vector2(0f, 40f), 36f, TMPro.TextAlignmentOptions.Center, new Vector2(760f, 50f))
            .text = "THE SITE WILL STILL BE HERE.";

        var size = new Vector2(280f, 96f);
        Button no = UiFactory.Button(root, "NO", centre, new Vector2(-170f, -100f), size, Close);
        UiFactory.Button(root, "YES", centre, new Vector2(170f, -100f), size, () => _quit?.Invoke());
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(no.gameObject);
    }
}
