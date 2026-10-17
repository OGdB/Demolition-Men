using Demolition.Run;

namespace Demolition.Meta
{
    /// <summary>Applies the skill shop's upgrades to a player at the start of a run.</summary>
    public static class PlayerUpgrades
    {
        public static void Apply(RunController.Player player, SaveData save)
        {
            player.Combat.DamageMultiplier = UpgradeCatalog.Value(save, UpgradeStat.Power);
            player.Combat.DynamiteCount = (int)UpgradeCatalog.Value(save, UpgradeStat.Dynamite);
            player.Combat.WeaponHitMultiplier = UpgradeCatalog.Value(save, UpgradeStat.ToolGrip);
            player.Health.SetMax(UpgradeCatalog.Value(save, UpgradeStat.Health));
            player.Motor.MaxSpeed = UpgradeCatalog.Value(save, UpgradeStat.Speed);
        }
    }
}
