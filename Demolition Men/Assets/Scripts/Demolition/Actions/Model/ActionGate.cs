using UnityEngine;

namespace Demolition.Actions
{
    /// <summary>
    /// The deciding device's say over one player's action (the host online, the only device
    /// offline): it starts only on a full meter, one at a time, and strikes once, soon after it
    /// started, of the kind that started, where the deciding device sees the player.
    /// </summary>
    public sealed class ActionGate
    {
        /// <summary>Seconds from start to strike at most: the longest windup (the slam's fall, held
        /// up to 3 s) with room for the network.</summary>
        public const float StrikeWindow = 6f;
        /// <summary>A strike claimed further than this from where the player is seen moves there.</summary>
        public const float Reach = 4f;

        private bool _pending;
        private ActionKind _kind;
        private float _startedAt;

        /// <summary>Start <paramref name="kind"/> if the player carries it, is in play, nothing else is
        /// under way and the meter is full; spends the meter. False changes nothing.</summary>
        public bool TryStart(ActionMeter meter, ActionKind kind, ActionKind carried, bool alive, float now)
        {
            if (_pending && now - _startedAt <= StrikeWindow)
                return false;
            if (!alive || kind != carried || !meter.TrySpend())
                return false;
            _pending = true;
            _kind = kind;
            _startedAt = now;
            return true;
        }

        /// <summary>The strike of the action that started, once. False for any other: too late, of
        /// another kind, or never started.</summary>
        public bool TryStrike(ActionKind kind, float now)
        {
            if (!_pending || _kind != kind || now - _startedAt > StrikeWindow)
                return false;
            _pending = false;
            return true;
        }

        /// <summary>Where a strike lands: where it was claimed, unless that's out of <see cref="Reach"/>
        /// of where the deciding device sees it.</summary>
        public static Vector2 Where(Vector2 claimed, Vector2 seen) =>
            (claimed - seen).magnitude <= Reach ? claimed : seen;
    }
}
