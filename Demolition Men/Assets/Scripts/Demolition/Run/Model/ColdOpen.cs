using Demolition.Meta;

namespace Demolition.Run
{
    public enum ColdOpenBeat { RunIn, Ask, Swing, Dust, Title, Done }

    /// <summary>
    /// The cold open (docs/hit-plan.md F4): a brand-new profile starts in the game instead of at
    /// the title and the lobby. The crew runs in to the Tutorial's painted block, one hit drops
    /// it, the title slams over the dust, GO. <see cref="Starts"/> says who gets it; an instance
    /// is one opening's timeline. Every beat ends by itself after its limit, so the opening
    /// never waits on a player, a swing or a view that doesn't come.
    /// </summary>
    public sealed class ColdOpen
    {
        /// <summary>Asks for the hit, with the button of the player's device (<see cref="InputGlyphs"/>).</summary>
        public const string Ask = "PRESS {HIT} TO SMASH THE WEAK POINT";
        /// <summary>Shown once the controls are theirs: the opening skips the Tutorial's MOVE &amp; JUMP stretch.</summary>
        public const string Controls = "{MOVE} TO MOVE, {JUMP} TO JUMP";

        /// <summary>Seconds: the crew is at the block by now, or the opening goes on from where they are.</summary>
        public const float RunInLimit = 2.5f;
        /// <summary>Nobody pressed anything for this long: the crew hits by itself.</summary>
        public const float Patience = 4f;
        /// <summary>The swing hasn't dropped the block by now: it drops without one.</summary>
        public const float SwingLimit = 1.2f;
        /// <summary>The building comes down for this long before the title covers it.</summary>
        public const float DustSeconds = 0.5f;
        /// <summary>The title is over by now, even if its view never said so.</summary>
        public const float TitleLimit = 4f;

        private float _time;
        private bool _pressed;

        public ColdOpenBeat Beat { get; private set; }

        /// <summary>A profile with no run behind it and no finished Tutorial.</summary>
        public static bool IsNew(SaveData save, string tutorialId) => save.runsPlayed == 0 && !save.IsFinished(tutorialId);

        /// <summary>At the app's start: the cold open instead of the title and the lobby?</summary>
        /// <param name="enabled">The switch (<c>GameConfig.coldOpen</c>).</param>
        /// <param name="invited">The build was opened by an invite link: its room comes first.</param>
        /// <param name="returning">The menu loaded on the way back from a run, not at the app's start.</param>
        public static bool Starts(SaveData save, string tutorialId, bool enabled, bool invited, bool returning) =>
            enabled && !invited && !returning && IsNew(save, tutorialId);

        /// <summary>One frame.</summary>
        /// <param name="arrived">The crew stands at the block.</param>
        /// <param name="pressed">The player pressed something this frame.</param>
        /// <param name="broken">The painted block is gone. From <see cref="ColdOpenBeat.Dust"/> on it has to be.</param>
        public void Tick(float deltaTime, bool arrived, bool pressed, bool broken)
        {
            _time += deltaTime;
            switch (Beat)
            {
                case ColdOpenBeat.RunIn:
                    _pressed |= pressed; // pressed on the way in: the hit follows as they arrive
                    if (arrived || _time >= RunInLimit)
                        Enter(ColdOpenBeat.Ask);
                    break;
                case ColdOpenBeat.Ask:
                    if (_pressed || pressed || _time >= Patience)
                        Enter(ColdOpenBeat.Swing);
                    break;
                case ColdOpenBeat.Swing:
                    if (broken || _time >= SwingLimit)
                        Enter(ColdOpenBeat.Dust);
                    break;
                case ColdOpenBeat.Dust:
                    if (pressed || _time >= DustSeconds) // a press skips the wait, as it skips the title
                        Enter(ColdOpenBeat.Title);
                    break;
                case ColdOpenBeat.Title:
                    if (_time >= TitleLimit)
                        Enter(ColdOpenBeat.Done);
                    break;
            }
        }

        /// <summary>The title's view has played, or a press skipped it.</summary>
        public void TitleDone()
        {
            if (Beat == ColdOpenBeat.Title)
                Enter(ColdOpenBeat.Done);
        }

        private void Enter(ColdOpenBeat beat)
        {
            Beat = beat;
            _time = 0f;
        }
    }
}
