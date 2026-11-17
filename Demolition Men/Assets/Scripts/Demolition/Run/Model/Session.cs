using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Demolition.Run
{
    /// <summary>Choices made in the menus that outlive scene loads: who plays, on which device.
    /// Two players can share one keyboard, a half each (<see cref="KeyLayout"/>).</summary>
    public static class Session
    {
        public const int MaxPlayers = 2;

        private static readonly List<InputDevice> _devices = new List<InputDevice>();
        // Per player: the half of the keyboard they joined with (Whole for any other device).
        private static readonly List<KeyHalf> _halves = new List<KeyHalf>();

        /// <summary>Joined devices in player order (P1 first); a shared keyboard is in it twice.</summary>
        public static IReadOnlyList<InputDevice> Devices => _devices;

        /// <summary>Players taking part. 0 = nobody joined (play mode started straight in a
        /// level): every player in the scene plays with the default input.</summary>
        public static int PlayerCount => _devices.Count;

        public static InputDevice DeviceFor(int player) =>
            player >= 0 && player < _devices.Count ? _devices[player] : null;

        /// <summary>The keys <paramref name="player"/> plays with: the half they joined with
        /// while another player is on the same keyboard, else all of it.</summary>
        public static KeyHalf KeysFor(int player)
        {
            InputDevice device = DeviceFor(player);
            for (int i = 0; i < _devices.Count; i++)
                if (i != player && _devices[i] == device)
                    return _halves[player];
            return KeyHalf.Whole;
        }

        /// <param name="half">A keyboard joins a half at a time (Whole counts as Left), so a
        /// second player can join on the other; ignored for other devices.</param>
        /// <returns>False if the device (that half of a keyboard) already joined or the lobby is full.</returns>
        public static bool Join(InputDevice device, KeyHalf half = KeyHalf.Whole)
        {
            if (device == null || _devices.Count >= MaxPlayers)
                return false;
            half = !(device is Keyboard) ? KeyHalf.Whole : half == KeyHalf.Right ? KeyHalf.Right : KeyHalf.Left;
            if (IsJoined(device, half))
                return false;
            _devices.Add(device);
            _halves.Add(half);
            return true;
        }

        /// <summary>Remove a device (of two players on one keyboard, the one who joined last);
        /// later players move up a slot.</summary>
        public static void Leave(InputDevice device)
        {
            int player = _devices.LastIndexOf(device);
            if (player < 0)
                return;
            _devices.RemoveAt(player);
            _halves.RemoveAt(player);
        }

        public static bool IsJoined(InputDevice device) => _devices.Contains(device);

        /// <summary>A player joined with this half of the keyboard.</summary>
        public static bool IsJoined(InputDevice device, KeyHalf half)
        {
            for (int i = 0; i < _devices.Count; i++)
                if (_devices[i] == device && _halves[i] == half)
                    return true;
            return false;
        }

        public static void Clear()
        {
            _devices.Clear();
            _halves.Clear();
        }
    }
}
