using System;
using UnityEngine;

namespace Demolition.Generation
{
    /// <summary>How each <see cref="TileKind"/> looks in a theme: sprite variants plus the
    /// tint, rotation and scale that fit them to the block's footprint. Wall kinds can bring
    /// corner variants (the building's left and right silhouette edges, with quoins, posts or
    /// pilasters); the back wall has one look per storey, and the rooms' props
    /// (<see cref="Furnishing"/>) come from here by name. A sprite that gives light has a lit
    /// overlay: only the pixels that glow, drawn over it where a theme lights its buildings.</summary>
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
            [Tooltip("The glowing pixels of each plain sprite, in the same order (none: it gives no light).")]
            public Sprite[] lit = new Sprite[0];
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

        [Serializable]
        public sealed class Prop
        {
            [Tooltip("Its name in the archetypes' furnish lists (Furnishing's catalog).")]
            public string name;
            public Sprite sprite;
            [Tooltip("The glowing pixels, drawn over the sprite while the building has power. None: it gives no light.")]
            public Sprite lit;
            [Tooltip("The colour of the light it casts.")]
            public Color light = Color.white;
        }

        [SerializeField] private Entry[] entries = new Entry[0];
        [Tooltip("The building's back wall, behind open rooms, see-through blocks and windows. None: the sky shows through.")]
        [SerializeField] private Sprite interior;
        [Tooltip("Back wall right under a ceiling (its shadow). None: the plain back wall.")]
        [SerializeField] private Sprite interiorTop;
        [Tooltip("Other back-wall looks; each storey of a building draws one of them in turn.")]
        [SerializeField] private Sprite[] interiorVariants = new Sprite[0];
        [Tooltip("The under-ceiling version of each variant, in the same order.")]
        [SerializeField] private Sprite[] interiorTopVariants = new Sprite[0];
        [Tooltip("What stands in the rooms and on the roofs, by name. An archetype's prop this theme has no sprite for is left out.")]
        [SerializeField] private Prop[] props = new Prop[0];

        [Tooltip("One cell of a ladder (up a stairwell, up a street wall), tiled upwards.")]
        [SerializeField] private Sprite ladder;

        public Sprite Ladder => ladder;
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

        /// <summary>The look of a prop, or null when this theme doesn't draw it.</summary>
        public Prop PropFor(string name)
        {
            foreach (Prop prop in props)
                if (prop.name == name && prop.sprite != null)
                    return prop;
            return null;
        }

        /// <summary>The glowing pixels of the sprite <see cref="Dress"/> gives this block, or null.</summary>
        public Sprite LitFor(SpecBlock spec, BlockEdge edge = BlockEdge.None)
        {
            Entry entry = Find(spec.Kind);
            if (entry == null || entry.sprites.Length == 0 || entry.Pool(edge) != entry.sprites)
                return null;
            int i = spec.Variant % entry.sprites.Length;
            return i < entry.lit.Length ? entry.lit[i] : null;
        }

        /// <summary>Add the block's sprite as a child "Visual".</summary>
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
