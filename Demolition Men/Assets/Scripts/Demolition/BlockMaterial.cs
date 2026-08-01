using UnityEngine;

namespace Demolition
{
    public enum BlockMaterial
    {
        Brick,
        Metal,
        Wood,
        Glass,
        Support,
    }

    public static class BlockMaterialInfo
    {
        /// <summary>Default health per material. Punch damage is compared against this.</summary>
        public static float DefaultHealth(BlockMaterial material)
        {
            switch (material)
            {
                case BlockMaterial.Metal:   return 240f;
                case BlockMaterial.Support: return 400f;
                case BlockMaterial.Brick:   return 120f;
                case BlockMaterial.Wood:    return 70f;
                case BlockMaterial.Glass:   return 25f;
                default:                    return 100f;
            }
        }

        /// <summary>Debug tint used by the code-driven test scene.</summary>
        public static Color Tint(BlockMaterial material)
        {
            switch (material)
            {
                case BlockMaterial.Metal:   return new Color(0.62f, 0.66f, 0.72f);
                case BlockMaterial.Support: return new Color(0.85f, 0.55f, 0.15f);
                case BlockMaterial.Brick:   return new Color(0.72f, 0.36f, 0.28f);
                case BlockMaterial.Wood:    return new Color(0.55f, 0.40f, 0.24f);
                case BlockMaterial.Glass:   return new Color(0.55f, 0.78f, 0.85f);
                default:                    return Color.white;
            }
        }
    }
}
