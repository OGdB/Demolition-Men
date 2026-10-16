using System;
using UnityEngine;

namespace Demolition.Generation
{
    /// <summary>How each <see cref="TileKind"/> looks in a theme: sprite variants plus the
    /// tint, rotation and scale that fit them to the block's footprint.</summary>
    [CreateAssetMenu(menuName = "Demolition/Building Tileset")]
    public sealed class BuildingTileset : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public TileKind kind;
            public Sprite[] sprites = new Sprite[0];
            public Color tint = Color.white;
            public float rotation;
            public Vector2 scale = Vector2.one;
            [Tooltip("In cells, from the footprint centre (e.g. a thin plank on the top half).")]
            public Vector2 offset;
            public int sortingOrder;
        }

        [SerializeField] private Entry[] entries = new Entry[0];
        [Tooltip("Drawn behind window panes (dark wall seen through the glass).")]
        [SerializeField] private Sprite windowBack;
        [SerializeField] private Color windowBackTint = new Color(0.47f, 0.47f, 0.47f);

        /// <summary>Add the block's sprite as a child "Visual" (and a window back for glass).</summary>
        public void Dress(GameObject block, SpecBlock spec)
        {
            Entry entry = Find(spec.Kind);
            if (entry == null || entry.sprites.Length == 0)
                return;

            // The visual must be the first SpriteRenderer under the block: BuildingBlock
            // leans/tints that one.
            SpriteRenderer visual = AddSprite(block.transform, "Visual", entry.sprites[spec.Variant % entry.sprites.Length],
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
