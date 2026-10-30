using System;
using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Meta
{
    /// <summary>
    /// A player's look items (<see cref="LookCatalog"/>) on screen: once the animator or an action has
    /// picked this frame's sprite, it swaps in the same frame dressed in them. Colours change by
    /// <see cref="LookPalette"/>; the chain, bracelets and tattoos come from the sheet's overlay
    /// (<c>Resources/LookOverlays/P1|P2/&lt;sheet&gt;</c>, tools/artpipe/accessories.py), which also
    /// marks the player's own sign; another sign (<see cref="LookInfo.Head"/>) clears that and goes on
    /// where <c>Resources/LookHeads.json</c> says (tools/artpipe/heads.py). Each sheet is made once per
    /// set of items and kept, so the sheets, overlays and sign atlases must be readable (Read/Write).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LookSkin : MonoBehaviour
    {
        private static readonly Dictionary<(string, bool, Texture2D), Texture2D> Sheets = new Dictionary<(string, bool, Texture2D), Texture2D>();
        private static readonly Dictionary<(string, bool, Sprite), Sprite> Frames = new Dictionary<(string, bool, Sprite), Sprite>();

        private SpriteRenderer _sprite;
        private bool _player2;
        private Sprite _shown;

        /// <summary>What it wears, by slot (null where the crew's own).</summary>
        public LookInfo[] Items { get; private set; } = new LookInfo[LookCatalog.SlotCount];

        /// <summary>The items put together; null for the crew's own look.</summary>
        public LookInfo Look { get; private set; }

        /// <summary>Put <paramref name="items"/> (by slot) on a player (Player_1 or Player_2).</summary>
        public static void Dress(GameObject player, LookInfo[] items)
        {
            if (!player.TryGetComponent(out LookSkin skin))
            {
                if (LookCatalog.Combine(items) == null)
                    return;
                skin = player.AddComponent<LookSkin>();
            }
            skin.Wear(items);
        }

        public void Wear(LookInfo[] items)
        {
            LookInfo look = LookCatalog.Combine(items);
            if (look?.Id == Look?.Id)
                return;
            Items = items;
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

        /// <summary>The sprite as <paramref name="look"/> dresses it, for Player 1's sheets or Player 2's.</summary>
        public static Sprite Recoloured(Sprite sprite, LookInfo look, bool player2)
        {
            if (look == null || sprite == null || sprite.packed)
                return sprite;
            if (Frames.TryGetValue((look.Id, player2, sprite), out Sprite copy))
                return copy;
            Texture2D sheet = Recoloured(sprite.texture, look, player2);
            copy = sheet == sprite.texture ? sprite
                : Sprite.Create(sheet, sprite.rect, sprite.pivot / sprite.rect.size, sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect, sprite.border);
            copy.name = sprite.name;
            Frames[(look.Id, player2, sprite)] = copy;
            Frames[(look.Id, player2, copy)] = copy; // already dressed
            return copy;
        }

        /// <summary>The whole sheet as <paramref name="look"/> dresses it.</summary>
        public static Texture2D Recoloured(Texture2D source, LookInfo look, bool player2)
        {
            if (look == null)
                return source;
            if (Sheets.TryGetValue((look.Id, player2, source), out Texture2D sheet))
                return sheet;
            sheet = source;
            if (source.isReadable)
                sheet = Texture(source.name + " " + look.Id, Dress(source, look, player2, new RectInt(0, 0, source.width, source.height)),
                    source.width, source.height, source);
            else
                Debug.LogWarning($"[Looks] {source.name} isn't readable: tick Read/Write in its import settings so looks can recolour it.");
            Sheets[(look.Id, player2, source)] = sheet;
            return sheet;
        }

        /// <summary>The square of a sheet a sprite stands in (its idle frame, for the shop), dressed; the
        /// caller destroys it. Null if the sheet isn't readable.</summary>
        public static Texture2D Portrait(Sprite sprite, LookInfo look, bool player2)
        {
            Texture2D source = sprite.texture;
            if (!source.isReadable)
                return null;
            int size = (int)sprite.rect.height;
            var square = new RectInt((int)sprite.rect.x, (int)sprite.rect.y, size, size); // the player is in the frame's left square (the pivot)
            return Texture(sprite.name, look != null ? Dress(source, look, player2, square) : Region(Pixels(source), source.width, square), size, size, source);
        }

        private static Texture2D Texture(string name, Color32[] pixels, int width, int height, Texture2D like)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = like.filterMode,
                wrapMode = like.wrapMode,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static readonly Dictionary<Texture2D, Color32[]> SourcePixels = new Dictionary<Texture2D, Color32[]>();
        private static readonly Dictionary<(bool, string), Color32[]> OverlayKeys = new Dictionary<(bool, string), Color32[]>();

        private static Color32[] Pixels(Texture2D source)
        {
            if (!SourcePixels.TryGetValue(source, out Color32[] pixels))
                SourcePixels[source] = pixels = source.GetPixels32();
            return pixels;
        }

        private static Color32[] Region(Color32[] pixels, int width, RectInt r)
        {
            var region = new Color32[r.width * r.height];
            for (int y = 0; y < r.height; y++)
                Array.Copy(pixels, (r.y + y) * width + r.x, region, y * r.width, r.width);
            return region;
        }

        // The sheet's overlay (keys per pixel, as the sheet counts them), or null.
        private static Color32[] Keys(Texture2D source, bool player2)
        {
            if (OverlayKeys.TryGetValue((player2, source.name), out Color32[] keys))
                return keys;
            Texture2D overlay = Resources.Load<Texture2D>($"LookOverlays/{(player2 ? "P2" : "P1")}/{source.name}");
            keys = overlay != null && overlay.isReadable && overlay.width == source.width && overlay.height == source.height
                ? overlay.GetPixels32() : null;
            if (overlay != null)
                Resources.UnloadAsset(overlay);
            return OverlayKeys[(player2, source.name)] = keys;
        }

        // The look's pixels for a region of a sheet (rows from the bottom, as textures count them).
        private static Color32[] Dress(Texture2D source, LookInfo look, bool player2, RectInt r)
        {
            Dictionary<int, int> map = LookPalette.Map(look, player2);
            Dictionary<int, int> extras = LookPalette.Extras(look);
            Color32[] all = Pixels(source), keys = Keys(source, player2);
            if (keys == null && (extras.Count > 0 || look.Head != null))
                Debug.LogWarning($"[Looks] no readable overlay of {source.name}'s size for {look.Id}: run tools/artpipe/accessories.py");
            Color32[] pixels = Region(all, source.width, r);
            for (int y = 0; y < r.height; y++)
                for (int x = 0; x < r.width; x++)
                {
                    int i = y * r.width + x, k = (r.y + y) * source.width + r.x + x;
                    Color32 p = pixels[i];
                    int key = keys != null && keys[k].a != 0 ? keys[k].r << 16 | keys[k].g << 8 | keys[k].b : -1;
                    bool sign = key == LookPalette.SignKey || key == LookPalette.SignKeepKey;
                    if (sign && look.Head != null)
                    {
                        pixels[i] = default; // another sign goes on instead
                        continue;
                    }
                    if (p.a == 0 || key == LookPalette.KeepKey || key == LookPalette.SignKeepKey)
                        continue;
                    if (extras.TryGetValue(key, out int to) || map.TryGetValue(p.r << 16 | p.g << 8 | p.b, out to))
                        pixels[i] = new Color32((byte)(to >> 16), (byte)(to >> 8), (byte)to, p.a);
                }
            if (look.Head != null)
                PutOn(look.Head, player2, source, r, pixels);
            return pixels;
        }

        [Serializable]
        private sealed class HeadIndex
        {
            public HeadEntry[] heads;
        }

        [Serializable]
        private sealed class HeadEntry
        {
            public string id;
            public HeadPlace[] places;
        }

        /// <summary>A turn of a sign in its atlas (sx, sy, w, h) and where it goes in a sheet (dx, dy), top-left pixels.</summary>
        [Serializable]
        private sealed class HeadPlace
        {
            public bool p2;
            public string tex;
            public int sx, sy, w, h, dx, dy;
        }

        private static Dictionary<string, HeadPlace[]> _places;
        private static readonly Dictionary<string, (Color32[] pixels, int width, int height)> Atlases = new Dictionary<string, (Color32[], int, int)>();

        // The sign on every clear pixel of its places in this sheet: behind whatever was in front of the old one.
        private static void PutOn(string head, bool player2, Texture2D source, RectInt r, Color32[] pixels)
        {
            if (_places == null)
            {
                _places = new Dictionary<string, HeadPlace[]>();
                TextAsset index = Resources.Load<TextAsset>("LookHeads");
                foreach (HeadEntry entry in index != null ? JsonUtility.FromJson<HeadIndex>(index.text).heads : new HeadEntry[0])
                    _places[entry.id] = entry.places;
            }
            if (!Atlases.TryGetValue(head, out var atlas))
            {
                Texture2D tex = Resources.Load<Texture2D>("LookHeads/" + head);
                atlas = tex != null && tex.isReadable ? (tex.GetPixels32(), tex.width, tex.height) : (null, 0, 0);
                if (tex != null)
                    Resources.UnloadAsset(tex);
                Atlases[head] = atlas;
            }
            if (atlas.pixels == null || !_places.TryGetValue(head, out HeadPlace[] places))
            {
                Debug.LogWarning($"[Looks] no readable sign '{head}': run tools/artpipe/heads.py");
                return;
            }
            foreach (HeadPlace place in places)
            {
                if (place.p2 != player2 || place.tex != source.name)
                    continue;
                for (int j = 0; j < place.h; j++)
                    for (int i = 0; i < place.w; i++)
                    {
                        // Textures count rows from the bottom; the index from the top.
                        Color32 c = atlas.pixels[(atlas.height - 1 - (place.sy + j)) * atlas.width + place.sx + i];
                        int x = place.dx + i - r.x, y = source.height - 1 - (place.dy + j) - r.y;
                        if (c.a == 0 || x < 0 || x >= r.width || y < 0 || y >= r.height)
                            continue;
                        int at = y * r.width + x;
                        if (pixels[at].a == 0)
                            pixels[at] = c;
                    }
            }
        }
    }
}
