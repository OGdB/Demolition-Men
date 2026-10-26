using System;
using Demolition.Levels;
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

        /// <summary>Index of this device's player online (host 0, client 1); -1 offline.</summary>
        public static int LocalPlayer => Mode == NetMode.Host ? 0 : Mode == NetMode.Client ? 1 : -1;

        /// <summary>Host: load a scene on both devices. True if it took the load over.</summary>
        public static Func<string, bool> LoadScene;

        /// <summary>Client: leave the session (MENU on the results screen).</summary>
        public static Action Leave;

        /// <summary>Both: the Level scene generated its level (hash check, then the run's sync starts).</summary>
        public static Action<LevelLayout> LevelBuilt;

        /// <summary>Host: a player collected a pickup here; tell the client it's gone.</summary>
        public static Action<Pickup, int> PickupTaken;

        /// <summary>This device's player threw a stick (with this force); the other device flies a copy.</summary>
        public static Action<Dynamite, Vector2> DynamiteThrown;

        /// <summary>This device's stick went off.</summary>
        public static Action<Dynamite> DynamiteExploded;

        /// <summary>Client: its player hit a standing block for this much; send a DamageRequest
        /// instead of applying it (the instigator is the sender's connection).</summary>
        public static Action<BuildingBlock, float> RequestDamage;

        /// <summary>Host: a standing block's health changed (0 = destroyed); add it to this
        /// tick's CellHealth batch. Read the block during the call: a destroyed one is gone by
        /// the next frame.</summary>
        public static Action<BuildingBlock> BlockHealthChanged;

        /// <summary>Client: its player touched a pickup; send a Claim. The host answers Taken.</summary>
        public static Action<Pickup> ClaimPickup;

        /// <summary>This device's player's action (<c>ActionUser</c>): phase (announce / started / struck),
        /// player index, action kind, where it struck and which way the player faced. The other device
        /// plays it on its copy.</summary>
        public static Action<int, int, int, Vector2, int> ActionEvent;

        /// <summary>Online: spawn the run's players at the spawn point (the host network-spawns
        /// both, a client none) and return how many there are.</summary>
        public static Func<Vector3, int> SpawnPlayers;
    }
}
