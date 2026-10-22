namespace Demolition
{
    /// <summary>Input set from code each frame: bots, the attract mode, tests. Whatever is
    /// in <see cref="Next"/> is read once and its one-frame presses cleared.</summary>
    public sealed class ScriptedInputSource : IPlayerInputSource
    {
        public PlayerInputFrame Next;

        public InputStyle Style => InputStyle.Gamepad;

        public PlayerInputFrame Read()
        {
            PlayerInputFrame frame = Next;
            Next.JumpPressed = Next.JumpReleased = Next.DynamitePressed = false;
            Next.WeaponCycle = 0;
            Next.Zoom = 0f;
            return frame;
        }
    }
}
