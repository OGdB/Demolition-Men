using System;
using System.Collections.Generic;
using UnityEngine;

namespace Demolition.Meta
{
    /// <summary>How a look is had: bought with cash, earned with full job sheets, or the supporter's
    /// (it comes with REMOVE ADS).</summary>
    public enum LookSource { Shop, JobSheets, Supporter }

    /// <summary>What part of the crew a look item dresses; a player wears one item per slot, or none.</summary>
    public enum LookSlot { Outfit, Sign, Chain, Bracelets, Tattoo }

    /// <summary>A look item for the crew: looks only, never stronger (roadmap v1.2, Cosmetics shop).
    /// By its slot it recolours the trousers and shoes, recolours the sign or puts another one on
    /// (<see cref="Head"/>), or adds a chain, bracelets or tattoos (<see cref="LookPalette"/>);
    /// what a player wears is their items put together (<see cref="LookCatalog.Combine"/>).</summary>
    public sealed class LookInfo
    {
        /// <summary>Save id.</summary>
        public string Id;
        public string Name;
        public LookSlot Slot;
        public LookSource Source;
        /// <summary>Cash, for <see cref="LookSource.Shop"/>.</summary>
        public int Price;
        /// <summary>Full job sheets it takes, for <see cref="LookSource.JobSheets"/>.</summary>
        public int Sheets;
        /// <summary>The colours it puts on, 0xRRGGBB; <see cref="Keep"/> leaves that part as the crew wears it.</summary>
        public int Trousers = Keep, Shoes = Keep, Sign = Keep, SignShade = Keep;
        /// <summary>What it adds, in these colours (0xRRGGBB); <see cref="Keep"/> for none.</summary>
        public int Chain = Keep, Bracelets = Keep, ChestTattoo = Keep, ArmTattoo = Keep;
        /// <summary>Another sign for a head (Resources/LookHeads, tools/artpipe/heads.py); null keeps the player's own.</summary>
        public string Head;

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

        // The overlays' key colours (tools/artpipe/accessories.py KEYS): what each pixel is part of.
        public const int ChainKey = 0xFF0000, ChainShadeKey = 0x800000, BraceletKey = 0x00FF00, BraceletShadeKey = 0x008000;
        public const int ChestTattooKey = 0x0000FF, ArmTattooKey = 0xFF00FF;
        /// <summary>What no look recolours though it's drawn in a look's colour (the radio, the skin's highlights).</summary>
        public const int KeepKey = 0x010101;
        /// <summary>The player's own sign, which another sign (<see cref="LookInfo.Head"/>) clears; and
        /// its part drawn in a look's colour, which nothing recolours either.</summary>
        public const int SignKey = 0x020202, SignKeepKey = 0x030303;

        /// <summary>Overlay key colour to what the look puts there (Resources/LookOverlays, drawn by
        /// tools/artpipe/accessories.py); a part it doesn't wear isn't in it. Metal gets a darker shade.</summary>
        public static Dictionary<int, int> Extras(LookInfo look)
        {
            var map = new Dictionary<int, int>();
            if (look == null)
                return map;
            if (look.Chain != LookInfo.Keep)
            {
                map[ChainKey] = look.Chain;
                map[ChainShadeKey] = Darken(look.Chain, 0.3f);
            }
            if (look.Bracelets != LookInfo.Keep)
            {
                map[BraceletKey] = look.Bracelets;
                map[BraceletShadeKey] = Darken(look.Bracelets, 0.3f);
            }
            if (look.ChestTattoo != LookInfo.Keep)
                map[ChestTattooKey] = look.ChestTattoo;
            if (look.ArmTattoo != LookInfo.Keep)
                map[ArmTattooKey] = look.ArmTattoo;
            return map;
        }

        private static void Add(Dictionary<int, int> map, int[] from, int to, bool highlight)
        {
            if (to == LookInfo.Keep)
                return;
            for (int i = 0; i < from.Length; i++)
                map[from[i]] = highlight && i > 0 ? Lighten(to, 0.35f) : to;
        }

        private static int Lighten(int rgb, float t) => Mix(rgb, 255, t);

        private static int Darken(int rgb, float t) => Mix(rgb, 0, t);

        private static int Mix(int rgb, int towards, float t)
        {
            int result = 0;
            for (int shift = 16; shift >= 0; shift -= 8)
            {
                int c = (rgb >> shift) & 0xFF;
                result |= (int)Math.Round(c + (towards - c) * t) << shift;
            }
            return result;
        }
    }

    /// <summary>The look items, who owns which and what each player wears.</summary>
    public static class LookCatalog
    {
        public const int SlotCount = 5;
        private const int SlotBits = 5; // online a player's items go as one int: 5 slots, up to 31 items each

        /// <summary>The items, from <c>Resources/Looks.json</c> (<see cref="Load"/>, at start).</summary>
        public static IReadOnlyList<LookInfo> All { get; private set; } = new LookInfo[0];

        [Serializable]
        private sealed class LookFile
        {
            public LookEntry[] looks;
        }

        [Serializable]
        private sealed class LookEntry
        {
            public string id, name, slot, source, trousers, shoes, sign, signShade, head, chain, bracelets, chestTattoo, armTattoo;
            public int price, sheets;
        }

        /// <summary>Read the catalog from its JSON (colours as "RRGGBB", empty to keep the crew's or wear none).</summary>
        public static void Load(string json)
        {
            var looks = new List<LookInfo>();
            LookFile file = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<LookFile>(json);
            foreach (LookEntry e in file?.looks ?? new LookEntry[0])
            {
                if (string.IsNullOrEmpty(e.id) || !Enum.TryParse(e.source, out LookSource source) || !Enum.TryParse(e.slot, out LookSlot slot))
                    throw new FormatException($"Looks.json: look '{e.id}' has no id, or an unknown source '{e.source}' or slot '{e.slot}'");
                looks.Add(new LookInfo
                {
                    Id = e.id, Name = e.name, Slot = slot, Source = source, Price = e.price, Sheets = e.sheets,
                    Trousers = Colour(e.trousers), Shoes = Colour(e.shoes), Sign = Colour(e.sign), SignShade = Colour(e.signShade),
                    Chain = Colour(e.chain), Bracelets = Colour(e.bracelets), ChestTattoo = Colour(e.chestTattoo), ArmTattoo = Colour(e.armTattoo),
                    Head = string.IsNullOrEmpty(e.head) ? null : e.head,
                });
            }
            All = looks;
        }

        private static int Colour(string hex) =>
            string.IsNullOrEmpty(hex) ? LookInfo.Keep : Convert.ToInt32(hex, 16) & 0xFFFFFF;

        /// <summary>The items for one slot, in catalog order.</summary>
        public static List<LookInfo> InSlot(LookSlot slot, IReadOnlyList<LookInfo> catalog = null)
        {
            var list = new List<LookInfo>();
            foreach (LookInfo look in catalog ?? All)
                if (look.Slot == slot)
                    list.Add(look);
            return list;
        }

        public static LookInfo Find(string id, IReadOnlyList<LookInfo> catalog = null)
        {
            foreach (LookInfo look in catalog ?? All)
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

        /// <summary>Buy a shop item with cash. False for any other, one owned already, or too expensive.</summary>
        public static bool TryBuy(SaveData save, LookInfo look)
        {
            if (look.Source != LookSource.Shop || Owns(save, look) || save.cash < look.Price)
                return false;
            save.cash -= look.Price;
            save.looks ??= new List<string>(); // a profile saved before looks
            save.looks.Add(look.Id);
            return true;
        }

        /// <summary>What player <paramref name="player"/> (0 = P1) wears in a slot, if owned; null for the crew's own.</summary>
        public static LookInfo Worn(SaveData save, int player, LookSlot slot, IReadOnlyList<LookInfo> catalog = null)
        {
            if (save.dressed == null || player < 0 || player >= save.dressed.Count || save.dressed[player]?.items == null)
                return null;
            foreach (string id in save.dressed[player].items)
            {
                LookInfo look = Find(id, catalog);
                if (look != null && look.Slot == slot)
                    return Owns(save, look) ? look : null;
            }
            return null;
        }

        /// <summary>Everything player <paramref name="player"/> wears, by slot (null where nothing is).</summary>
        public static LookInfo[] Wearing(SaveData save, int player, IReadOnlyList<LookInfo> catalog = null)
        {
            var items = new LookInfo[SlotCount];
            for (int slot = 0; slot < SlotCount; slot++)
                items[slot] = Worn(save, player, (LookSlot)slot, catalog);
            return items;
        }

        /// <summary>Put an owned item on player <paramref name="player"/>, in place of what they wore in its slot.</summary>
        public static bool Wear(SaveData save, int player, LookInfo look, IReadOnlyList<LookInfo> catalog = null)
        {
            if (player < 0 || look == null || !Owns(save, look))
                return false;
            TakeOff(save, player, look.Slot, catalog);
            save.dressed[player].items.Add(look.Id);
            return true;
        }

        /// <summary>Back to the crew's own in one slot.</summary>
        public static void TakeOff(SaveData save, int player, LookSlot slot, IReadOnlyList<LookInfo> catalog = null)
        {
            if (player < 0)
                return;
            save.dressed ??= new List<PlayerLooks>();
            while (save.dressed.Count <= player)
                save.dressed.Add(new PlayerLooks());
            save.dressed[player] ??= new PlayerLooks();
            save.dressed[player].items ??= new List<string>();
            save.dressed[player].items.RemoveAll(id => Find(id, catalog) is LookInfo look ? look.Slot == slot : true); // and ids gone from the catalog
        }

        /// <summary>A player's items put together as one look (each item sets only its slot's parts); null for none.</summary>
        public static LookInfo Combine(IReadOnlyList<LookInfo> items)
        {
            LookInfo all = null;
            foreach (LookInfo look in items)
            {
                if (look == null)
                    continue;
                all ??= new LookInfo { Id = string.Empty };
                all.Id = all.Id.Length == 0 ? look.Id : all.Id + "+" + look.Id;
                Take(ref all.Trousers, look.Trousers);
                Take(ref all.Shoes, look.Shoes);
                Take(ref all.Sign, look.Sign);
                Take(ref all.SignShade, look.SignShade);
                Take(ref all.Chain, look.Chain);
                Take(ref all.Bracelets, look.Bracelets);
                Take(ref all.ChestTattoo, look.ChestTattoo);
                Take(ref all.ArmTattoo, look.ArmTattoo);
                all.Head ??= look.Head;
            }
            return all;
        }

        private static void Take(ref int into, int colour)
        {
            if (colour != LookInfo.Keep)
                into = colour;
        }

        /// <summary>A player's items by slot as one int, for <c>NetPlayer</c>: per slot its place in <see cref="InSlot"/> + 1.</summary>
        public static int Pack(IReadOnlyList<LookInfo> items, IReadOnlyList<LookInfo> catalog = null)
        {
            int packed = 0;
            for (int slot = 0; slot < SlotCount; slot++)
            {
                LookInfo look = slot < items.Count ? items[slot] : null;
                int index = look != null ? InSlot((LookSlot)slot, catalog).IndexOf(look) + 1 : 0;
                packed |= (index & ((1 << SlotBits) - 1)) << (slot * SlotBits);
            }
            return packed;
        }

        public static LookInfo[] Unpack(int packed, IReadOnlyList<LookInfo> catalog = null)
        {
            var items = new LookInfo[SlotCount];
            for (int slot = 0; slot < SlotCount; slot++)
            {
                int index = (packed >> (slot * SlotBits)) & ((1 << SlotBits) - 1);
                List<LookInfo> list = InSlot((LookSlot)slot, catalog);
                items[slot] = index > 0 && index <= list.Count ? list[index - 1] : null;
            }
            return items;
        }
    }
}
