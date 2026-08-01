using UnityEngine;

namespace Demolition
{
    /// <summary>Generates a cached 1x1 white sprite (1 pixel-per-unit) for the test scene,
    /// so the demo needs no imported art.</summary>
    public static class PrimitiveSprite
    {
        private static Sprite _unit;

        public static Sprite Unit()
        {
            if (_unit != null)
                return _unit;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();

            _unit = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            _unit.name = "UnitSprite";
            return _unit;
        }
    }
}
