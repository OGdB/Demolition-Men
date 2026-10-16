namespace Demolition.Run
{
    /// <summary>Choices made in the menus that outlive scene loads.</summary>
    public static class Session
    {
        /// <summary>Players taking part. 0 = not chosen (play mode started straight in a
        /// level): every player in the scene joins.</summary>
        public static int PlayerCount;
    }
}
