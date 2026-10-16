namespace Demolition
{
    /// <summary>
    /// Score earned by destroying blocks (+10 per block, same reward the old
    /// dstrBlock gave). Lives in the Demolition assembly so BuildingBlock can award
    /// points; the legacy UI layer (GlobalVariables / ScoreManager / PointsScript in
    /// Assembly-CSharp) reads and resets it through <see cref="Value"/>.
    /// </summary>
    public static class Score
    {
        public static int Value;

        public static void Add(int amount) => Value += amount;
    }
}
