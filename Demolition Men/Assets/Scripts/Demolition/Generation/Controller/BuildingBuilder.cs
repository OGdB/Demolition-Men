using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Generation
{
    /// <summary>
    /// Spec → GameObjects: a <see cref="DestructibleBuilding"/> root with one
    /// <see cref="BuildingBlock"/> per spec block, its bottom-left cell corner at the origin.
    /// In play mode the cells are registered right away (no scan). In edit mode (preview,
    /// bake to prefab) nothing is registered; the building auto-scans its children at Start,
    /// which the collider layout reproduces exactly.
    /// </summary>
    public static class BuildingBuilder
    {
        private const string BlockLayer = "PlatformsLayer";

        public static DestructibleBuilding Build(BuildingSpec spec, BuildingTileset tileset, Vector3 origin, Transform parent = null)
        {
            var root = new GameObject(spec.Name);
            root.transform.SetParent(parent, false);
            root.transform.position = origin;
            var building = root.AddComponent<DestructibleBuilding>();
            bool register = Application.isPlaying;
            int layer = LayerMask.NameToLayer(BlockLayer);

            var coords = new List<Vector2Int>();
            foreach (SpecBlock b in spec.Blocks)
            {
                var go = new GameObject(b.Kind.ToString());
                if (layer >= 0)
                    go.layer = layer;
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(b.Rect.center.x, b.Rect.center.y, 0f);
                if (tileset != null)
                    tileset.Dress(go, b);

                go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
                var collider = go.AddComponent<BoxCollider2D>();
                Rect shape = TileKinds.Collider(b.Kind);
                collider.size = shape.size;
                collider.offset = shape.center - (Vector2)b.Rect.size * 0.5f;
                collider.isTrigger = TileKinds.WalkThrough(b.Kind);

                var block = go.AddComponent<BuildingBlock>();
                block.SetMaterial(b.Material);
                if (!register)
                    continue;
                coords.Clear();
                coords.AddRange(b.Cells());
                block.Initialise(building, coords);
                building.RegisterBlock(block, coords, b.Anchor);
            }
            return building;
        }
    }
}
