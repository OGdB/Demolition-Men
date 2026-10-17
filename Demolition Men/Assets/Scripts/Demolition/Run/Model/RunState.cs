using System;

namespace Demolition.Run
{
    public enum RunPhase { Countdown, Playing, Finished, Failed }

    public enum PlayerStatus { Alive, Rejoining, Out }

    /// <summary>The host's run state as an online client takes it (docs/multiplayer.md §2).</summary>
    public struct RunSnapshot
    {
        public RunPhase Phase;
        public float Countdown;
        public float Elapsed;
        public float LastCall;
        public PlayerStatus[] Status;
        public float[] OutOfBounds;
        public float[] Rejoin;
        public bool[] Crossed;
        public bool[] FreeRespawnUsed;
    }

    [Serializable]
    public struct RunRules
    {
        public float countdown;
        public float outOfBoundsGrace;
        public float rejoinDelay;
        public float lastCall;

        public static RunRules Default => new RunRules
        {
            countdown = 3f,
            outOfBoundsGrace = 3f,
            rejoinDelay = 5f,
            lastCall = 5f,
        };
    }

    /// <summary>
    /// The run's rules as a state machine (roadmap M1, decision D1):
    /// 3-2-1 countdown, then play until every living player has crossed the finish
    /// (a "last call" timer starts when the first one crosses) or nobody is alive.
    ///
    /// A player is out when the out-of-bounds grace runs out or their health is
    /// depleted. In co-op the first time out is free: they rejoin next to their partner
    /// after <see cref="RunRules.rejoinDelay"/>. After that they stay out until the
    /// partner collects a Resurrect pickup (<see cref="TryResurrect"/>). Solo, out ends
    /// the run. Players who crossed the finish are safe from out-of-bounds.
    /// </summary>
    public sealed class RunState
    {
        private sealed class Slot
        {
            public PlayerStatus Status = PlayerStatus.Alive;
            public bool InBounds = true;
            public float OutOfBounds = -1f;
            public float Rejoin;
            public bool FreeRespawnUsed;
            public bool Crossed;
        }

        private readonly RunRules _rules;
        private readonly Slot[] _slots;

        public event Action<RunPhase> PhaseChanged;
        /// <summary>Player went out; the bool says whether they will rejoin on their own.</summary>
        public event Action<int, bool> PlayerOut;
        public event Action<int> PlayerRejoined;

        public RunState(int playerCount, RunRules rules)
        {
            _rules = rules;
            _slots = new Slot[Math.Max(1, playerCount)];
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = new Slot();
            Countdown = rules.countdown;
            LastCallRemaining = -1f;
        }

        public RunPhase Phase { get; private set; } = RunPhase.Countdown;
        public float Countdown { get; private set; }
        public float Elapsed { get; private set; }
        /// <summary>Seconds left after the first player crossed the finish, -1 if not started.</summary>
        public float LastCallRemaining { get; private set; }
        public int PlayerCount => _slots.Length;
        public bool IsOver => Phase == RunPhase.Finished || Phase == RunPhase.Failed;

        public PlayerStatus Status(int player) => _slots[player].Status;
        public bool HasFreeRespawn(int player) => PlayerCount > 1 && !_slots[player].FreeRespawnUsed;
        /// <summary>Grace seconds left while out of bounds, -1 while in bounds.</summary>
        public float OutOfBoundsRemaining(int player) => _slots[player].OutOfBounds;
        public float RejoinRemaining(int player) => _slots[player].Rejoin;
        public bool HasCrossed(int player) => _slots[player].Crossed;

        public int AliveCount
        {
            get
            {
                int alive = 0;
                for (int i = 0; i < _slots.Length; i++)
                    if (_slots[i].Status == PlayerStatus.Alive)
                        alive++;
                return alive;
            }
        }

        public void SetInBounds(int player, bool inBounds) => _slots[player].InBounds = inBounds;

        /// <summary>Host: the state an online client mirrors.</summary>
        public RunSnapshot Snapshot()
        {
            int n = _slots.Length;
            var s = new RunSnapshot
            {
                Phase = Phase, Countdown = Countdown, Elapsed = Elapsed, LastCall = LastCallRemaining,
                Status = new PlayerStatus[n], OutOfBounds = new float[n], Rejoin = new float[n],
                Crossed = new bool[n], FreeRespawnUsed = new bool[n],
            };
            for (int i = 0; i < n; i++)
            {
                s.Status[i] = _slots[i].Status;
                s.OutOfBounds[i] = _slots[i].OutOfBounds;
                s.Rejoin[i] = _slots[i].Rejoin;
                s.Crossed[i] = _slots[i].Crossed;
                s.FreeRespawnUsed[i] = _slots[i].FreeRespawnUsed;
            }
            return s;
        }

        /// <summary>Online client: take the host's state instead of ticking the rules, raising
        /// the same events the host's run raised (players first, then the phase).</summary>
        public void ApplySnapshot(RunSnapshot s)
        {
            Countdown = s.Countdown;
            Elapsed = s.Elapsed;
            LastCallRemaining = s.LastCall;
            for (int i = 0; i < _slots.Length && i < s.Status.Length; i++)
            {
                Slot slot = _slots[i];
                PlayerStatus was = slot.Status;
                slot.Status = s.Status[i];
                slot.OutOfBounds = s.OutOfBounds[i];
                slot.Rejoin = s.Rejoin[i];
                slot.Crossed = s.Crossed[i];
                slot.FreeRespawnUsed = s.FreeRespawnUsed[i];
                if (was == PlayerStatus.Alive && slot.Status != PlayerStatus.Alive)
                    PlayerOut?.Invoke(i, slot.Status == PlayerStatus.Rejoining);
                else if (was != PlayerStatus.Alive && slot.Status == PlayerStatus.Alive)
                    PlayerRejoined?.Invoke(i);
            }
            if (s.Phase != Phase)
                SetPhase(s.Phase);
        }

        public void Tick(float dt)
        {
            switch (Phase)
            {
                case RunPhase.Countdown:
                    Countdown -= dt;
                    if (Countdown <= 0f)
                    {
                        Countdown = 0f;
                        SetPhase(RunPhase.Playing);
                    }
                    break;

                case RunPhase.Playing:
                    Elapsed += dt;
                    for (int i = 0; i < _slots.Length && Phase == RunPhase.Playing; i++)
                        TickPlayer(i, dt);
                    if (Phase == RunPhase.Playing && LastCallRemaining >= 0f)
                    {
                        LastCallRemaining = Math.Max(0f, LastCallRemaining - dt);
                        if (LastCallRemaining <= 0f)
                            SetPhase(RunPhase.Finished);
                    }
                    break;
            }
        }

        public void ReportDepleted(int player)
        {
            if (Phase == RunPhase.Playing && _slots[player].Status == PlayerStatus.Alive)
                GoOut(player);
        }

        public void ReportCrossedFinish(int player)
        {
            Slot slot = _slots[player];
            if (Phase != RunPhase.Playing || slot.Status != PlayerStatus.Alive || slot.Crossed)
                return;
            slot.Crossed = true;
            slot.OutOfBounds = -1f;
            if (LastCallRemaining < 0f)
                LastCallRemaining = _rules.lastCall;
            CheckEnd();
        }

        /// <summary>The collector picked up a Resurrect: bring back a partner who is out
        /// (or still waiting to rejoin). False if there is nobody to bring back.</summary>
        public bool TryResurrect(int collector)
        {
            if (Phase != RunPhase.Playing || _slots[collector].Status != PlayerStatus.Alive)
                return false;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (i == collector || _slots[i].Status == PlayerStatus.Alive)
                    continue;
                Rejoin(i);
                return true;
            }
            return false;
        }

        private void TickPlayer(int player, float dt)
        {
            Slot slot = _slots[player];
            switch (slot.Status)
            {
                case PlayerStatus.Alive:
                    if (slot.InBounds || slot.Crossed)
                    {
                        slot.OutOfBounds = -1f;
                        break;
                    }
                    if (slot.OutOfBounds < 0f)
                        slot.OutOfBounds = _rules.outOfBoundsGrace;
                    slot.OutOfBounds -= dt;
                    if (slot.OutOfBounds <= 0f)
                        GoOut(player);
                    break;

                case PlayerStatus.Rejoining:
                    slot.Rejoin -= dt;
                    // A rejoin needs a living partner; with nobody alive the run has already failed.
                    if (slot.Rejoin <= 0f && AliveCount > 0)
                        Rejoin(player);
                    break;
            }
        }

        private void GoOut(int player)
        {
            Slot slot = _slots[player];
            slot.OutOfBounds = -1f;
            bool willRejoin = HasFreeRespawn(player);
            if (willRejoin)
            {
                slot.FreeRespawnUsed = true;
                slot.Status = PlayerStatus.Rejoining;
                slot.Rejoin = _rules.rejoinDelay;
            }
            else
            {
                slot.Status = PlayerStatus.Out;
            }
            PlayerOut?.Invoke(player, willRejoin);
            CheckEnd();
        }

        private void Rejoin(int player)
        {
            Slot slot = _slots[player];
            slot.Status = PlayerStatus.Alive;
            slot.Rejoin = 0f;
            slot.InBounds = true;
            slot.OutOfBounds = -1f;
            slot.Crossed = false;
            PlayerRejoined?.Invoke(player);
        }

        private void CheckEnd()
        {
            if (Phase != RunPhase.Playing)
                return;
            if (AliveCount == 0)
            {
                SetPhase(RunPhase.Failed);
                return;
            }
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Status == PlayerStatus.Alive && !_slots[i].Crossed)
                    return;
            SetPhase(RunPhase.Finished);
        }

        private void SetPhase(RunPhase phase)
        {
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }
}
