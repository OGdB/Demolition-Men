using UnityEngine;

namespace Demolition
{
    /// <summary>
    /// For sprites that give their own light (a lit window, a screen, the moon): no 2D light
    /// dims or lights them, so they glow where the level is dark.
    /// </summary>
    public static class UnlitSprite
    {
        private static Material _material;
        private static Sprite _halo;

        /// <summary>URP's own unlit sprite shader (its default unlit material is in every build
        /// with the pipeline's settings). The built-in one draws every sprite of a shared
        /// material with one texture under URP, so it only serves where URP isn't installed.</summary>
        public static Material Material
        {
            get
            {
                if (_material != null)
                    return _material;
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                return _material = new Material(shader);
            }
        }

        /// <summary>A soft round glow one unit across: white in the middle, nothing at its edge.</summary>
        public static Sprite Halo
        {
            get
            {
                if (_halo != null)
                    return _halo;
                const int Size = 64;
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[Size * Size];
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        float edge = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * (Size * 0.5f)) / (Size * 0.5f));
                        pixels[y * Size + x] = new Color(1f, 1f, 1f, edge * edge);
                    }
                texture.SetPixels(pixels);
                texture.Apply();
                return _halo = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size);
            }
        }
    }
}
