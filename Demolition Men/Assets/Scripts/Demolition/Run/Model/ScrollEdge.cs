using UnityEngine;

namespace Demolition.Run
{
    /// <summary>
    /// The scrolling left edge of play, in world units: nothing about a device's screen or the
    /// players' zoom goes in, so every device plays by the same line, and the ranked server
    /// replays it from a run's poses. The front player pushes it once they are
    /// <see cref="PushDistance"/> ahead of it; from that first push on it also moves at the level's
    /// scroll speed, up to where the finish sits on the push line. It only ever moves right.
    ///
    /// Online the host's edge is the one that counts. A client runs its own, so its view moves
    /// with its own player at once, and follows the host's from the snapshots.
    /// </summary>
    public sealed class ScrollEdge
    {
        /// <summary>How much of the world every screen shows across, before zooming out.</summary>
        public const float ViewWidth = 32f;
        public const float PushLine = 0.7f;
        public const float PushDistance = ViewWidth * PushLine;

        /// <summary>The edge starts here at the furthest left: nothing to see left of the level.</summary>
        public const float LevelLeft = -0.5f;
        /// <summary>How far behind the spawn the edge starts.</summary>
        public const float SpawnRoom = 10.6f;

        /// <summary>A client's edge may lead the host's latest snapshot by this much (it is a little
        /// old when it arrives) before it stops to wait.</summary>
        public const float FollowLead = 1f;
        /// <summary>Units/s at which a client's edge catches up with the host's.</summary>
        public const float CatchUpSpeed = 16f;

        private bool _following;
        private float _hostLeft;

        public float Left { get; private set; }
        public bool Pushed { get; private set; }
        public float Speed { get; set; }
        /// <summary>The finish line's x: the edge stops with the finish on the push line.</summary>
        public float FinishX { get; set; }

        public ScrollEdge(float left, float speed, float finishX = float.PositiveInfinity)
        {
            Left = left;
            Speed = speed;
            FinishX = finishX;
        }

        public static float StartLeft(float spawnX) => Mathf.Max(spawnX - SpawnRoom, LevelLeft);

        /// <summary>Put the edge somewhere else, for a shot that starts mid-level.</summary>
        public void Place(float left, bool pushed)
        {
            Left = left;
            Pushed = pushed;
        }

        /// <summary>Online client: the host's edge, from its latest snapshot.</summary>
        public void Follow(float hostLeft)
        {
            _following = true;
            _hostLeft = hostLeft;
        }

        /// <param name="frontX">The front player's x; -infinity for nobody.</param>
        public void Tick(float deltaTime, float frontX)
        {
            float next = Left, push = frontX - PushDistance;
            if (push > next)
            {
                next = push;
                Pushed = true;
            }
            else if (Pushed && (!_following || next < _hostLeft + FollowLead))
            {
                next += Speed * deltaTime;
            }
            if (_following && next < _hostLeft)
            {
                next = Mathf.Min(_hostLeft, next + CatchUpSpeed * deltaTime);
                Pushed = true;
            }
            Left = Mathf.Max(Left, Mathf.Min(next, FinishX - PushDistance));
        }
    }
}
