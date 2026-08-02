using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// One-click test harness. Drop this on a single empty GameObject in an empty scene
    /// and press Play — it builds a ground, a realistic hollow building (outer walls with
    /// windows, interior floor slabs, load-bearing support columns, a ground-floor doorway
    /// and a roof), a player, and a camera entirely from code.
    ///
    /// Punch out a support column or a wall and watch whatever loses its path to the
    /// foundation collapse. Fallen blocks settle into persistent rubble and can hurt the
    /// player on the way down. An on-screen HUD shows destruction progress and health.
    /// </summary>
    public class DemoBootstrap : MonoBehaviour
    {
        [Header("Building size")]
        [SerializeField] private int width = 12;
        [SerializeField] private int height = 12;
        [SerializeField] private float blockSize = 0.7f;
        [SerializeField] private float blockGap = 0.015f;
        [SerializeField] private Vector2 buildingOrigin = new Vector2(3f, 0.35f);

        [Header("Toggles")]
        [SerializeField] private bool buildOnStart = true;
        [SerializeField] private bool showHud = true;

        private DestructibleBuilding _building;
        private PlayerHealth _playerHealth;
        private Texture2D _white;

        // Interior floor rows and support-column columns are derived from the size.
        private int _floorA, _floorB, _colA, _colB;

        private void Start()
        {
            if (buildOnStart)
                BuildScene();
        }

        public void BuildScene()
        {
            _floorA = Mathf.RoundToInt(height * 0.34f);
            _floorB = Mathf.RoundToInt(height * 0.67f);
            _colA = Mathf.Max(2, Mathf.RoundToInt(width * 0.33f));
            _colB = Mathf.Min(width - 3, Mathf.RoundToInt(width * 0.66f));

            EnsureCamera();
            CreateGround();
            CreateBuilding();
            CreatePlayer(new Vector2(buildingOrigin.x - 2.2f, buildingOrigin.y + 1.5f));
        }

        private float Pitch => blockSize + blockGap;

        private void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(6f, height * Pitch * 0.62f);
            cam.backgroundColor = new Color(0.16f, 0.18f, 0.22f);
            cam.transform.position = new Vector3(
                buildingOrigin.x + (width - 1) * Pitch * 0.5f,
                buildingOrigin.y + (height - 1) * Pitch * 0.5f,
                -10f);
        }

        private void CreateGround()
        {
            var go = NewSpriteObject("Ground", new Color(0.22f, 0.24f, 0.28f));
            go.transform.position = new Vector3(buildingOrigin.x + (width - 1) * Pitch * 0.5f, -1f, 0f);
            go.transform.localScale = new Vector3(80f, 2f, 1f);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            go.AddComponent<BoxCollider2D>();
        }

        private void CreateBuilding()
        {
            var buildingGo = new GameObject("DestructibleBuilding");
            _building = buildingGo.AddComponent<DestructibleBuilding>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!TryGetCell(x, y, out BlockMaterial mat, out bool isAnchor))
                        continue; // hollow interior / doorway

                    var go = NewSpriteObject($"Block_{x}_{y}", BlockMaterialInfo.Tint(mat));
                    go.transform.SetParent(buildingGo.transform);
                    go.transform.position = new Vector3(
                        buildingOrigin.x + x * Pitch,
                        buildingOrigin.y + y * Pitch,
                        0f);
                    go.transform.localScale = new Vector3(blockSize, blockSize, 1f);

                    go.AddComponent<Rigidbody2D>();   // BuildingBlock.Awake sets it Static
                    go.AddComponent<BoxCollider2D>();  // punch/impact detection is component-based

                    var block = go.AddComponent<BuildingBlock>();
                    block.Initialise(_building, new Vector2Int(x, y), mat);
                    _building.RegisterBlock(block, new Vector2Int(x, y), isAnchor);
                }
            }
        }

        /// <summary>
        /// Describes the building as a facade cross-section: solid foundation, a roof cap,
        /// two outer walls with windows, full-width interior floor slabs, two load-bearing
        /// support columns, and a doorway carved into the left wall at ground level.
        /// </summary>
        private bool TryGetCell(int x, int y, out BlockMaterial mat, out bool isAnchor)
        {
            mat = BlockMaterial.Brick;
            isAnchor = false;

            bool isWall = x == 0 || x == width - 1;
            bool isColumn = x == _colA || x == _colB;
            bool isFloor = y == _floorA || y == _floorB;

            // Doorway: an opening in the left wall at ground level.
            if (x == 0 && (y == 1 || y == 2))
                return false;

            if (y == 0) { mat = BlockMaterial.Metal; isAnchor = true; return true; } // foundation
            if (y == height - 1) { mat = BlockMaterial.Brick; return true; }          // roof cap
            if (isFloor) { mat = BlockMaterial.Metal; return true; }                  // floor slab
            if (isWall) { mat = (y % 2 == 1) ? BlockMaterial.Glass : BlockMaterial.Brick; return true; } // wall + windows
            if (isColumn) { mat = BlockMaterial.Support; return true; }               // load-bearing column

            return false; // hollow interior
        }

        private void CreatePlayer(Vector2 pos)
        {
            var go = NewSpriteObject("Player", new Color(0.95f, 0.85f, 0.25f));
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.75f, 1.5f, 1f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 3f;
            rb.freezeRotation = true;
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<SimplePlayerController>();
            _playerHealth = go.AddComponent<PlayerHealth>();
        }

        private static GameObject NewSpriteObject(string name, Color color)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PrimitiveSprite.Unit();
            sr.color = color;
            return go;
        }

        // ---------------- HUD ----------------

        private void OnGUI()
        {
            if (!showHud || _building == null)
                return;

            if (_white == null)
            {
                _white = new Texture2D(1, 1);
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
            }

            const int x = 12, w = 320;
            GUI.Box(new Rect(x - 4, 8, w + 8, 118), GUIContent.none);

            GUI.Label(new Rect(x, 14, w, 20), $"Destruction: {_building.DestroyedFraction * 100f:0}%");
            DrawBar(new Rect(x, 34, w, 16), _building.DestroyedFraction,
                    new Color(0.85f, 0.55f, 0.15f));

            if (_playerHealth != null)
            {
                float hp = _playerHealth.Max > 0 ? _playerHealth.Current / _playerHealth.Max : 0f;
                GUI.Label(new Rect(x, 56, w, 20), $"Health: {_playerHealth.Current:0}");
                DrawBar(new Rect(x, 76, w, 16), hp, Color.Lerp(new Color(0.8f, 0.2f, 0.2f),
                        new Color(0.3f, 0.8f, 0.3f), hp));
            }

            GUI.Label(new Rect(x, 98, w, 20), "A/D move · Space jump · J / L-Mouse punch");
        }

        private void DrawBar(Rect r, float fraction, Color fill)
        {
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(r, _white);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fraction), r.height), _white);
            GUI.color = prev;
        }
    }
}
