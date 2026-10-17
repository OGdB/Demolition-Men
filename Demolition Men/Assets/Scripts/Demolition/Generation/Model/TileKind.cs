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
        // Street props (LevelGrammar's dressing pass): small one-block "buildings" that break for points.
        Crate,
        Car,
        Barrel,
        Container,
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
                case TileKind.Car:
                    return new Vector2Int(2, 1);
                case TileKind.Container:
                    return new Vector2Int(3, 2);
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
                case TileKind.Crate:
                case TileKind.Barrel:
                    return BlockMaterial.Wood;
                case TileKind.Glass:
                    return BlockMaterial.Glass;
                case TileKind.MetalTruss:
                case TileKind.Car:
                case TileKind.Container:
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
                case TileKind.Barrel:
                    return new Rect(0.2f, 0f, 0.6f, 1f);
                case TileKind.Car:
                    return new Rect(0.05f, 0f, 1.9f, 0.85f);
                default:
                    Vector2Int size = Size(kind);
                    return new Rect(0f, 0f, size.x, size.y);
            }
        }

        /// <summary>Interior supports are trigger colliders: players walk through, hits still land.</summary>
        public static bool WalkThrough(TileKind kind) => kind == TileKind.Support;

        /// <summary>Health when it differs from the material's default (props), else -1.</summary>
        public static float Health(TileKind kind)
        {
            switch (kind)
            {
                case TileKind.Car: return 100f;    // a weapon's job: fists take a while
                case TileKind.Barrel: return 30f;  // one punch
                default: return -1f;
            }
        }

        /// <summary>Goes off like a stick of dynamite when destroyed (gas tanks).</summary>
        public static bool Explosive(TileKind kind) => kind == TileKind.Barrel;

        /// <summary>Kinds whose sprite leaves part of the cell open (a thin column, a floor
        /// board, a lattice): the building's back wall shows through them.</summary>
        public static bool SeeThrough(TileKind kind) =>
            kind == TileKind.Support || kind == TileKind.Plank || kind == TileKind.MetalTruss;
    }
}
