using System;
using UnityEngine;

namespace Demolition
{
    public enum NetMode { Offline, Host, Client }

    /// <summary>
    /// This device's part in an online run (docs/multiplayer.md §2, §5): offline (the game as
    /// it always was), the host that decides what both screens share, or a client. The core
    /// assembly never references netcode; the hooks below are what <c>Demolition.Online</c>
    /// fills in. Offline nothing calls them.
    /// </summary>
    public static class NetRole
    {
        public static NetMode Mode { get; set; } = NetMode.Offline;
        public static bool IsOnline => Mode != NetMode.Offline;
        public static bool IsHost => Mode == NetMode.Host;
        public static bool IsClient => Mode == NetMode.Client;

        /// <summary>Client: its player hit a standing block for this much; send a DamageRequest
        /// instead of applying it (the instigator is the sender's connection).</summary>
        public static Action<BuildingBlock, float> RequestDamage;

        /// <summary>Host: a standing block's health changed (0 = destroyed); add it to this
        /// tick's CellHealth batch. Read the block during the call: a destroyed one is gone by
        /// the next frame.</summary>
        public static Action<BuildingBlock> BlockHealthChanged;

        /// <summary>Client: its player touched a pickup; send a Claim. The host answers Taken.</summary>
        public static Action<Pickup> ClaimPickup;

        /// <summary>Online: spawn the run's players at the spawn point (the host network-spawns
        /// both, a client none) and return how many there are.</summary>
        public static Func<Vector3, int> SpawnPlayers;
    }
}
