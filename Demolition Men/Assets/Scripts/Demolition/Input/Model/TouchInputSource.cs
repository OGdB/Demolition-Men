namespace Demolition
{
    /// <summary>
    /// Input written by the on-screen touch controls (<see cref="TouchControls"/>) and read
    /// by the player they drive. Presses are queued, so a tap shorter than a frame still
    /// counts.
    /// </summary>
    public sealed class TouchInputSource : IPlayerInputSource
    {
        /// <summary>The one set of on-screen controls (a phone has one touch player).</summary>
        public static readonly TouchInputSource Shared = new TouchInputSource();

        private bool _jumpHeld, _jumpPressed, _jumpReleased, _attackHeld, _dynamitePressed, _actionPressed;
        private int _cycle;
        private float _zoom;

        public InputStyle Style => InputStyle.Touch;

        public UnityEngine.Vector2 Move { get; set; }

        public void SetJump(bool held)
        {
            if (held && !_jumpHeld) _jumpPressed = true;
            if (!held && _jumpHeld) _jumpReleased = true;
            _jumpHeld = held;
        }

        public void SetAttack(bool held) => _attackHeld = held;
        public void PressDynamite() => _dynamitePressed = true;
        public void PressAction() => _actionPressed = true;
        public void PressCycle() => _cycle = 1;
        /// <summary>Pinch: zoom steps, added up until the next read.</summary>
        public void AddZoom(float steps) => _zoom += steps;

        /// <summary>Let go of everything (controls hidden, scene change).</summary>
        public void Release()
        {
            Move = UnityEngine.Vector2.zero;
            SetJump(false);
            _attackHeld = false;
            _zoom = 0f;
        }

        public PlayerInputFrame Read()
        {
            var frame = new PlayerInputFrame
            {
                Move = Move,
                JumpPressed = _jumpPressed,
                JumpHeld = _jumpHeld || _jumpPressed,
                JumpReleased = _jumpReleased,
                AttackHeld = _attackHeld,
                DynamitePressed = _dynamitePressed,
                ActionPressed = _actionPressed,
                WeaponCycle = _cycle,
                Zoom = _zoom,
            };
            _jumpPressed = _jumpReleased = _dynamitePressed = _actionPressed = false;
            _cycle = 0;
            _zoom = 0f;
            return frame;
        }
    }
}
