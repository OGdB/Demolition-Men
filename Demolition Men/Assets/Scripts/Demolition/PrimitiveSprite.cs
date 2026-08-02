using UnityEngine;

namespace Demolition
{
    /// <summary>Generates cached primitive sprites (unit square, disc, ring) for the test
    /// scene, so the demo needs no imported art. The disc/ring are exactly 1 world unit in
    /// diameter at scale 1 — scale by (radius * 2) to match a physics circle.</summary>
    public static class PrimitiveSprite
    {
        private static Sprite _unit;
        private static Sprite _disc;
        private static Sprite _ring;

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

        /// <summary>Soft-edged filled circle.</summary>
        public static Sprite Disc()
        {
            if (_disc == null)
            {
                _disc = CreateCircleSprite(filled: true);
                _disc.name = "DiscSprite";
            }
            return _disc;
        }

        /// <summary>Thin circle outline — used to show the punch overlap area.</summary>
        public static Sprite Ring()
        {
            if (_ring == null)
            {
                _ring = CreateCircleSprite(filled: false);
                _ring.name = "RingSprite";
            }
            return _ring;
        }

        private static Sprite CreateCircleSprite(bool filled)
        {
            const int size = 64;
            const float ringWidth = 2.5f;
            float centre = (size - 1) * 0.5f;
            float radius = size * 0.5f - 1.5f;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                    float a = filled
                        ? Mathf.Clamp01((radius - r) / 1.5f)
                        : Mathf.Clamp01(1f - Mathf.Abs(radius - r) / ringWidth);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            // pixelsPerUnit = diameter in pixels → the circle is exactly 1 unit across.
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), radius * 2f);
        }
    }
}
