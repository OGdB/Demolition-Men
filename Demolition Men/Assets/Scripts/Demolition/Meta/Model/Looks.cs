using System;
using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Meta
{
    /// <summary>How a look is had: bought with cash, earned with full job sheets, or the supporter's
    /// (it comes with REMOVE ADS).</summary>
    public enum LookSource { Shop, JobSheets, Supporter }

    /// <summary>A look for the crew: looks only, never stronger (roadmap v1.2, Cosmetics shop). It
    /// recolours the trousers, shoes and sign (<see cref="LookPalette"/>).</summary>
    public sealed class LookInfo
    {
        /// <summary>Save id.</summary>
        public string Id;
        public string Name;
        public LookSource Source;
        /// <summary>Cash, for <see cref="LookSource.Shop"/>.</summary>
        public int Price;
        /// <summary>Full job sheets it takes, for <see cref="LookSource.JobSheets"/>.</summary>
        public int Sheets;
        /// <summary>The colours it puts on, 0xRRGGBB; <see cref="Keep"/> leaves that part as the crew wears it.</summary>
        public int Trousers = Keep, Shoes = Keep, Sign = Keep, SignShade = Keep;

        public const int Keep = -1;
    }

    /// <summary>What a look recolours on the player sheets and action frames: each part's colours
    /// as the art draws them (Player 1's warning sign, Player 2's stop sign). Skin and weapons keep theirs.</summary>
    public static class LookPalette
    {
        private static readonly int[] P1Trousers = { 0x222034 }, P1Shoes = { 0x663931 };
        private static readonly int[] P1Sign = { 0xFFE08B, 0xFFECA4 }, P1SignShade = { 0xFFC055 }; // the second: the action frames' highlight
        private static readonly int[] P2Trousers = { 0xE1BB7A }, P2Shoes = { 0x663931 };
        private static readonly int[] P2Sign = { 0xF64949 }, P2SignShade = { 0xBF0000 };

        /// <summary>Source colour to the look's, 0xRRGGBB; empty for none.</summary>
        public static Dictionary<int, int> Map(LookInfo look, bool player2)
        {
            var map = new Dictionary<int, int>();
            if (look == null)
                return map;
            Add(map, player2 ? P2Trousers : P1Trousers, look.Trousers, false);
            Add(map, player2 ? P2Shoes : P1Shoes, look.Shoes, false);
            Add(map, player2 ? P2Sign : P1Sign, look.Sign, true);
            Add(map, player2 ? P2SignShade : P1SignShade, look.SignShade, false);
            return map;
        }

        private static void Add(Dictionary<int, int> map, int[] from, int to, bool highlight)
        {
            if (to == LookInfo.Keep)
                return;
            for (int i = 0; i < from.Length; i++)
                map[from[i]] = highlight && i > 0 ? Lighten(to, 0.35f) : to;
        }

        private static int Lighten(int rgb, float t)
        {
            int result = 0;
            for (int shift = 16; shift >= 0; shift -= 8)
            {
                int c = (rgb >> shift) & 0xFF;
                result |= (int)Math.Round(c + (255 - c) * t) << shift;
            }
            return result;
        }
    }

    /// <summary>The looks, who owns which and what each player wears.</summary>
    public static class LookCatalog
    {
        /// <summary>The looks, from <c>Resources/Looks.json</c> (<see cref="Load"/>, at start).</summary>
        public static IReadOnlyList<LookInfo> All { get; private set; } = new LookInfo[0];

        [Serializable]
        private sealed class LookFile
        {
            public LookEntry[] looks;
        }

        [Serializable]
        private sealed class LookEntry
        {
            public string id, name, source, trousers, shoes, sign, signShade;
            public int price, sheets;
        }

        /// <summary>Read the catalog from its JSON (colours as "RRGGBB", empty to keep the crew's).</summary>
        public static void Load(string json)
        {
            var looks = new List<LookInfo>();
            LookFile file = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<LookFile>(json);
            foreach (LookEntry e in file?.looks ?? new LookEntry[0])
            {
                if (string.IsNullOrEmpty(e.id) || !Enum.TryParse(e.source, out LookSource source))
                    throw new FormatException($"Looks.json: look '{e.id}' has no id or an unknown source '{e.source}'");
                looks.Add(new LookInfo
                {
                    Id = e.id, Name = e.name, Source = source, Price = e.price, Sheets = e.sheets,
                    Trousers = Colour(e.trousers), Shoes = Colour(e.shoes), Sign = Colour(e.sign), SignShade = Colour(e.signShade),
                });
            }
            All = looks;
        }

        private static int Colour(string hex) =>
            string.IsNullOrEmpty(hex) ? LookInfo.Keep : Convert.ToInt32(hex, 16) & 0xFFFFFF;

        /// <summary>Its place in <see cref="All"/> (online, a look is sent as this), or -1.</summary>
        public static int IndexOf(LookInfo look)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i] == look)
                    return i;
            return -1;
        }

        public static LookInfo Find(string id)
        {
            foreach (LookInfo look in All)
                if (look.Id == id)
                    return look;
            return null;
        }

        public static bool Owns(SaveData save, LookInfo look)
        {
            switch (look.Source)
            {
                case LookSource.Shop: return save.looks != null && save.looks.Contains(look.Id);
                case LookSource.JobSheets: return save.FullSheets() >= look.Sheets;
                default: return save.adsRemoved;
            }
        }

        /// <summary>Buy a shop look with cash. False for any other, one owned already, or too expensive.</summary>
        public static bool TryBuy(SaveData save, LookInfo look)
        {
            if (look.Source != LookSource.Shop || Owns(save, look) || save.cash < look.Price)
                return false;
            save.cash -= look.Price;
            save.looks ??= new List<string>(); // a profile saved before looks
            save.looks.Add(look.Id);
            return true;
        }

        /// <summary>What player <paramref name="player"/> (0 = P1) wears, if owned; null for the crew's own look.</summary>
        public static LookInfo Worn(SaveData save, int player, IReadOnlyList<LookInfo> catalog = null)
        {
            if (save.wearing == null || player < 0 || player >= save.wearing.Count)
                return null;
            foreach (LookInfo look in catalog ?? All)
                if (look.Id == save.wearing[player])
                    return Owns(save, look) ? look : null;
            return null;
        }

        /// <summary>Put a look on player <paramref name="player"/>; null for the crew's own. False if not owned.</summary>
        public static bool Wear(SaveData save, int player, LookInfo look)
        {
            if (player < 0 || (look != null && !Owns(save, look)))
                return false;
            save.wearing ??= new List<string>();
            while (save.wearing.Count <= player)
                save.wearing.Add(string.Empty);
            save.wearing[player] = look != null ? look.Id : string.Empty;
            return true;
        }
    }
}
