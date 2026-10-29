using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Meta
{
    /// <summary>
    /// A player's look (<see cref="LookCatalog"/>) on screen: once the animator or an action has
    /// picked this frame's sprite, it swaps in the same frame recoloured by <see cref="LookPalette"/>,
    /// with the chain, bracelets and tattoos the look wears from the sheet's overlay
    /// (<c>Resources/LookOverlays/P1|P2/&lt;sheet&gt;</c>, tools/artpipe/accessories.py). Each sheet is
    /// made once per look and kept, so the sheets and overlays must be readable (Read/Write).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LookSkin : MonoBehaviour
    {
        private static readonly Dictionary<(LookInfo, bool, Texture2D), Texture2D> Sheets = new Dictionary<(LookInfo, bool, Texture2D), Texture2D>();
        private static readonly Dictionary<(LookInfo, bool, Sprite), Sprite> Frames = new Dictionary<(LookInfo, bool, Sprite), Sprite>();

        private SpriteRenderer _sprite;
        private bool _player2;
        private Sprite _shown;

        /// <summary>What it wears; null for the crew's own look.</summary>
        public LookInfo Look { get; private set; }

        /// <summary>Put <paramref name="look"/> on a player (Player_1 or Player_2); null for the crew's own.</summary>
        public static void Dress(GameObject player, LookInfo look)
        {
            if (!player.TryGetComponent(out LookSkin skin))
            {
                if (look == null)
                    return;
                skin = player.AddComponent<LookSkin>();
            }
            skin.Wear(look);
        }

        public void Wear(LookInfo look)
        {
            if (look == Look)
                return;
            Look = look;
            _player2 = gameObject.name == "Player_2"; // the stop sign's sheets
            _shown = null;
        }

        private void Awake() => _sprite = GetComponentInChildren<SpriteRenderer>();

        private void LateUpdate()
        {
            Sprite sprite = _sprite != null ? _sprite.sprite : null;
            if (Look == null || sprite == null || sprite == _shown)
                return;
            _shown = _sprite.sprite = Recoloured(sprite, Look, _player2);
        }

        /// <summary>The sprite as <paramref name="look"/> colours it, for Player 1's sheets or Player 2's.</summary>
        public static Sprite Recoloured(Sprite sprite, LookInfo look, bool player2)
        {
            if (look == null || sprite == null || sprite.packed)
                return sprite;
            if (Frames.TryGetValue((look, player2, sprite), out Sprite copy))
                return copy;
            Texture2D sheet = Recoloured(sprite.texture, look, player2);
            copy = sheet == sprite.texture ? sprite
                : Sprite.Create(sheet, sprite.rect, sprite.pivot / sprite.rect.size, sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect, sprite.border);
            copy.name = sprite.name;
            Frames[(look, player2, sprite)] = copy;
            Frames[(look, player2, copy)] = copy; // already in the look
            return copy;
        }

        /// <summary>The whole sheet as <paramref name="look"/> colours it.</summary>
        public static Texture2D Recoloured(Texture2D source, LookInfo look, bool player2)
        {
            if (Sheets.TryGetValue((look, player2, source), out Texture2D sheet))
                return sheet;
            sheet = source;
            if (source.isReadable)
            {
                Dictionary<int, int> map = LookPalette.Map(look, player2);
                Dictionary<int, int> extras = LookPalette.Extras(look);
                Texture2D overlay = Resources.Load<Texture2D>($"LookOverlays/{(player2 ? "P2" : "P1")}/{source.name}");
                Color32[] keys = overlay != null && overlay.isReadable && overlay.width == source.width && overlay.height == source.height
                    ? overlay.GetPixels32() : null;
                if (keys == null && extras.Count > 0)
                    Debug.LogWarning($"[Looks] no readable overlay of {source.name}'s size for {look.Id}: run tools/artpipe/accessories.py");
                if (overlay != null)
                    Resources.UnloadAsset(overlay);

                Color32[] pixels = source.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 p = pixels[i];
                    int key = keys != null && keys[i].a != 0 ? keys[i].r << 16 | keys[i].g << 8 | keys[i].b : -1;
                    if (p.a == 0 || key == LookPalette.KeepKey)
                        continue;
                    if (extras.TryGetValue(key, out int to) || map.TryGetValue(p.r << 16 | p.g << 8 | p.b, out to))
                        pixels[i] = new Color32((byte)(to >> 16), (byte)(to >> 8), (byte)to, p.a);
                }
                sheet = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
                {
                    name = source.name + " " + look.Id,
                    filterMode = source.filterMode,
                    wrapMode = source.wrapMode,
                };
                sheet.SetPixels32(pixels);
                sheet.Apply(false, true);
            }
            else
            {
                Debug.LogWarning($"[Looks] {source.name} isn't readable: tick Read/Write in its import settings so looks can recolour it.");
            }
            Sheets[(look, player2, source)] = sheet;
            return sheet;
        }
    }
}
