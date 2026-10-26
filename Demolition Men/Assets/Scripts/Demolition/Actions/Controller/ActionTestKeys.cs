using Demolition.Run;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demolition.Actions
{
    /// <summary>
    /// Editor and development builds only (added by <see cref="RunController"/>): try every action
    /// without earning it. 1 to 9 hands Player 1 that action (<see cref="ActionCatalog"/> order) with
    /// a full meter, Shift + 1 to 9 Player 2; the action button fires it as usual.
    /// </summary>
    public sealed class ActionTestKeys : MonoBehaviour
    {
        private RunController _run;

        private void Awake() => _run = GetComponent<RunController>();

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _run == null)
                return;
            for (int i = 0; i < ActionCatalog.All.Count && i < 9; i++)
            {
                if (!keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                    continue;
                int player = keyboard.shiftKey.isPressed ? 1 : 0;
                ActionUser user = player < _run.Players.Count ? _run.Players[player].Action : null;
                if (user == null || user.IsProxy || user.Busy)
                    return;
                user.Equip(ActionCatalog.All[i].Kind);
                user.Meter.Fill();
            }
        }
    }
}
