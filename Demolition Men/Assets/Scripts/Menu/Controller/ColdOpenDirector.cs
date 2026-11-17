using Demolition;
using Demolition.Ranked;
using Demolition.Run;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays the cold open (<see cref="ColdOpen"/>, docs/hit-plan.md F4) in the Tutorial, where
/// <see cref="MainMenu"/> sends a brand-new profile instead of showing the title and the lobby.
/// It holds the run's countdown and plays in its place, at the block with the painted X
/// (<see cref="WeakPointMark"/>): the player runs in and stops beside it, the HUD asks for a hit
/// with the button of their device, the first swing that lands drops the block, the title slams
/// over the dust (<see cref="TitleView.PlayOver"/>) and GO hands over the controls where they
/// stand, the Tutorial's signs (hidden until then) carrying on from there. Any press is the hit
/// and nothing waits for one. Nobody chose a device in a lobby, so through this first run
/// whichever device is pressed plays (a phone's touchscreen on a phone) and is the player's in
/// the <see cref="Session"/> from then on. With no painted block in the scene the Tutorial
/// starts as it always did.
/// </summary>
public sealed class ColdOpenDirector : MonoBehaviour
{
    // Tuned to the Tutorial's street: the house before the painted one ends 5 units left of the
    // block, and the pit under the painted one starts at its far side.
    private const float StandOff = 1.6f; // left of the block's middle: the swing's circle covers it
    private const float RunIn = 3.4f;    // they start this far behind that spot
    private const float Near = 0.1f;
    private const float Brake = 6f;      // speed allowed per unit still to go
    private const float Creep = 1.5f;    // and never slower than this until they're there
    private const float PromptSeconds = 0.25f; // a prompt lasts this long past its last frame
    private const float TaughtAfter = 3f;      // the controls stay on the HUD until they've gone this far

    private readonly ColdOpen _open = new ColdOpen();
    private readonly ScriptedInputSource _script = new ScriptedInputSource();
    private RunController _run;
    private RunController.Player _player;
    private Rigidbody2D _body;
    private BuildingBlock _mark;
    private IPlayerInputSource _own;
    private TitleView _title;
    private float _spot;
    private bool _wasInvincible;
    private bool _handed;
    private bool _taught;
    private string _asked, _text;
    private InputStyle _askedIn;

    /// <summary>Load the Tutorial and open cold in it. Whoever plays is in the <see cref="Session"/> already.</summary>
    public static void Play(string tutorialScene)
    {
        SceneManager.sceneLoaded += Begin;
        RunController.LoadScene(tutorialScene);
    }

    // After the scene's Awakes, before its first frame.
    private static void Begin(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= Begin;
        RunController run = RunController.Current;
        BuildingBlock mark = Mark();
        ScrollCamera scroll = run != null && run.Camera != null ? run.Camera.GetComponent<ScrollCamera>() : null;
        if (mark != null && scroll != null && run.Players.Count > 0)
            run.gameObject.AddComponent<ColdOpenDirector>().Open(run, mark, scroll);
    }

    // The painted block the player reaches first.
    private static BuildingBlock Mark()
    {
        BuildingBlock first = null;
        foreach (WeakPointMark mark in FindObjectsByType<WeakPointMark>(FindObjectsSortMode.None))
            if (mark.TryGetComponent(out BuildingBlock block)
                && (first == null || block.transform.position.x < first.transform.position.x))
                first = block;
        return first;
    }

    private void Open(RunController run, BuildingBlock mark, ScrollCamera scroll)
    {
        _run = run;
        _mark = mark;
        _player = run.Players[0];
        _body = _player.Transform.GetComponent<Rigidbody2D>();
        _spot = mark.transform.position.x - StandOff;

        // The block in the middle of the screen, where the title lands; the bottom stays the scene's
        // ground framing. That leaves the stretch before it (the house to smash, the axe) in play.
        Camera view = scroll.Camera;
        scroll.Place(mark.transform.position.x - ScrollEdge.ViewWidth * 0.5f, view.transform.position.y - view.orthographicSize);
        Vector3 at = _player.Transform.position;
        _player.Transform.position = new Vector3(_spot - RunIn, at.y, at.z);

        run.Held = true;
        _wasInvincible = _player.Health.Invincible;
        _player.Health.Invincible = true; // no hand on the controls yet: the building mustn't hurt them
        _own = _player.Input.Source;
        _player.Input.Source = _script;
        _player.Motor.enabled = _player.Combat.enabled = true; // the run switched them off for its countdown
        _player.Combat.Landed += OnLanded;
    }

    private void Update()
    {
        InputDevice pressed = PauseController.BlocksInput ? null : PressedDevice();
        if (pressed != null && pressed != Session.DeviceFor(0))
            Adopt(pressed);
        float x = _player.Transform.position.x;
        if (_handed)
        {
            // The opening skipped the stretch that teaches these.
            _taught |= Mathf.Abs(x - _spot) > TaughtAfter;
            if (!_taught)
                Prompt(ColdOpen.Controls);
            return;
        }

        bool arrived = x >= _spot - Near;
        _open.Tick(Time.deltaTime, arrived, pressed != null, _mark == null);
        if (_open.Beat > ColdOpenBeat.Swing)
            Break(); // the swing missed or never came: the block drops all the same

        var frame = new PlayerInputFrame();
        switch (_open.Beat)
        {
            case ColdOpenBeat.RunIn:
                frame.Move = Vector2.right;
                break;
            case ColdOpenBeat.Ask:
                Prompt(ColdOpen.Ask);
                break;
            case ColdOpenBeat.Swing:
                frame.AttackHeld = true;
                break;
            case ColdOpenBeat.Title:
                if (_title == null)
                    (_title = gameObject.AddComponent<TitleView>()).PlayOver(_open.TitleDone);
                break;
            case ColdOpenBeat.Done:
                HandOver();
                return;
        }
        _script.Style = _own.Style;
        _script.Next = frame;
    }

    // The motor coasts for units once the stick is let go, and the pit is two units past the spot:
    // they ease in to it and stand there. Every physics step, so a slow frame can't carry them on.
    private void FixedUpdate()
    {
        if (_handed)
            return;
        float left = _spot - _body.position.x;
        float cap = left <= Near ? 0f : Mathf.Max(Creep, left * Brake);
        Vector2 velocity = _body.linearVelocity;
        if (velocity.x > cap)
            _body.linearVelocity = new Vector2(cap, velocity.y);
    }

    // The first swing that lands drops the painted block whatever its health: one hit, as the paint promises.
    private void OnLanded(bool weapon)
    {
        if (_open.Beat == ColdOpenBeat.Swing)
            Break();
    }

    private void Break()
    {
        if (_mark != null)
            _mark.Shatter(_player.Index, HitCause.Fist);
    }

    private void HandOver()
    {
        _handed = true;
        _player.Combat.Landed -= OnLanded;
        _player.Health.Invincible = _wasInvincible;
        _own.Read(); // what was pressed during the opening isn't played back at GO
        _player.Input.Source = _own;
        _run.Held = false;
        _run.State.Go();
    }

    // A line on the HUD with the buttons of the device in hand, for as long as it is asked for.
    private void Prompt(string template)
    {
        InputStyle style = _player.Input.Style;
        if (template != _asked || style != _askedIn)
        {
            _asked = template;
            _askedIn = style;
            _text = InputGlyphs.Format(template, new[] { style });
        }
        _run.ShowPrompt(_text, PromptSeconds);
    }

    private void Adopt(InputDevice device)
    {
        Session.Clear();
        Session.Join(device);
        _player.Input.AssignDevice(device);
        if (_handed)
            return;
        _own = _player.Input.Source;
        _player.Input.Source = _script;
    }

    // A device a player could be holding was pressed this frame. On a phone that is its touchscreen
    // (the lobby's rule); elsewhere a pad's button or stick, a key or a click.
    private static InputDevice PressedDevice()
    {
        if (Platform.IsTouch)
        {
            Touchscreen touch = Touchscreen.current;
            return touch != null && touch.primaryTouch.press.wasPressedThisFrame ? touch : null;
        }
        foreach (Gamepad pad in Gamepad.all)
            if (pad.wasUpdatedThisFrame && (pad.leftStick.ReadValue().sqrMagnitude > 0.25f || DeviceInputSource.AnyButton(pad)))
                return pad;
        Keyboard keys = Keyboard.current;
        Mouse mouse = Mouse.current;
        bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
        return keys != null && (click || keys.anyKey.wasPressedThisFrame) ? keys : null;
    }
}
