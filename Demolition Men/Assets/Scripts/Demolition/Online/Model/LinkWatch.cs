namespace Demolition.Online
{
    public enum LinkVerdict { Ok, Unreachable, PeerSilent, Away }

    /// <summary>
    /// Whether the relay link still works (docs/multiplayer.md O4), judged on this device's
    /// clock. A paired peer pings every second, so <see cref="Timeout"/> seconds without a
    /// word means it froze: a phone sent to the background, a hidden browser tab, a dead
    /// network. A gap that long between our own ticks means this device froze, and the other
    /// side has given up on it already. Shorter hitches (a level loading) are forgiven.
    /// </summary>
    public sealed class LinkWatch
    {
        public const float Timeout = 10f;
        private const float Hitch = 1f;

        private float _opened, _lastHeard, _lastTick;
        private bool _ready;

        /// <summary>The socket started connecting.</summary>
        public void Opened(float now)
        {
            _opened = _lastHeard = _lastTick = now;
            _ready = false;
        }

        /// <summary>The relay answered: the host has its code, or the client is in the room.</summary>
        public void Ready() => _ready = true;

        /// <summary>Anything arrived from the relay or the peer.</summary>
        public void Heard(float now) => _lastHeard = now;

        /// <param name="paired">Both players are in the room (only then is silence suspicious).</param>
        public LinkVerdict Tick(float now, bool paired)
        {
            float gap = now - _lastTick;
            _lastTick = now;
            if (!_ready)
                return now - _opened > Timeout ? LinkVerdict.Unreachable : LinkVerdict.Ok;
            if (!paired)
                return LinkVerdict.Ok;
            if (gap > Timeout)
                return LinkVerdict.Away;
            if (gap > Hitch)
                _lastHeard = now; // our own hitch: let what queued up meanwhile arrive before judging the peer
            return now - _lastHeard > Timeout ? LinkVerdict.PeerSilent : LinkVerdict.Ok;
        }
    }
}
