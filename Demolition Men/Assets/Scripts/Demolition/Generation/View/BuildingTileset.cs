using System;
using UnityEngine;

namespace Demolition.Generation
{
    /// <summary>How each <see cref="TileKind"/> looks in a theme: sprite variants plus the
    /// tint, rotation and scale that fit them to the block's footprint. Wall kinds can bring
    /// corner variants (the building's left and right silhouette edges, with quoins, posts or
    /// pilasters); the back wall has one look per storey and details scattered over rooms.</summary>
    [CreateAssetMenu(menuName = "Demolition/Building Tileset")]
    public sealed class BuildingTileset : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public TileKind kind;
            public Sprite[] sprites = new Sprite[0];
            [Tooltip("Variants for a block on the building's left edge (empty: the plain sprites).")]
            public Sprite[] left = new Sprite[0];
            [Tooltip("Variants for a block on the building's right edge (empty: the plain sprites).")]
            public Sprite[] right = new Sprite[0];
            public Color tint = Color.white;
            public float rotation;
            public Vector2 scale = Vector2.one;
            [Tooltip("In cells, from the footprint centre (e.g. a thin plank on the top half).")]
            public Vector2 offset;
            public int sortingOrder;

            /// <summary>The sprite pool for a block on the given edges.</summary>
            public Sprite[] Pool(BlockEdge edge)
            {
                if ((edge & BlockEdge.Left) != 0 && left.Length > 0)
                    return left;
                if ((edge & BlockEdge.Right) != 0 && right.Length > 0)
                    return right;
                return sprites;
            }
        }

        [SerializeField] private Entry[] entries = new Entry[0];
        [Tooltip("Drawn behind window panes (dark wall seen through the glass).")]
        [SerializeField] private Sprite windowBack;
        [SerializeField] private Color windowBackTint = new Color(0.47f, 0.47f, 0.47f);
        [Tooltip("The building's back wall, behind open rooms and see-through blocks. None: the sky shows through.")]
        [SerializeField] private Sprite interior;
        [Tooltip("Back wall right under a ceiling (its shadow). None: the plain back wall.")]
        [SerializeField] private Sprite interiorTop;
        [Tooltip("Other back-wall looks; each storey of a building draws one of them in turn.")]
        [SerializeField] private Sprite[] interiorVariants = new Sprite[0];
        [Tooltip("The under-ceiling version of each variant, in the same order.")]
        [SerializeField] private Sprite[] interiorTopVariants = new Sprite[0];
        [Tooltip("Things on the back wall of open rooms (a picture, a shelf...), drawn over the wallpaper of some cells.")]
        [SerializeField] private Sprite[] interiorDetails = new Sprite[0];
        [Tooltip("Share of open room cells that get a detail.")]
        [SerializeField, Range(0f, 1f)] private float interiorDetailChance = 0.18f;

        public Sprite Interior => interior;
        public Sprite InteriorTop => interiorTop != null ? interiorTop : interior;

        /// <summary>The back wall of a storey: the base look and the variants in turn, starting
        /// where the building's seed says, so neighbouring storeys differ.</summary>
        public Sprite InteriorFor(int storey, bool underCeiling, int seed)
        {
            int count = 1 + interiorVariants.Length;
            int i = (int)(((uint)seed + (uint)storey) % (uint)count);
            if (i == 0)
                return underCeiling ? InteriorTop : interior;
            Sprite top = i - 1 < interiorTopVariants.Length ? interiorTopVariants[i - 1] : null;
            return underCeiling && top != null ? top : interiorVariants[i - 1];
        }

        /// <summary>A detail for the open room cell (x, y), or null: the same building shows
        /// the same details on every machine.</summary>
        public Sprite InteriorDetail(int x, int y, int seed)
        {
            if (interiorDetails.Length == 0)
                return null;
            uint h = CellHash(x, y, seed);
            if ((h & 0xFFFF) / 65536f >= interiorDetailChance)
                return null;
            return interiorDetails[(h >> 16) % (uint)interiorDetails.Length];
        }

        /// <summary>A well-mixed hash of a cell within a building (mirrored by level_preview.py).</summary>
        public static uint CellHash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 73856093u ^ (uint)x * 19349663u ^ (uint)y * 83492791u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return h;
            }
        }

        /// <summary>Add the block's sprite as a child "Visual" (and a window back for glass).</summary>
        public void Dress(GameObject block, SpecBlock spec, BlockEdge edge = BlockEdge.None)
        {
            Entry entry = Find(spec.Kind);
            if (entry == null || entry.sprites.Length == 0)
                return;
            Sprite[] pool = entry.Pool(edge);

            // The visual must be the first SpriteRenderer under the block: BuildingBlock
            // leans/tints that one.
            SpriteRenderer visual = AddSprite(block.transform, "Visual", pool[spec.Variant % pool.Length],
                entry.tint, entry.sortingOrder);
            visual.transform.localPosition = entry.offset;
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, entry.rotation);
            visual.transform.localScale = new Vector3(entry.scale.x, entry.scale.y, 1f);

            if (spec.Kind == TileKind.Glass && windowBack != null)
                AddSprite(block.transform, "WindowBack", windowBack, windowBackTint, entry.sortingOrder - 1);
        }

        private Entry Find(TileKind kind)
        {
            foreach (Entry entry in entries)
                if (entry.kind == kind)
                    return entry;
            return null;
        }

        private static SpriteRenderer AddSprite(Transform parent, string name, Sprite sprite, Color tint, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tint;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
