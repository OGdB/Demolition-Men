using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// One-click test harness. Drop this on a single empty GameObject in an empty scene
    /// and press Play — it builds a ground, a multi-storey destructible building, a
    /// player, and a camera entirely from code (no prefabs or imported art required).
    ///
    /// The building has two <see cref="BlockMaterial.Support"/> columns resting on the
    /// foundation row. Punch out a support column's base and everything it was holding
    /// loses its path to the ground and collapses — the emergent behaviour Option C
    /// is designed to produce.
    /// </summary>
    public class DemoBootstrap : MonoBehaviour
    {
        [Header("Building")]
        [SerializeField] private int width = 8;
        [SerializeField] private int height = 7;
        [SerializeField] private float blockSize = 0.9f;
        [SerializeField] private float blockGap = 0.02f;
        [SerializeField] private Vector2 buildingOrigin = new Vector2(2f, 0f);

        [Header("Toggles")]
        [SerializeField] private bool buildOnStart = true;
        [SerializeField] private bool showControlsOverlay = true;

        private void Start()
        {
            if (buildOnStart)
                BuildScene();
        }

        public void BuildScene()
        {
            EnsureCamera();
            CreateGround();
            CreateBuilding();
            CreatePlayer(new Vector2(buildingOrigin.x - 3f, 1.5f));
        }

        private void EnsureCamera()
        {
            if (Camera.main != null)
                return;
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.backgroundColor = new Color(0.16f, 0.18f, 0.22f);
            camGo.transform.position = new Vector3(buildingOrigin.x, 4f, -10f);
        }

        private void CreateGround()
        {
            var go = NewSpriteObject("Ground", new Color(0.22f, 0.24f, 0.28f));
            go.transform.position = new Vector3(buildingOrigin.x, -1f, 0f);
            go.transform.localScale = new Vector3(60f, 2f, 1f);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            go.AddComponent<BoxCollider2D>();
        }

        private void CreateBuilding()
        {
            var buildingGo = new GameObject("DestructibleBuilding");
            var building = buildingGo.AddComponent<DestructibleBuilding>();

            float pitch = blockSize + blockGap;
            int supportColA = 1;
            int supportColB = Mathf.Max(1, width - 2);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    BlockMaterial mat = PickMaterial(x, y, supportColA, supportColB);
                    var coord = new Vector2Int(x, y);

                    var go = NewSpriteObject($"Block_{x}_{y}", BlockMaterialInfo.Tint(mat));
                    go.transform.SetParent(buildingGo.transform);
                    go.transform.position = new Vector3(
                        buildingOrigin.x + x * pitch,
                        buildingOrigin.y + y * pitch,
                        0f);
                    go.transform.localScale = new Vector3(blockSize, blockSize, 1f);

                    go.AddComponent<Rigidbody2D>();      // BuildingBlock.Awake sets it Static
                    go.AddComponent<BoxCollider2D>();     // punch detection is component-based, not tag/layer

                    var block = go.AddComponent<BuildingBlock>();
                    block.Initialise(building, coord, mat);

                    bool isAnchor = y == 0; // foundation row is connected to the ground
                    building.RegisterBlock(block, coord, isAnchor);
                }
            }
        }

        private static BlockMaterial PickMaterial(int x, int y, int supportColA, int supportColB)
        {
            if (x == supportColA || x == supportColB)
                return BlockMaterial.Support;      // load-bearing columns
            if (y == 0)
                return BlockMaterial.Metal;        // sturdy foundation
            if ((x + y) % 5 == 0)
                return BlockMaterial.Glass;        // scattered weak spots
            if (y >= 5)
                return BlockMaterial.Wood;         // lighter upper floors
            return BlockMaterial.Brick;
        }

        private void CreatePlayer(Vector2 pos)
        {
            var go = NewSpriteObject("Player", new Color(0.95f, 0.85f, 0.25f));
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.8f, 1.6f, 1f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 3f;
            rb.freezeRotation = true;
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<SimplePlayerController>();
        }

        private static GameObject NewSpriteObject(string name, Color color)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PrimitiveSprite.Unit();
            sr.color = color;
            return go;
        }

        private void OnGUI()
        {
            if (!showControlsOverlay)
                return;
            const int w = 360, h = 92;
            GUI.Box(new Rect(10, 10, w, h), "Demolition — Option C test");
            GUI.Label(new Rect(22, 34, w, 20), "Move: A / D   Jump: Space   Punch: J or Left-Mouse");
            GUI.Label(new Rect(22, 54, w, 20), "Punch out an orange SUPPORT column base to collapse floors above.");
            GUI.Label(new Rect(22, 74, w, 20), "Glass (cyan) is weak; Metal (grey) foundation is tough.");
        }
    }
}
