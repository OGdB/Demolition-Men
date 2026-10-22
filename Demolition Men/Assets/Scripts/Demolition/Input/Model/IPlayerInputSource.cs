namespace Demolition
{
    /// <summary>Anything that can drive a player: devices, touch controls, a bot.</summary>
    public interface IPlayerInputSource
    {
        /// <summary>Sample this frame's input. Called once per frame by one reader.</summary>
        PlayerInputFrame Read();

        /// <summary>Glyphs for prompts, following whatever was used last.</summary>
        InputStyle Style { get; }
    }

    /// <summary>Two sources driving one player (keyboard + touch when a level is started directly).</summary>
    public sealed class CombinedInputSource : IPlayerInputSource
    {
        private readonly IPlayerInputSource _a;
        private readonly IPlayerInputSource _b;
        private PlayerInputFrame _lastA, _lastB;
        private InputStyle _style;

        public CombinedInputSource(IPlayerInputSource a, IPlayerInputSource b)
        {
            _a = a;
            _b = b;
            _style = a.Style;
        }

        public InputStyle Style => _style;

        public PlayerInputFrame Read()
        {
            PlayerInputFrame a = _a.Read(), b = _b.Read();
            if (b.IsActive && !_lastB.IsActive) _style = _b.Style;
            else if (a.IsActive && !_lastA.IsActive) _style = _a.Style;
            _lastA = a;
            _lastB = b;
            return PlayerInputFrame.Merge(a, b);
        }
    }
}
