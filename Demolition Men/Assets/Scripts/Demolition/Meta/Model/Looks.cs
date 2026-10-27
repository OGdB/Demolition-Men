using System.Collections.Generic;

namespace Demolition.Meta
{
    /// <summary>How a look is had: bought with cash, earned with full job sheets, or the supporter's
    /// (it comes with REMOVE ADS).</summary>
    public enum LookSource { Shop, JobSheets, Supporter }

    /// <summary>A look for the crew: looks only, never stronger (roadmap v1.2, Cosmetics shop).</summary>
    public sealed class LookInfo
    {
        /// <summary>Save id, and the name its sprites go by.</summary>
        public string Id;
        public string Name;
        public LookSource Source;
        /// <summary>Cash, for <see cref="LookSource.Shop"/>.</summary>
        public int Price;
        /// <summary>Full job sheets it takes, for <see cref="LookSource.JobSheets"/>.</summary>
        public int Sheets;
    }

    /// <summary>The looks, who owns which and what each player wears.</summary>
    public static class LookCatalog
    {
        /// <summary>None yet: they come through the art pipeline, generated and reviewed like the action frames.</summary>
        public static readonly IReadOnlyList<LookInfo> All = new LookInfo[0];

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
