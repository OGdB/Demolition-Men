using System.Collections.Generic;
using Demolition.Levels;
using UnityEngine;

namespace Demolition.Run
{
    /// <summary>
    /// The level camera. Its left edge is <see cref="Edge"/>, the rule the run judges
    /// out-of-bounds with, and every screen shows the same width of the world from there:
    /// <see cref="ScrollEdge.ViewWidth"/> times the zoom. So a device's shape only decides how much
    /// sky it sees. Screens wider than <see cref="WidestAspect"/> get bars at the sides. The bottom
    /// stays on the authored ground framing, and the framing zooms out so every target stays in
    /// view on the widest screen too: jumping players raise the top, and players below the ground
    /// (sewers) lower the bottom. The players can zoom it a little on top of that (<see cref="Zoom"/>,
    /// unless <see cref="GameConfig.playerZoom"/> is off): out to see upper floors, in as far as the
    /// framing still holds every target. Zooming only widens ahead and up, and changes no rule.
    ///
    /// The run controller decides when it scrolls and which players it frames.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ScrollCamera : MonoBehaviour
    {
        /// <summary>The widest screen shown in full: past it, bars at the sides. Covers the widest
        /// phones and 21:9 monitors.</summary>
        public const float WidestAspect = 2.4f;

        [Header("Scrolling")]
        [Tooltip("Auto-scroll speed (units/s) after the first push.")]
        [SerializeField] private float scrollSpeed = 1.5f;

        [Header("Framing")]
        [Tooltip("Feet-to-head height of a player.")]
        [SerializeField] private float playerHeight = 2.5f;
        [SerializeField] private float marginAbove = 1f;
        [SerializeField] private float marginBelow = 0.5f;
        [Tooltip("Room kept right of the front player.")]
        [SerializeField] private float marginX = 3f;
        [Tooltip("Widest the framing zooms out to, in view widths.")]
        [SerializeField] private float maxZoom = 3.33f;
        [Tooltip("View widths per second while zooming in / out.")]
        [SerializeField] private float zoomInSpeed = 0.35f;
        [SerializeField] private float zoomOutSpeed = 0.55f;
        [SerializeField] private float verticalFollowSpeed = 2.5f;
        [Tooltip("How far past the right edge the framing looks for lower ground (a trench floor ahead).")]
        [SerializeField] private float groundLookAhead = 4f;

        [Header("Player zoom")]
        [SerializeField] private float minPlayerZoom = 0.8f;
        [SerializeField] private float maxPlayerZoom = 1.5f;

        // The players' zoom on top of the framing; kept from one level to the next.
        private static float _playerZoom = 1f;

        private readonly List<Transform> _targets = new List<Transform>();
        private float _floorY;
        private float _zoom = 1f;
        // Where framing put the camera; the shake is added on top of it.
        private Vector3 _framed;
        private Vector3 _shake;
        private Vector2Int _screen;
        private Camera _bars;

        public Camera Camera { get; private set; }
        public ScrollEdge Edge { get; private set; }
        public float ScrollSpeed { get => scrollSpeed; set => Edge.Speed = scrollSpeed = Mathf.Max(0f, value); }

        /// <summary>Optional: the lowest ground surface between two world x, +infinity if none
        /// (a generated level supplies it). The framing lowers its bottom so a floor below the
        /// street shows before anyone drops onto it; a pit has no floor, so it stays a dark hole.</summary>
        public System.Func<float, float, float> LowestGround { get; set; }
        /// <summary>Half the widest view the framing zooms out to.</summary>
        public float MaxHalfWidth => maxZoom * ScrollEdge.ViewWidth * 0.5f;
        /// <summary>Set by the run controller: scrolling only happens while playing.</summary>
        public bool Scrolling { get; set; }

        private float HalfWidth => _zoom * ScrollEdge.ViewWidth * 0.5f;

        private void Awake()
        {
            Camera = GetComponent<Camera>();
            // The authored framing defines the ground: never show below it unless a player is there.
            _floorY = transform.position.y - Camera.orthographicSize;
            Edge = new ScrollEdge(transform.position.x - ScrollEdge.ViewWidth * 0.5f, scrollSpeed);
            FitScreen();
            Snap();
            CameraShake.Clear();
        }

        /// <summary>Start from here (a level placed the camera): the edge's x and the ground framing's bottom.</summary>
        public void Place(float left, float floorY)
        {
            Edge.Place(left, false);
            _floorY = floorY;
            Snap();
        }

        /// <summary>Start at this edge already scrolling, as a run would be by then (the Clip Lab's shots mid-level).</summary>
        public void PlaceScrolling(float left)
        {
            Edge.Place(left, true);
            Snap();
        }

        /// <summary>Below the street with no floor there, further than <paramref name="margin"/>: out of bounds.</summary>
        public bool IsBelowPlay(Vector3 p, float margin) => FallingIntoPit(p) && p.y < _floorY - margin;

        /// <summary>Zoom out (+) or in (-) by whole or fractional steps (see <see cref="PlayerInputFrame.Zoom"/>).</summary>
        public void Zoom(float steps)
        {
            if (steps != 0f)
                _playerZoom = Mathf.Clamp(_playerZoom * Mathf.Pow(PlayerInputFrame.ZoomStep, steps), minPlayerZoom, maxPlayerZoom);
        }

        public void SetTargets(IEnumerable<Transform> targets)
        {
            _targets.Clear();
            _targets.AddRange(targets);
        }

        private void LateUpdate()
        {
            FitScreen();
            transform.position = _framed;
            _targets.RemoveAll(t => t == null); // online, NGO destroys a client's player copies before its scene goes
            if (_targets.Count > 0)
                FrameTargets();
            _framed = transform.position;
            _shake = CameraShake.Tick(Time.deltaTime);
            transform.position = _framed + _shake;
        }

        private void FrameTargets()
        {
            float maxX = float.NegativeInfinity;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (Transform t in _targets)
            {
                Vector3 p = t.position;
                if (FallingIntoPit(p))
                    continue;
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }

            if (Scrolling)
                Edge.Tick(Time.deltaTime, maxX);
            if (!float.IsInfinity(maxX))
                Frame(maxX, minY, maxY);
            else
                PlaceX();
        }

        // Below the street with no floor under it: the player is gone (the run takes them out at
        // the bottom), and the camera doesn't dive after them.
        private bool FallingIntoPit(Vector3 p)
        {
            if (p.y > _floorY - 1f)
                return false;
            float ground = LowestGround != null ? LowestGround(p.x - 0.5f, p.x + 0.5f) : float.PositiveInfinity;
            return float.IsInfinity(ground) || p.y < ground - 2f;
        }

        private void Frame(float maxX, float minY, float maxY)
        {
            float bottom = Mathf.Min(_floorY, minY - marginBelow);
            if (LowestGround != null)
            {
                float ground = LowestGround(Edge.Left, Edge.Left + HalfWidth * 2f + groundLookAhead);
                if (!float.IsInfinity(ground))
                    bottom = Mathf.Min(bottom, ground - marginBelow);
            }
            float top = maxY + playerHeight + marginAbove;
            // The same zoom on every screen: what the widest one needs to hold the targets.
            float fit = Mathf.Max((top - bottom) * WidestAspect, maxX + marginX - Edge.Left) / ScrollEdge.ViewWidth;
            float wanted = Mathf.Clamp(fit, 1f, maxZoom);
            // Zooming in never crops a target, which would put them off the screen.
            GameConfig config = GameConfig.Instance;
            if (config == null || config.playerZoom)
                wanted = Mathf.Min(Mathf.Max(wanted * _playerZoom, fit), maxZoom);

            float speed = wanted > _zoom ? zoomOutSpeed : zoomInSpeed;
            _zoom = Mathf.MoveTowards(_zoom, wanted, speed * Time.deltaTime);

            Camera.orthographicSize = HalfWidth / Camera.aspect;
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(pos.y, bottom + Camera.orthographicSize, Time.deltaTime * verticalFollowSpeed);
            transform.position = pos;
            PlaceX();
        }

        // The left edge of the screen is the edge of play, however far the view zooms.
        private void PlaceX()
        {
            Vector3 pos = transform.position;
            pos.x = Edge.Left + HalfWidth;
            transform.position = pos;
        }

        // Straight to the framing, with no zoom or follow in between.
        private void Snap()
        {
            Camera.orthographicSize = HalfWidth / Camera.aspect;
            transform.position = new Vector3(Edge.Left + HalfWidth, _floorY + Camera.orthographicSize, transform.position.z);
            _framed = transform.position;
        }

        // Screens wider than the widest we show draw into its share in the middle, with bars beside it.
        private void FitScreen()
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (screen == _screen || screen.x <= 0 || screen.y <= 0)
                return;
            _screen = screen;
            float share = Mathf.Min(1f, WidestAspect / ((float)screen.x / screen.y));
            Camera.rect = new Rect((1f - share) * 0.5f, 0f, share, 1f);
            if (share < 1f && _bars == null)
            {
                _bars = new GameObject("Bars").AddComponent<Camera>();
                _bars.transform.SetParent(transform, false);
                _bars.clearFlags = CameraClearFlags.SolidColor;
                _bars.backgroundColor = Color.black;
                _bars.cullingMask = 0;
                _bars.depth = Camera.depth - 1f;
            }
            if (_bars != null)
                _bars.enabled = share < 1f;
        }
    }
}
