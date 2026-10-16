using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Demolition.Run
{
    /// <summary>Choices made in the menus that outlive scene loads: who plays, on which device.</summary>
    public static class Session
    {
        public const int MaxPlayers = 2;

        private static readonly List<InputDevice> _devices = new List<InputDevice>();

        /// <summary>Joined devices in player order (P1 first).</summary>
        public static IReadOnlyList<InputDevice> Devices => _devices;

        /// <summary>Players taking part. 0 = nobody joined (play mode started straight in a
        /// level): every player in the scene plays with the default input.</summary>
        public static int PlayerCount => _devices.Count;

        public static InputDevice DeviceFor(int player) =>
            player >= 0 && player < _devices.Count ? _devices[player] : null;

        /// <returns>False if the device already joined or the lobby is full.</returns>
        public static bool Join(InputDevice device)
        {
            if (device == null || _devices.Count >= MaxPlayers || _devices.Contains(device))
                return false;
            _devices.Add(device);
            return true;
        }

        /// <summary>Remove a device; later players move up a slot.</summary>
        public static void Leave(InputDevice device) => _devices.Remove(device);

        public static void Clear() => _devices.Clear();
    }
}
