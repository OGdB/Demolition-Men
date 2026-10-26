using UnityEngine;

namespace Demolition.Actions
{
    /// <summary>What a message between the devices says about a player's action (<c>DM.Act</c>).</summary>
    public enum ActionPhase : byte
    {
        /// <summary>Owner → other: the action this player carries.</summary>
        Kind,
        /// <summary>Host → client: a player's meter (<see cref="ActionCall.Charge"/>); the host keeps both.</summary>
        Meter,
        /// <summary>Client → host: may my player start? Host → client: the host's player started.</summary>
        Start,
        /// <summary>Host → client: no, your player may not (its meter isn't full on the host).</summary>
        Deny,
        /// <summary>Client → host: my player reached its strike frame here.</summary>
        Strike,
        /// <summary>Host → client: the strike as the host decided it, with what it picked (<see cref="ActionCall.Spots"/>).</summary>
        Struck,
    }

    /// <summary>
    /// One action message. Online the host decides everything the players share: the meters,
    /// whether an action may start, and what a strike picks in the world (the charges' blocks,
    /// where the radio stands, where the pallet lands). The owner of a player decides only its
    /// own player (where it stands when it strikes). So the client sends <see cref="ActionPhase.Kind"/>,
    /// <see cref="ActionPhase.Start"/> and <see cref="ActionPhase.Strike"/>, the host everything else.
    /// </summary>
    public struct ActionCall
    {
        /// <summary>Most spots a strike picks (the demolition's charges).</summary>
        public const int MaxSpots = 3;

        public ActionPhase Phase;
        public int Player;
        public ActionKind Kind;
        public Vector2 At;
        public int Facing;
        public float Charge;
        public Vector2[] Spots;

        /// <summary>Whether a device in this role may send this phase.</summary>
        public static bool Sendable(ActionPhase phase, bool fromHost) =>
            fromHost ? phase != ActionPhase.Strike : phase == ActionPhase.Kind || phase == ActionPhase.Start || phase == ActionPhase.Strike;
    }
}
