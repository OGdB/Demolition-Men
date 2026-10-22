using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Run
{
    /// <summary>
    /// The level camera. Horizontally it only moves right: the front player pushes it
    /// once they pass <see cref="pushLine"/>, and from that first push on it also
    /// auto-scrolls at <see cref="scrollSpeed"/>. Vertically it keeps the authored
    /// ground framing and zooms out so every target stays in view: jumping players
    /// raise the top, and players below the ground (sewers) lower the bottom. The players
    /// can zoom it a little on top of that (<see cref="Zoom"/>): out to see upper floors,
    /// in as far as the framing still holds every target.
    ///
    /// The run controller decides when it scrolls and which players it frames, and
    /// judges out-of-bounds from its viewport.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ScrollCamera : MonoBehaviour
    {
        [Header("Scrolling")]
        [Tooltip("Auto-scroll speed (units/s) after the first push.")]
        [SerializeField] private float scrollSpeed = 1.5f;
        [Tooltip("Viewport x (0 = left edge, 1 = right edge) where the front player starts pushing the camera.")]
        [SerializeField, Range(0.5f, 0.95f)] private float pushLine = 0.7f;

        [Header("Framing")]
        [Tooltip("Feet-to-head height of a player.")]
        [SerializeField] private float playerHeight = 2.5f;
        [SerializeField] private float marginAbove = 1f;
        [SerializeField] private float marginBelow = 0.5f;
        [Tooltip("Horizontal room kept around the two players' spread.")]
        [SerializeField] private float marginX = 3f;
        [SerializeField] private float minZoom = 6f;
        [SerializeField] private float maxZoom = 30f;
        [Tooltip("Zoom change per second while zooming in / out.")]
        [SerializeField] private float zoomInSpeed = 3f;
        [SerializeField] private float zoomOutSpeed = 4.8f;
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
        // Where framing put the camera; the shake is added on top of it.
        private Vector3 _framed;
        private Vector3 _shake;

        public Camera Camera { get; private set; }
        public float ScrollSpeed { get => scrollSpeed; set => scrollSpeed = Mathf.Max(0f, value); }

        /// <summary>Optional: the lowest ground surface between two world x, +infinity if none
        /// (a generated level supplies it). The framing lowers its bottom so a floor below the
        /// street shows before anyone drops onto it; a pit has no floor, so it stays a dark hole.</summary>
        public System.Func<float, float, float> LowestGround { get; set; }
        /// <summary>Widest orthographic size the framing zooms out to.</summary>
        public float MaxZoom => maxZoom;
        /// <summary>Set by the run controller: scrolling only happens while playing.</summary>
        public bool Scrolling { get; set; }
        public bool Pushed { get; private set; }
        /// <summary>World x the camera stops at (the finish line sits on the push line).</summary>
        public float StopX { get; set; } = float.PositiveInfinity;

        private float PushOffset => (pushLine * 2f - 1f) * Camera.orthographicSize * Camera.aspect;

        private void Awake()
        {
            Camera = GetComponent<Camera>();
            // The authored framing defines the ground: never show below it unless a player is there.
            _floorY = transform.position.y - Camera.orthographicSize;
            _framed = transform.position;
            CameraShake.Clear();
            if (Camera.rect != new Rect(0f, 0f, 1f, 1f))
                AddLetterboxClear();
        }

        // Outside a letterboxed camera rect nothing clears the screen, so overlay UI drawn
        // in the bars would smear across frames. A camera behind it clears them to black.
        private void AddLetterboxClear()
        {
            var go = new GameObject("LetterboxClear");
            go.transform.SetParent(transform, false);
            var clear = go.AddComponent<Camera>();
            clear.depth = Camera.depth - 1f;
            clear.clearFlags = CameraClearFlags.SolidColor;
            clear.backgroundColor = Color.black;
            clear.cullingMask = 0;
            clear.orthographic = true;
        }

        /// <summary>Take the current framing as the ground framing (after a level placed the camera).</summary>
        public void ResetFloor()
        {
            _floorY = transform.position.y - Camera.orthographicSize;
            _framed = transform.position;
        }

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
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (Transform t in _targets)
            {
                Vector3 p = t.position;
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }

            Frame(minX, maxX, minY, maxY);
            if (Scrolling)
                Scroll(maxX);
        }

        private void Frame(float minX, float maxX, float minY, float maxY)
        {
            float bottom = Mathf.Min(_floorY, minY - marginBelow);
            if (LowestGround != null)
            {
                float x = transform.position.x, half = Camera.orthographicSize * Camera.aspect;
                float ground = LowestGround(x - half, x + half + groundLookAhead);
                if (!float.IsInfinity(ground))
                    bottom = Mathf.Min(bottom, ground - marginBelow);
            }
            float top = maxY + playerHeight + marginAbove;
            float halfWidth = (maxX - minX) * 0.5f + marginX;
            float fit = Mathf.Max((top - bottom) * 0.5f, halfWidth / Camera.aspect);
            float wanted = Mathf.Clamp(fit, minZoom, maxZoom);
            // Zooming in never crops a target, which would put them out of bounds.
            wanted = Mathf.Min(Mathf.Max(wanted * _playerZoom, fit), maxZoom);

            float speed = wanted > Camera.orthographicSize ? zoomOutSpeed : zoomInSpeed;
            Camera.orthographicSize = Mathf.MoveTowards(Camera.orthographicSize, wanted, speed * Time.deltaTime);

            // Anchor the bottom edge, so extra height from zooming out goes to the sky.
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(pos.y, bottom + Camera.orthographicSize, Time.deltaTime * verticalFollowSpeed);
            transform.position = pos;
        }

        private void Scroll(float frontX)
        {
            Vector3 pos = transform.position;
            float pushX = frontX - PushOffset;
            if (pushX > pos.x)
            {
                pos.x = pushX;
                Pushed = true;
            }
            else if (Pushed)
            {
                pos.x += scrollSpeed * Time.deltaTime;
            }
            // Stop at the finish, but never scroll back left.
            pos.x = Mathf.Max(transform.position.x, Mathf.Min(pos.x, StopX - PushOffset));
            transform.position = pos;
        }
    }
}
