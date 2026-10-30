namespace Demolition
{
    /// <summary>Input set from code each frame: bots, the attract mode, tests. Whatever is
    /// in <see cref="Next"/> is read once and its one-frame presses cleared.</summary>
    public sealed class ScriptedInputSource : IPlayerInputSource
    {
        public PlayerInputFrame Next;

        /// <summary>Whose buttons the prompts show: set to the controls this stands in for.</summary>
        public InputStyle Style { get; set; }

        public PlayerInputFrame Read()
        {
            PlayerInputFrame frame = Next;
            Next.JumpPressed = Next.JumpReleased = Next.DynamitePressed = Next.ActionPressed = false;
            Next.WeaponCycle = 0;
            Next.Zoom = 0f;
            return frame;
        }
    }
}
