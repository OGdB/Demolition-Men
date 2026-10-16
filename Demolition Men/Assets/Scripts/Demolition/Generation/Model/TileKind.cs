using UnityEngine;

namespace Demolition.Generation
{
    /// <summary>What a generated block is: decides its footprint, material, collider and
    /// (through a tileset) its look.</summary>
    public enum TileKind
    {
        Brick,
        BrickTall,
        Concrete,
        Wood,
        Glass,
        Door,
        Plank,
        MetalTruss,
        Beam,
        BrickCap,
        Support,
        Chimney,
    }

    public static class TileKinds
    {
        /// <summary>Footprint in cells.</summary>
        public static Vector2Int Size(TileKind kind)
        {
            switch (kind)
            {
                case TileKind.BrickTall:
                case TileKind.Glass:
                case TileKind.Door:
                    return new Vector2Int(1, 2);
                case TileKind.MetalTruss:
                case TileKind.Beam:
                case TileKind.BrickCap:
                    return new Vector2Int(2, 1);
                default:
                    return Vector2Int.one;
            }
        }

        public static BlockMaterial Material(TileKind kind)
        {
            switch (kind)
            {
                case TileKind.Wood:
                case TileKind.Door:
                case TileKind.Plank:
                case TileKind.Beam:
                    return BlockMaterial.Wood;
                case TileKind.Glass:
                    return BlockMaterial.Glass;
                case TileKind.MetalTruss:
                    return BlockMaterial.Metal;
                case TileKind.Support:
                    return BlockMaterial.Support;
                default:
                    return BlockMaterial.Brick;
            }
        }

        /// <summary>Collider within the footprint, in cells from its bottom-left corner.</summary>
        public static Rect Collider(TileKind kind)
        {
            switch (kind)
            {
                case TileKind.Glass:
                case TileKind.Door:
                    return new Rect(0.25f, 0f, 0.5f, 2f);
                case TileKind.Plank:
                    return new Rect(0f, 0.5f, 1f, 0.5f); // thin floor: walk on the cell's top
                default:
                    Vector2Int size = Size(kind);
                    return new Rect(0f, 0f, size.x, size.y);
            }
        }

        /// <summary>Interior supports are trigger colliders: players walk through, hits still land.</summary>
        public static bool WalkThrough(TileKind kind) => kind == TileKind.Support;
    }
}
