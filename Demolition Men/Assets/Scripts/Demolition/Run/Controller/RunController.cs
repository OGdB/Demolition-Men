using System;
using System.Collections.Generic;
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

        private readonly List<Player> _players = new List<Player>();
        private ScrollCamera _camera;
        private int _totalCells;
        private int _removedCells;

        /// <summary>The run in progress, if the scene has one.</summary>
        public static RunController Current { get; private set; }

        public RunState State { get; private set; }
        public RunScore Score { get; private set; }
        public IReadOnlyList<Player> Players => _players;
        public float ParTime => parTime;
        public Camera Camera => _camera != null ? _camera.Camera : null;

        /// <summary>Share of every building's cells destroyed or collapsed, 0..1.</summary>
        public float DestroyedFraction => _totalCells == 0 ? 0f : (float)_removedCells / _totalCells;

        public int FinalScore => State.Phase == RunPhase.Finished
            ? Score.FinalTotal(State.Elapsed, parTime)
            : Score.Total;

        /// <summary>Points just awarded: player (-1 for none), points, world position.</summary>
        public event Action<int, int, Vector3> Scored;
        /// <summary>A tutorial hint to show: message, seconds.</summary>
        public event Action<string, float> PromptShown;

        public bool HasNextLevel => !string.IsNullOrEmpty(nextScene);

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
            State = new RunState(_players.Count, rules);
            Score = new RunScore(_players.Count);
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

        private void FindPlayers()
        {
            int wanted = Session.PlayerCount > 0 ? Session.PlayerCount : PlayerNames.Length;
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
                input.PlayerIndex = player.Index;
                input.AssignDevice(Session.DeviceFor(player.Index));
                player.Health.ManagedByRun = true;
                player.Health.Depleted += OnDepleted;
                _players.Add(player);
            }
        }

        private void Update()
        {
            if (State.IsOver)
                return;

            FinishLine finish = FinishLine.Active;
            float frontX = float.MinValue;
            for (int i = 0; i < _players.Count; i++)
            {
                if (State.Status(i) != PlayerStatus.Alive)
                    continue;
                Vector3 pos = _players[i].Transform.position;
                frontX = Mathf.Max(frontX, pos.x);
                State.SetInBounds(i, IsInBounds(pos));
                if (finish != null && pos.x >= finish.X)
                    State.ReportCrossedFinish(i);
            }
            ShowReachedPrompts(frontX);

            if (_camera != null && finish != null)
                _camera.StopX = finish.X;

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

        // Several hints reached at once (a fast player): only the furthest one is current.
        private void ShowReachedPrompts(float frontX)
        {
            TutorialPrompt furthest = null;
            foreach (TutorialPrompt prompt in TutorialPrompt.All)
            {
                if (prompt.Shown || frontX < prompt.X)
                    continue;
                prompt.Shown = true;
                if (furthest == null || prompt.X > furthest.X)
                    furthest = prompt;
            }
            if (furthest != null)
                PromptShown?.Invoke(furthest.Message, furthest.Duration);
        }

        /// <summary>A player collected a Resurrect pickup. False if nobody needed it.</summary>
        public bool TryResurrect(int collector) => State.TryResurrect(collector);

        public void Retry() => LoadScene(SceneManager.GetActiveScene().name);

        public void QuitToMenu() => LoadScene(menuScene);

        public void NextLevel() => LoadScene(nextScene);

        private static void LoadScene(string scene)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }

        // ---------------- Model → scene ----------------

        private void OnPhaseChanged(RunPhase phase)
        {
            bool playing = phase == RunPhase.Playing;
            SetControls(playing);
            if (_camera != null)
                _camera.Scrolling = playing;
        }

        private void OnPlayerOut(int index, bool willRejoin)
        {
            _players[index].Health.Kill();
            if (willRejoin)
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
                _players[i].Motor.enabled = enabled;
                _players[i].Combat.enabled = enabled;
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

        private void OnBlockDestroyed(BlockDestroyedInfo info)
        {
            if (State.Phase != RunPhase.Playing)
                return;
            int points = Score.AddDestroyed(info.Instigator, info.Material, info.Cells);
            Scored?.Invoke(info.Instigator, points, info.Position);
        }

        private void OnCollapsed(CollapseInfo info)
        {
            if (State.Phase != RunPhase.Playing)
                return;
            int points = Score.AddCollapse(info.Instigator, info.Cells);
            Scored?.Invoke(info.Instigator, points, info.Position);
        }
    }
}
