using System;
using System.Collections.Generic;
using Demolition.Actions;
using Demolition.Levels;
using Demolition.Meta;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Demolition.Run
{
    /// <summary>
    /// Runs one level: owns the <see cref="RunState"/> and <see cref="RunScore"/> models,
    /// feeds them what happens in the scene (player positions, health, destruction
    /// events) and applies their decisions (take a player out, bring one back, start
    /// and stop the camera). Views read this component; they never change the run.
    /// </summary>
    public sealed class RunController : MonoBehaviour
    {
        public sealed class Player
        {
            public int Index;
            public Transform Transform;
            public PlayerHealth Health;
            public PlayerCombat Combat;
            public PlayerMotor Motor;
            public PlayerInputReader Input;
            public ActionUser Action;
        }

        // A player running in during the countdown: the script that runs them, the input it
        // stands in for, and the x they reach at GO.
        private sealed class Runner
        {
            public Player Player;
            public float ToX;
            public readonly ScriptedInputSource Script = new ScriptedInputSource();
            public IPlayerInputSource Own;
        }

        // Players are found by GameObject name (see CLAUDE.md).
        private static readonly string[] PlayerNames = { "Player_1", "Player_2" };

        [SerializeField] private RunRules rules = RunRules.Default;
        [Tooltip("Seconds; finishing faster than this earns the speed bonus.")]
        [SerializeField] private float parTime = 150f;
        [Tooltip("Points a player loses when they use their free rejoin.")]
        [SerializeField] private int rejoinPenalty = 500;
        [Tooltip("Where a rejoining player appears, relative to their partner.")]
        [SerializeField] private Vector2 rejoinOffset = new Vector2(-1.5f, 1f);
        [Tooltip("How far below the bottom of the screen (viewport units) still counts as in bounds.")]
        [SerializeField] private float bottomMargin = 0.1f;
        [SerializeField] private string menuScene = "MainMenu";
        [Tooltip("Scene the results screen's Next button loads; empty hides the button.")]
        [SerializeField] private string nextScene;
        [Tooltip("Progress is saved under this id; empty = the scene name.")]
        [SerializeField] private string levelId;

        private readonly List<Player> _players = new List<Player>();
        private readonly List<Runner> _runners = new List<Runner>();
        private ScrollCamera _camera;
        private int _totalCells;
        private int _plannedCells;
        private int _removedCells;
        private Action _nextLevel;

        /// <summary>The run in progress, if the scene has one.</summary>
        public static RunController Current { get; private set; }

        public RunState State { get; private set; }
        public RunScore Score { get; private set; }
        /// <summary>What the crew did that the job sheet asks about.</summary>
        public RunTally Tally { get; private set; }
        /// <summary>The level's job sheet (<see cref="Objectives"/>); null for none (the Tutorial).</summary>
        public ObjectiveKind[] Sheet { get; set; }
        public IReadOnlyList<Player> Players => _players;
        public float ParTime => parTime;
        public Camera Camera => _camera != null ? _camera.Camera : null;

        /// <summary>Share of every building's cells destroyed or collapsed, 0..1. A streamed
        /// level counts the buildings it hasn't spawned yet too.</summary>
        public float DestroyedFraction
        {
            get
            {
                if (NetRole.IsClient && NetFraction >= 0f)
                    return NetFraction; // the host's count: it has every building's latest state
                int total = Mathf.Max(_totalCells, _plannedCells);
                return total == 0 ? 0f : Mathf.Clamp01((float)_removedCells / total);
            }
        }

        /// <summary>Online client: the host's <see cref="DestroyedFraction"/>, -1 until it arrives.</summary>
        public float NetFraction { get; set; } = -1f;

        /// <summary>Nobody goes out of bounds (the Clip Lab frames shots with its own camera).</summary>
        public bool IgnoreBounds { get; set; }

        public string LevelId => string.IsNullOrEmpty(levelId) ? SceneManager.GetActiveScene().name : levelId;

        /// <summary>What the profile got from this run, once it is over.</summary>
        public RunReward Reward { get; private set; }

        /// <summary>Destruction and score as banked when the run ended; the results show these,
        /// so debris still falling afterwards can't light a star the profile never recorded.</summary>
        public float BankedFraction { get; private set; }
        public int BankedScore { get; private set; }

        public int FinalScore => State.Phase == RunPhase.Finished
            ? Score.FinalTotal(State.Elapsed, parTime)
            : Score.Total;

        /// <summary>Points just awarded: player (-1 for none), points, world position.</summary>
        public event Action<int, int, Vector3> Scored;
        /// <summary>A message for everyone (a player left): message, seconds.</summary>
        public event Action<string, float> PromptShown;
        /// <summary>The run is over and banked in the profile (see <see cref="Reward"/>).</summary>
        public event Action Ended;

        public bool HasNextLevel => _nextLevel != null || !string.IsNullOrEmpty(nextScene);

        /// <summary>Set up by a generated level before the run starts: its save id, par,
        /// every cell it will spawn, and what Next does (null hides it).</summary>
        public void Configure(string id, float par, int plannedCells, Action nextLevel)
        {
            levelId = id;
            parTime = par;
            _plannedCells = plannedCells;
            _nextLevel = nextLevel;
        }

        /// <summary>Seconds of 3-2-1 before GO.</summary>
        public float CountdownTime => rules.countdown;

        /// <summary>A generated level starts on the move: the players stand <paramref name="distance"/>
        /// left of their spot and run in during the countdown, reaching it at GO at full speed
        /// (faster boots wait a moment first). Call before the first frame.</summary>
        public void RunIn(float distance)
        {
            foreach (Player p in _players)
                if (!p.Health.IsProxy) // online the other device runs its own player in
                    _runners.Add(new Runner { Player = p, ToX = p.Transform.position.x + distance });
        }

        private void OnEnable()
        {
            DestructionEvents.BuildingReady += OnBuildingReady;
            DestructionEvents.StandingCellsRemoved += OnStandingCellsRemoved;
            DestructionEvents.BlockDestroyed += OnBlockDestroyed;
            DestructionEvents.Collapsed += OnCollapsed;
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        private void OnDisable()
        {
            DestructionEvents.BuildingReady -= OnBuildingReady;
            DestructionEvents.StandingCellsRemoved -= OnStandingCellsRemoved;
            DestructionEvents.BlockDestroyed -= OnBlockDestroyed;
            DestructionEvents.Collapsed -= OnCollapsed;
            foreach (var p in _players)
                if (p.Health != null)
                    p.Health.Depleted -= OnDepleted;
        }

        private void Awake()
        {
            Current = this;
            FindPlayers();
            if (Debug.isDebugBuild && !LevelSelection.IsRanked) // a full meter for free would void the ranked log
                gameObject.AddComponent<ActionTestKeys>(); // 1-9: try any action with a full meter
            State = new RunState(_players.Count, rules);
            Score = new RunScore(_players.Count);
            Tally = new RunTally(_players.Count);
            State.PhaseChanged += OnPhaseChanged;
            State.PlayerOut += OnPlayerOut;
            State.PlayerRejoined += OnPlayerRejoined;

            var main = UnityEngine.Camera.main;
            _camera = main != null ? main.GetComponent<ScrollCamera>() : null;
            if (_camera != null)
                _camera.Scrolling = false;
            RefreshCameraTargets();
            SetControls(false);
        }

        /// <summary>Ranked runs play with no upgrades and lend every action (docs/ranked.md K-D2): a fresh save.</summary>
        private static readonly SaveData RankedKit = new SaveData();

        private void FindPlayers()
        {
            // Online each device has one local player, and both players are in the run.
            int wanted = NetRole.IsOnline ? PlayerNames.Length : Session.PlayerCount > 0 ? Session.PlayerCount : PlayerNames.Length;
            for (int i = 0; i < PlayerNames.Length; i++)
            {
                GameObject go = GameObject.Find(PlayerNames[i]);
                if (go == null)
                    continue;
                if (_players.Count >= wanted)
                {
                    go.SetActive(false); // solo run: the second player sits out
                    continue;
                }

                var player = new Player
                {
                    Index = _players.Count,
                    Transform = go.transform,
                    Health = go.GetComponent<PlayerHealth>(),
                    Combat = go.GetComponent<PlayerCombat>(),
                    Motor = go.GetComponent<PlayerMotor>(),
                };
                var input = go.GetComponent<PlayerInputReader>();
                player.Input = input;
                input.PlayerIndex = player.Index;
                input.AssignDevice(!NetRole.IsOnline ? Session.DeviceFor(player.Index)
                    : player.Index == NetRole.LocalPlayer ? Session.DeviceFor(0) : null);
                player.Health.ManagedByRun = true;
                player.Health.Depleted += OnDepleted;
                PlayerUpgrades.Apply(player, LevelSelection.IsRanked ? RankedKit : Profile.Data);
                if (!go.TryGetComponent(out player.Action))
                    player.Action = go.AddComponent<ActionUser>();
                // Online this device's loadout is its own player's (its P1 pick); the other's arrives with its announce.
                int pick = NetRole.IsOnline ? (player.Index == NetRole.LocalPlayer ? 0 : -1) : player.Index;
                player.Action.Configure(player.Index, pick < 0 ? ActionCatalog.Default
                    : LevelSelection.IsRanked ? ActionCatalog.RankedPick(Profile.Data, pick) : ActionCatalog.Equipped(Profile.Data, pick));
                // Looks likewise: online the other's comes through its NetPlayer.
                if (pick >= 0)
                    LookSkin.Dress(go, LookCatalog.Wearing(Profile.Data, pick));
                _players.Add(player);
            }

            // Online the other device's player is a kinematic copy a few frames behind: bumping into
            // it shoves ours, which shoves our copy on their screen into theirs, and so on until
            // both are pushed off the level. So online the players pass through each other.
            if (NetRole.IsOnline && _players.Count == 2)
                foreach (Collider2D a in _players[0].Transform.GetComponentsInChildren<Collider2D>(true))
                    foreach (Collider2D b in _players[1].Transform.GetComponentsInChildren<Collider2D>(true))
                        Physics2D.IgnoreCollision(a, b);
        }

        private void Update()
        {
            if (State.IsOver)
                return;
            if (_camera != null)
                _camera.Zoom(ZoomInput());
            if (State.Phase == RunPhase.Countdown)
                RunInTick();

            FinishLine finish = FinishLine.Active;
            for (int i = 0; i < _players.Count; i++)
            {
                if (State.Status(i) != PlayerStatus.Alive)
                    continue;
                Vector3 pos = _players[i].Transform.position;
                if (NetRole.IsClient)
                    continue; // online, the host's RunState decides
                if (State.Phase == RunPhase.Playing)
                {
                    Player p = _players[i];
                    Tally.Observe(i, p.Combat.WeaponHits, p.Combat.DynamiteCount, p.Health.Current);
                }
                State.SetInBounds(i, IgnoreBounds || IsInBounds(pos));
                if (finish != null && pos.x >= finish.X)
                    State.ReportCrossedFinish(i);
            }

            if (_camera != null && finish != null)
                _camera.StopX = finish.X;

            if (!NetRole.IsClient)
                State.Tick(Time.deltaTime);
        }

        private bool IsInBounds(Vector3 position)
        {
            Camera cam = Camera;
            if (cam == null)
                return true;
            Vector3 vp = cam.WorldToViewportPoint(position);
            return vp.x >= 0f && vp.y >= -bottomMargin;
        }

        // Either local player zooms the shared camera; the other device's player zooms theirs.
        private float ZoomInput()
        {
            float steps = 0f;
            for (int i = 0; i < _players.Count; i++)
                if (!_players[i].Health.IsProxy)
                    steps += _players[i].Input.Zoom;
            return steps;
        }

        /// <summary>Online host: the other device left. Its player is out for good; the run goes on.</summary>
        public void PlayerLeft(int index)
        {
            if (index < 0 || index >= _players.Count)
                return;
            State.Leave(index);
            PromptShown?.Invoke($"PLAYER {index + 1} LEFT THE GAME", 3f);
        }

        /// <summary>Online client: show points the host awarded (the score itself arrives with the run state).</summary>
        public void ShowScored(int player, int points, Vector3 at) => Scored?.Invoke(player, points, at);

        /// <summary>A player collected a Resurrect pickup. False if nobody needed it.</summary>
        public bool TryResurrect(int collector) => State.TryResurrect(collector);

        /// <summary>Play the level again; a ranked run asks the server for a new attempt first (one run each).</summary>
        public void Retry()
        {
            if (!Ranked.RankedSession.Again())
                LoadScene(SceneManager.GetActiveScene().name);
        }

        public void QuitToMenu()
        {
            if (NetRole.IsClient)
                NetRole.Leave?.Invoke(); // the host keeps choosing for the session; this device leaves it
            LevelSelection.ReturnToLevelSelect = true;
            LoadScene(menuScene);
        }

        public void NextLevel()
        {
            if (_nextLevel != null)
                _nextLevel();
            else
                LoadScene(nextScene);
        }

        public static void LoadScene(string scene)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (NetRole.LoadScene != null && NetRole.LoadScene(scene))
                return; // online host: both devices load it
            SceneManager.LoadScene(scene);
        }

        // ---------------- Model → scene ----------------

        // Right is held while they're behind schedule, so they reach their spot as the countdown
        // ends however the physics ramps up and coasts.
        private void RunInTick()
        {
            foreach (Runner r in _runners)
            {
                PlayerInputReader input = r.Player.Input;
                if (input.Source != r.Script)
                {
                    r.Own = input.Source; // taken here, not in RunIn: a bot sets its own source in Start
                    input.Source = r.Script;
                    r.Player.Motor.enabled = true;
                }
                float due = r.ToX - State.Countdown * r.Player.Motor.MaxSpeed;
                r.Script.Next.Move = new Vector2(r.Player.Transform.position.x < due ? 1f : 0f, 0f);
            }
        }

        private void OnPhaseChanged(RunPhase phase)
        {
            bool playing = phase == RunPhase.Playing;
            foreach (Runner r in _runners)
                if (r.Player.Input.Source == r.Script)
                    r.Player.Input.Source = r.Own; // still running as the controls take over
            _runners.Clear();
            SetControls(playing);
            if (_camera != null)
                _camera.Scrolling = playing;
            if (State.IsOver)
                Bank();
        }

        private void Bank()
        {
            bool finished = State.Phase == RunPhase.Finished;
            BankedFraction = DestroyedFraction;
            BankedScore = FinalScore;
            bool withinPar = RunScore.WithinPar(State.Elapsed, parTime);
            Reward = Profile.Data.RecordRun(LevelId, finished, RunScore.GradeFor(BankedFraction), BankedScore, State.Elapsed,
                withinPar, Objectives.MetMask(Sheet, Tally, finished, BankedFraction, withinPar));
            Profile.Save();
            Ended?.Invoke();
        }

        /// <summary>A rewarded ad paid out: bank the run's cash once more.</summary>
        public void DoubleReward()
        {
            Profile.Data.AddCash(Reward.Cash);
            Profile.Save();
            RunReward doubled = Reward;
            doubled.Cash *= 2;
            Reward = doubled;
        }

        private void OnPlayerOut(int index, bool willRejoin)
        {
            _players[index].Health.Kill();
            if (willRejoin && !NetRole.IsClient) // a client's score is the host's, penalty included
                Score.ApplyPenalty(index, rejoinPenalty);
            RefreshCameraTargets();
        }

        private void OnPlayerRejoined(int index)
        {
            Player partner = null;
            for (int i = 0; i < _players.Count; i++)
                if (i != index && State.Status(i) == PlayerStatus.Alive)
                    partner = _players[i];

            Vector3 at = partner != null
                ? partner.Transform.position + (Vector3)rejoinOffset
                : _players[index].Transform.position;
            _players[index].Health.Revive(at);
            RefreshCameraTargets();
        }

        private void OnDepleted(PlayerHealth health)
        {
            if (NetRole.IsClient)
                return; // the host hears it through the player's health and decides
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].Health == health)
                    State.ReportDepleted(i);
        }

        private void SetControls(bool enabled)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (State.Status(i) != PlayerStatus.Alive)
                    continue;
                bool local = !_players[i].Health.IsProxy;
                _players[i].Motor.enabled = enabled && local;
                _players[i].Combat.enabled = enabled && local;
            }
        }

        private void RefreshCameraTargets()
        {
            if (_camera == null)
                return;
            var targets = new List<Transform>();
            for (int i = 0; i < _players.Count; i++)
                if (State.Status(i) == PlayerStatus.Alive)
                    targets.Add(_players[i].Transform);
            _camera.SetTargets(targets);
        }

        // ---------------- Destruction → score ----------------

        private void OnBuildingReady(int cells) => _totalCells += cells;

        private void OnStandingCellsRemoved(int cells) => _removedCells += cells;

        // Online only the host scores; a client shows the host's score.
        private void OnBlockDestroyed(BlockDestroyedInfo info)
        {
            if (State.Phase != RunPhase.Playing || NetRole.IsClient)
                return;
            int points = Score.AddDestroyed(info.Instigator, info.Material, info.Cells);
            Scored?.Invoke(info.Instigator, points, info.Position);
        }

        private void OnCollapsed(CollapseInfo info)
        {
            if (State.Phase != RunPhase.Playing || NetRole.IsClient)
                return;
            int points = Score.AddCollapse(info.Instigator, info.Cells);
            Tally.Collapsed(info.Cells.Count, Time.time);
            Scored?.Invoke(info.Instigator, points, info.Position);
        }
    }
}
