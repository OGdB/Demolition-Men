namespace Demolition
{
    /// <summary>Default structural tuning, shared by <see cref="DestructibleBuilding"/> and
    /// the building validator so generated buildings are judged by the rules they'll play by.</summary>
    public static class StructureDefaults
    {
        public const int StressScanRange = 6;
        public const float ColumnLoadCapacity = 4f;
        public const float ColumnMomentCapacity = 8f;
        public const float OverloadTolerance = 0.6f;
        public const float CrumbleDamageRate = 150f;
    }
}
