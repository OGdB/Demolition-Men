using UnityEngine;

namespace Demolition.Meta
{
    /// <summary>Score → cash, and what upgrades cost and do.</summary>
    public static class Economy
    {
        /// <summary>Score points per dollar: about one upgrade per level. The campaign holds about 104k
        /// points of buildings; chains and the speed bonus take the best play to about 4.5x that
        /// (a 40k level paid 5k at 10 points per dollar, playtest 2026-10-27), which at 25 pays
        /// about a quarter of the shop (upgrades and actions, 92k) per run through the campaign,
        /// so the shop lasts into replays, job sheets and dailies.</summary>
        public const int PointsPerCash = 25;
        /// <summary>Finishing pays this much more than failing with the same score.</summary>
        public const float FinishBonus = 1.25f;
        /// <summary>Paid once, on finishing the Tutorial for the first time: about four first upgrade
        /// levels, for the player to pick, so the shop means something before the first job.</summary>
        public const int StartingCash = 2500;

        public static int CashFor(int score, bool finished) =>
            Mathf.Max(0, Mathf.RoundToInt(score / (float)PointsPerCash * (finished ? FinishBonus : 1f)));
    }

    public enum UpgradeStat { Power, Health, Speed, Dynamite, ToolGrip }

    /// <summary>One upgrade track: cost of each next level and the stat value at each level.</summary>
    public sealed class UpgradeDefinition
    {
        public readonly string Id;
        public readonly UpgradeStat Stat;
        public readonly string Name;
        public readonly string Description;
        /// <summary>Cost to buy level i + 1.</summary>
        public readonly int[] Costs;
        /// <summary>Stat value at level i (index 0 = not bought).</summary>
        public readonly float[] Values;
        /// <summary>How a value reads in the shop, e.g. "x{0:0.00}".</summary>
        public readonly string Format;

        public UpgradeDefinition(string id, UpgradeStat stat, string name, string description, int[] costs, float[] values, string format)
        {
            Id = id;
            Stat = stat;
            Name = name;
            Description = description;
            Costs = costs;
            Values = values;
            Format = format;
        }

        public int MaxLevel => Costs.Length;
        public float ValueAt(int level) => Values[Mathf.Clamp(level, 0, Values.Length - 1)];
        public int CostOfNext(int level) => level < Costs.Length ? Costs[level] : -1;
    }

    /// <summary>The skill shop's stock (roadmap D6): tuning values the player scripts expose.</summary>
    public static class UpgradeCatalog
    {
        public static readonly UpgradeDefinition[] All =
        {
            new UpgradeDefinition("power", UpgradeStat.Power, "POWER", "Every hit does more damage",
                new[] { 600, 1500, 3500, 7500 }, new[] { 1f, 1.15f, 1.3f, 1.5f, 1.75f }, "x{0:0.00}"),
            new UpgradeDefinition("health", UpgradeStat.Health, "HARD HAT", "More health against falling debris",
                new[] { 500, 1200, 3000, 6500 }, new[] { 100f, 125f, 150f, 180f, 220f }, "{0:0} HP"),
            new UpgradeDefinition("speed", UpgradeStat.Speed, "BOOTS", "Run faster",
                new[] { 500, 1200, 3000, 6500 }, new[] { 8f, 8.5f, 9f, 9.5f, 10f }, "{0:0.0} m/s"),
            new UpgradeDefinition("dynamite", UpgradeStat.Dynamite, "TNT BELT", "Every dynamite pickup gives extra sticks",
                new[] { 800, 2000, 4500, 9000 }, new[] { 0f, 1f, 2f, 3f, 4f }, "+{0:0} TNT"),
            new UpgradeDefinition("grip", UpgradeStat.ToolGrip, "TOOL GRIP", "Picked-up weapons last longer",
                new[] { 700, 1800, 4000, 8000 }, new[] { 1f, 1.2f, 1.4f, 1.7f, 2f }, "x{0:0.0}"),
        };

        public static UpgradeDefinition Find(string id)
        {
            foreach (UpgradeDefinition upgrade in All)
                if (upgrade.Id == id)
                    return upgrade;
            return null;
        }

        public static UpgradeDefinition Find(UpgradeStat stat)
        {
            foreach (UpgradeDefinition upgrade in All)
                if (upgrade.Stat == stat)
                    return upgrade;
            return null;
        }

        /// <summary>The stat at the profile's current level.</summary>
        public static float Value(SaveData save, UpgradeStat stat)
        {
            UpgradeDefinition upgrade = Find(stat);
            return upgrade.ValueAt(save.UpgradeLevel(upgrade.Id));
        }

        /// <summary>Buy the next level if affordable. False when maxed or too expensive.</summary>
        public static bool TryBuy(SaveData save, string id)
        {
            UpgradeDefinition upgrade = Find(id);
            if (upgrade == null)
                return false;
            int level = save.UpgradeLevel(id);
            int cost = upgrade.CostOfNext(level);
            if (cost < 0 || save.cash < cost)
                return false;
            save.cash -= cost;
            save.SetUpgradeLevel(id, level + 1);
            return true;
        }
    }
}
