using System.Collections.Generic;
using Demolition.Meta;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demolition.Run
{
    /// <summary>
    /// The run in the players' hands (<see cref="Haptics"/>, docs/hit-plan.md §5.1 F5): each
    /// player's own gamepad rumbles, or the phone's motor for the player on its touchscreen,
    /// for their hits, the damage they take, blasts near them and collapses they set off or
    /// stand near. Only for players on this device who are in play (online each device
    /// buzzes for what its own screen shows), never for a bot, and only while the run is
    /// being played: everything stops when it ends, on pause, when the app loses focus, when
    /// the setting goes off and when this object does. Added by <see cref="RunController"/>.
    /// </summary>
    public sealed class RunHaptics : MonoBehaviour
    {
        private sealed class Hands
        {
            public RunController.Player Player;
            public readonly HapticChannel Channel = new HapticChannel();
            public Gamepad Buzzing; // the pad whose motors this player's pulse runs
            public System.Action<bool> Landed;
            public System.Action<float, string> Hurt;
        }

        private readonly List<Hands> _hands = new List<Hands>();
        private RunController _run;
        private bool _wasLive;
        // From Unity's messages, not Application.isFocused: a browser game in a portal's frame
        // may never be told it has the focus, and must buzz all the same.
        private bool _away;

        /// <summary>The Input System drives a pad's motors here: on Windows, macOS, Android and
        /// the consoles, not in a browser or on Linux (the package's gamepad-haptics page).</summary>
        public static bool PadsRumble => Application.platform != RuntimePlatform.WebGLPlayer
            && Application.platform != RuntimePlatform.LinuxPlayer && Application.platform != RuntimePlatform.LinuxEditor;

        private bool Live => Profile.Data.settings.vibration && !_away
            && _run.Live && !PauseController.MenuOpen;

        private void Awake()
        {
            _run = GetComponent<RunController>();
            foreach (RunController.Player player in _run.Players)
            {
                var hands = new Hands { Player = player };
                hands.Landed = weapon => Play(hands, Haptics.Hit(weapon));
                hands.Hurt = (damage, source) => Play(hands, Haptics.Hurt(damage));
                _hands.Add(hands);
            }
        }

        private void OnEnable()
        {
            DestructionEvents.Collapsed += OnCollapsed;
            Dynamite.Exploded += OnExploded;
            foreach (Hands hands in _hands)
            {
                hands.Player.Combat.Landed += hands.Landed;
                hands.Player.Health.Hurt += hands.Hurt;
            }
        }

        private void OnDisable()
        {
            DestructionEvents.Collapsed -= OnCollapsed;
            Dynamite.Exploded -= OnExploded;
            foreach (Hands hands in _hands)
            {
                if (hands.Player.Combat != null)
                    hands.Player.Combat.Landed -= hands.Landed;
                if (hands.Player.Health != null)
                    hands.Player.Health.Hurt -= hands.Hurt;
            }
            Stop();
        }

        private void OnApplicationFocus(bool focused) => Away(!focused);

        private void OnApplicationPause(bool paused) => Away(paused);

        private void Away(bool away)
        {
            _away = away;
            if (away)
                Stop();
        }

        private void Update()
        {
            bool live = Live;
            if (_wasLive && !live)
                Stop();
            _wasLive = live;

            foreach (Hands hands in _hands)
                if (hands.Buzzing != null && hands.Channel.StrengthAt(Time.unscaledTime) <= 0f)
                    Rest(hands);
        }

        private void OnCollapsed(CollapseInfo info)
        {
            foreach (Hands hands in _hands)
                Play(hands, Haptics.Collapse(info.Cells.Count, info.Instigator == hands.Player.Index, Distance(hands, info.Position)));
        }

        private void OnExploded(Vector3 position)
        {
            foreach (Hands hands in _hands)
                Play(hands, Haptics.Blast(Distance(hands, position)));
        }

        private static float Distance(Hands hands, Vector3 position) =>
            Vector2.Distance(hands.Player.Transform.position, position);

        private void Play(Hands hands, Pulse pulse)
        {
            RunController.Player player = hands.Player;
            if (!Live || player.Health.IsProxy || player.Health.IsDead || player.Transform.TryGetComponent(out BotPilot _))
                return;
            // Online a device has one player, whatever their number in the run. A keyboard has no motor.
            InputDevice device = Session.DeviceFor(NetRole.IsOnline ? 0 : player.Index);
            if (!(device is Gamepad) && !(device is Touchscreen))
                return;
            if (!hands.Channel.Play(pulse, Time.unscaledTime))
                return;
            if (device is Gamepad pad)
            {
                pad.SetMotorSpeeds(pulse.Heavy, pulse.Strength);
                hands.Buzzing = pad;
            }
            else
                PhoneMotor.Play(pulse);
        }

        private static void Rest(Hands hands)
        {
            if (hands.Buzzing.added)
                hands.Buzzing.ResetHaptics();
            hands.Buzzing = null;
        }

        // Every pad, not only the ones a pulse is known to run on: one left buzzing is the bug to avoid.
        private void Stop()
        {
            foreach (Hands hands in _hands)
            {
                hands.Channel.Stop();
                hands.Buzzing = null;
            }
            InputSystem.ResetHaptics();
            PhoneMotor.Stop();
        }
    }
}
