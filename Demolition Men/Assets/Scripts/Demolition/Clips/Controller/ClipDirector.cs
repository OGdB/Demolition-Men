#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Demolition.Generation;
using Demolition.Levels;
using Demolition.Meta;
using Demolition.Run;
using UnityEngine;

namespace Demolition.Clips
{
    /// <summary>
    /// Plays a <see cref="ShotSpec"/> in the Run scene for the Clip Lab (Tools ▸ Demolition ▸
    /// Clip Lab; editor only). The Clip Lab writes a request before entering play mode; this
    /// boots on a temporary profile, places the players at the shot's building with its kit,
    /// drives them through <see cref="ScriptedInputSource"/>s (walks through the
    /// <see cref="BotPilot"/>), frames the camera, hides the HUD and owns the time scale. Once the
    /// countdown is over it is <see cref="Stage.Ready"/>; the Clip Lab starts the recorder and calls
    /// <see cref="Roll"/>. Every collapse, break and blast goes to a JSON log next to the video.
    /// </summary>
    public sealed class ClipDirector : MonoBehaviour
    {
        public const string RequestKey = "Demolition.Clips.Request";
        public enum Stage { Setup, Ready, Rolling, Done }

        /// <summary>Wait this long after GO!, so the countdown text is gone before the first frame.</summary>
        private const float SettleSeconds = 1.2f;
        private const float WalkTimeout = 20f;
        private const float UntilTimeout = 10f;
        private const float JumpSeconds = 0.35f;

        public static ClipDirector Current { get; private set; }

        public Stage State { get; private set; }
        public ShotSpec Shot { get; private set; }
        public int Take { get; private set; }
        /// <summary>Output path without extension: the video, its log and stills share it.</summary>
        public string OutputBase { get; private set; }
        /// <summary>The Clip Lab records this take (it calls <see cref="Roll"/>); else it rolls by itself.</summary>
        public bool Records { get; private set; }
        /// <summary>What happened so far (written next to the video when the shot ends).</summary>
        public ShotLog Log => _log;
        /// <summary>Video seconds since <see cref="Roll"/>.</summary>
        public float VideoTime => State == Stage.Rolling || State == Stage.Done ? (Time.frameCount - _startFrame) / (float)Shot.fps : 0f;

        private sealed class Lane
        {
            public readonly List<ShotStep> Steps = new List<ShotStep>();
            public int Index = -1;
            public float StartedAt;
            public int EventsAtStart;
            public bool Fresh; // the step hasn't had its first frame yet
            public ShotStep Step => Index < Steps.Count ? Steps[Index] : null;
        }

        private readonly Lane[] _lanes = { new Lane(), new Lane(), new Lane() };
        private readonly ScriptedInputSource[] _sources = { new ScriptedInputSource(), new ScriptedInputSource() };
        private readonly BotPilot[] _bots = new BotPilot[2];
        private readonly ShotLog _log = new ShotLog();
        private RunController _run;
        private LevelBuilder _level;
        private ScrollCamera _scroll;
        private Camera _camera;
        private Vector3 _anchor;          // the building's bottom-left corner, or the spawn
        private Vector3 _framed;          // where the camera sits before the shake
        private BuildingPlacement _placement;
        private SpecBlock _weak;
        private float _readyAt = -1f;
        private int _startFrame;
        private float _scale = 1f;
        private float _slowUntil;
        private float _fixedDelta;
        private bool _setCaptureRate;
        private int _setupFrames;

        // ---------------- Boot ----------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            string request = PlayerPrefs.GetString(RequestKey, string.Empty);
            if (string.IsNullOrEmpty(request))
                return;
            PlayerPrefs.DeleteKey(RequestKey);
            // "shotPath|take|outputBase|record|utcTicks", written right before entering play mode.
            string[] parts = request.Split('|');
            if (parts.Length < 5 || !long.TryParse(parts[4], out long ticks)
                || (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalSeconds > 60)
                return;
            ShotSpec shot;
            try
            {
                shot = ShotSpec.FromJson(File.ReadAllText(parts[0]));
            }
            catch (Exception e)
            {
                Debug.LogError($"Clip Lab: can't read {parts[0]}: {e.Message}");
                return;
            }
            List<string> errors = shot.Errors();
            if (errors.Count > 0)
            {
                Debug.LogError($"Clip Lab: {parts[0]}: {string.Join("; ", errors)}");
                return;
            }
            Play(shot, int.TryParse(parts[1], out int take) ? take : 1, parts[2], parts[3] == "1");
        }

        /// <summary>Direct the next Run scene that loads (the boot above, PlayMode tests).
        /// <paramref name="records"/>: wait for <see cref="Roll"/> instead of rolling when ready.</summary>
        public static ClipDirector Play(ShotSpec shot, int take, string outputBase, bool records)
        {
            // A fresh profile: the shot's upgrades, no music (it goes on in the edit), nothing saved.
            Profile.UseTemporary();
            foreach (UpgradeDefinition upgrade in UpgradeCatalog.All)
                Profile.Data.SetUpgradeLevel(upgrade.Id, Mathf.Clamp(shot.upgrades, 0, upgrade.MaxLevel));
            GameSettings settings = Profile.Data.settings;
            settings.musicVolume = 0f;
            settings.quality = (int)QualityChoice.High; // full resolution, every block falls as a body
            settings.touchControls = (int)(shot.hud && shot.touch ? TouchControlsMode.On : TouchControlsMode.Off);
            Platform.TouchMode = (TouchControlsMode)settings.touchControls;
            LevelBootstrap.Players = shot.players;

            var go = new GameObject("ClipDirector");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<ClipDirector>();
            Current.Shot = shot;
            Current.Take = take;
            Current.OutputBase = outputBase;
            Current.Records = records;
            return Current;
        }

        /// <summary>The recorder is running: the timeline starts on this frame.</summary>
        public void Roll()
        {
            if (State != Stage.Ready)
                return;
            State = Stage.Rolling;
            _startFrame = Time.frameCount;
            foreach (Lane lane in _lanes)
                Advance(lane);
        }

        private void Awake() => _fixedDelta = Time.fixedDeltaTime;

        private void OnEnable()
        {
            DestructionEvents.Collapsed += OnCollapsed;
            DestructionEvents.BlockDestroyed += OnBlockDestroyed;
            Dynamite.Exploded += OnExploded;
        }

        private void OnDisable()
        {
            DestructionEvents.Collapsed -= OnCollapsed;
            DestructionEvents.BlockDestroyed -= OnBlockDestroyed;
            Dynamite.Exploded -= OnExploded;
        }

        private void OnDestroy()
        {
            if (Current != this && Current != null)
                return; // destroyed late, after the next shot began: leave that one's setup alone
            Restore();
            Profile.UseDisk();
            Current = null;
        }

        // ---------------- Setup ----------------

        private void Update()
        {
            switch (State)
            {
                case Stage.Setup:
                    TrySetup();
                    break;
                case Stage.Ready:
                    break;
                case Stage.Rolling:
                    Tick();
                    break;
            }
            // One physics step per frame, also in slow motion. Not while time stands still (the
            // Recorder holds it at 0 while it resizes the Game view): the step would stay tiny.
            if (State != Stage.Done && Time.timeScale > 0f)
                Time.fixedDeltaTime = Time.timeScale / Shot.fps;
        }

        private void TrySetup()
        {
            if (_run == null)
            {
                RunController run = RunController.Current;
                LevelBuilder level = FindAnyObjectByType<LevelBuilder>();
                if (run == null || level == null || level.Layout == null || run.Camera == null)
                    return;
                _run = run;
                _level = level;
                Configure();
            }
            ClearPickups();
            _setupFrames++;
            if (_run.State.Phase != RunPhase.Playing)
                return;
            if (_readyAt < 0f)
                _readyAt = Time.unscaledTime;
            if (Time.unscaledTime - _readyAt < SettleSeconds || _setupFrames < 10)
                return;
            if (Shot.building >= 0 && !DestructibleBuilding.TryGetByNetIndex(Shot.building, out _))
                return; // not streamed in yet
            State = Stage.Ready;
            if (!Records)
                Roll();
        }

        private void Configure()
        {
            if (!Records)
            {
                // The recorder sets these when it runs; a preview plays at the recording's pace.
                Time.captureFramerate = Shot.fps;
                Application.targetFrameRate = Shot.fps;
                _setCaptureRate = true;
            }

            LevelLayout layout = _level.Layout;
            Vector3 origin = _level.transform.position;
            if (Shot.building >= 0 && Shot.building < layout.Buildings.Count)
            {
                _placement = layout.Buildings[Shot.building];
                _anchor = origin + new Vector3(_placement.X, _placement.GroundY, 0f);
                _weak = ShotPlanning.WeakPoint(_placement.Spec, out _);
            }
            else
            {
                _anchor = origin + new Vector3(layout.SpawnX, layout.GroundAt(layout.SpawnX), 0f);
            }

            // The take nudges the start a little: same inputs, a slightly different collapse.
            float start = Shot.building >= 0 ? _anchor.x + Shot.offset + (Take - 1) * 0.37f : float.NaN;
            for (int i = 0; i < _run.Players.Count; i++)
            {
                RunController.Player player = _run.Players[i];
                if (!float.IsNaN(start))
                {
                    float x = start - 1.5f * i;
                    int ground = layout.GroundAt(Mathf.FloorToInt(x - origin.x));
                    var at = new Vector3(x, (ground == LevelLayout.NoGround ? _anchor.y : origin.y + ground) + 0.1f, 0f);
                    player.Transform.position = at;
                    if (player.Transform.TryGetComponent(out Rigidbody2D body))
                    {
                        body.position = at;
                        body.linearVelocity = Vector2.zero;
                    }
                }
                if (Shot.dynamite > 0)
                    player.Combat.DynamiteCount = Shot.dynamite;
                if (Shot.weapon == "axe")
                    player.Combat.AddWeapon(WeaponType.Axe, Shot.weaponHits);
                else if (Shot.weapon == "wreckingball")
                    player.Combat.AddWeapon(WeaponType.WreckingBall, Shot.weaponHits);
                player.Input.Source = _sources[i];
            }

            _camera = _run.Camera;
            _camera.rect = new Rect(0f, 0f, 1f, 1f); // no letterbox in a clip
            _scroll = _camera.GetComponent<ScrollCamera>();
            if (Shot.camera.mode == "run")
            {
                if (!float.IsNaN(start) && _scroll != null)
                {
                    // Start the run camera at the shot's building instead of the spawn.
                    float half = _camera.orthographicSize * _camera.aspect;
                    Vector3 pos = _camera.transform.position;
                    _camera.transform.position = new Vector3(start + half * 0.6f, pos.y, pos.z);
                    _scroll.ResetFloor();
                }
            }
            else
            {
                if (_scroll != null)
                    _scroll.enabled = false;
                _run.IgnoreBounds = true;
                _framed = _camera.transform.position;
                PlaceCamera(1f);
            }

            foreach (ShotStep step in Shot.steps)
                _lanes[step.p].Steps.Add(step);
            _log.shot = Shot.id;
            _log.take = Take;
            _log.fps = Shot.fps;
        }

        // Pickups on the way would change the kit: marked taken so they don't stream in, and the
        // ones already there go (every Setup frame: a segment can stream in after Configure).
        private void ClearPickups()
        {
            if (Shot.building < 0)
                return;
            float x0 = _anchor.x + Shot.offset - 1.5f * Shot.players - 2f;
            float x1 = _anchor.x + (_placement != null ? _placement.Spec.Width : 0) + 2f;
            float ox = _level.transform.position.x;
            List<PickupPlacement> pickups = _level.Layout.Pickups;
            for (int i = 0; i < pickups.Count; i++)
                if (pickups[i].X + ox >= x0 && pickups[i].X + ox <= x1)
                    Pickup.MarkTaken(i);
            foreach (Pickup pickup in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                if (pickup.transform.position.x >= x0 && pickup.transform.position.x <= x1)
                    Destroy(pickup.gameObject);
        }

        // ---------------- Timeline ----------------

        private void Tick()
        {
            float t = VideoTime;
            if (_slowUntil > 0f && t >= _slowUntil)
                SetScale(1f);
            for (int p = 1; p < _lanes.Length; p++)
                if (p <= _run.Players.Count)
                    TickPlayer(p - 1, _lanes[p], t);
            TickDirector(_lanes[0], t);
            if (_lanes[0].Step == null || t >= Shot.length)
                Finish();
        }

        private void TickDirector(Lane lane, float t)
        {
            ShotStep step = lane.Step;
            if (step == null)
                return;
            float elapsed = t - lane.StartedAt;
            switch (step.@do)
            {
                case "wait":
                    if (Waited(step, lane, elapsed))
                        Advance(lane);
                    break;
                case "slowmo":
                    SetScale(step.scale);
                    _slowUntil = t + Mathf.Max(step.seconds, 0.1f);
                    Advance(lane);
                    break;
                case "still":
                    ScreenCapture.CaptureScreenshot($"{OutputBase}-{step.name}.png", Shot.stillScale);
                    Advance(lane);
                    break;
                case "end":
                    lane.Index = lane.Steps.Count;
                    break;
            }
        }

        private void TickPlayer(int index, Lane lane, float t)
        {
            ScriptedInputSource source = _sources[index];
            RunController.Player player = _run.Players[index];
            ShotStep step = lane.Step;
            source.Next.Move = Vector2.zero;
            source.Next.AttackHeld = false;
            source.Next.JumpHeld = false;
            if (step == null)
                return;
            float elapsed = t - lane.StartedAt;
            bool first = lane.Fresh;
            lane.Fresh = false;
            switch (step.@do)
            {
                case "walk":
                {
                    BotPilot bot = Bot(index, WalkTarget(step), false);
                    if (bot.Arrived || TimedOut(step, elapsed, WalkTimeout))
                    {
                        EndBot(index);
                        Advance(lane);
                    }
                    break;
                }
                case "bot":
                    Bot(index, float.PositiveInfinity, true);
                    if (Waited(step, lane, elapsed))
                    {
                        EndBot(index);
                        Advance(lane);
                    }
                    break;
                case "hit":
                    source.Next.AttackHeld = true;
                    if (Waited(step, lane, elapsed))
                        Advance(lane);
                    break;
                case "tnt":
                    source.Next.DynamitePressed = true;
                    Advance(lane);
                    break;
                case "jump":
                    source.Next.JumpPressed = first;
                    source.Next.JumpHeld = true;
                    source.Next.Move = new Vector2(Mathf.Sign(step.x) * (step.x != 0f ? 1f : 0f), 0f);
                    if (elapsed >= (step.seconds > 0f ? step.seconds : JumpSeconds))
                    {
                        source.Next.JumpReleased = true;
                        Advance(lane);
                    }
                    break;
                case "back":
                    source.Next.Move = new Vector2(-1f, 0f);
                    if (elapsed >= step.seconds)
                        Advance(lane);
                    break;
                case "swap":
                    source.Next.WeaponCycle = 1;
                    Advance(lane);
                    break;
                case "wait":
                    if (Waited(step, lane, elapsed))
                        Advance(lane);
                    break;
            }
        }

        private bool Waited(ShotStep step, Lane lane, float elapsed) =>
            step.until != "" ? Happened(step, lane) || TimedOut(step, elapsed, UntilTimeout) : elapsed >= step.seconds;

        // An until or a walk that ran out of time: noted in the log, the shot wants tuning.
        private bool TimedOut(ShotStep step, float elapsed, float fallback)
        {
            if (elapsed < (step.seconds > 0f ? step.seconds : fallback))
                return false;
            int number = Array.IndexOf(Shot.steps, step) + 1;
            _log.timeouts.Add($"step {number} ({step.@do} {step.until}{step.to})");
            Debug.LogWarning($"Clip Lab: {Shot.id} step {number} ({step.@do} {step.until}{step.to}) timed out after {elapsed:0.0}s");
            return true;
        }

        // The step's event happened since the step began.
        private bool Happened(ShotStep step, Lane lane)
        {
            if (step.until == "over")
                return _run.State.IsOver;
            for (int i = lane.EventsAtStart; i < _log.events.Count; i++)
            {
                ShotEvent e = _log.events[i];
                if (e.kind == step.until && (e.kind != "collapse" || e.cells >= step.cells))
                    return true;
            }
            return false;
        }

        private void Advance(Lane lane)
        {
            lane.Index++;
            lane.StartedAt = VideoTime;
            lane.EventsAtStart = _log.events.Count;
            lane.Fresh = true;
        }

        private float WalkTarget(ShotStep step)
        {
            switch (step.to)
            {
                case "weakpoint" when _weak != null: return _anchor.x + _weak.Rect.xMin;
                case "weakpoint":
                case "building": return _anchor.x;
                case "end": return _anchor.x + (_placement != null ? _placement.Spec.Width : 0) + 2f;
                default: return _anchor.x + step.x;
            }
        }

        private BotPilot Bot(int index, float stopX, bool dynamite)
        {
            if (_bots[index] == null)
                _bots[index] = _run.Players[index].Transform.gameObject.AddComponent<BotPilot>();
            _bots[index].StopX = stopX;
            _bots[index].ThrowsDynamite = dynamite;
            return _bots[index];
        }

        private void EndBot(int index)
        {
            if (_bots[index] == null)
                return;
            Destroy(_bots[index]);
            _bots[index] = null;
            _run.Players[index].Input.Source = _sources[index];
        }

        private void SetScale(float scale)
        {
            _scale = scale;
            _slowUntil = 0f;
            RunJuice.BaseTimeScale = scale;
            Time.timeScale = scale;
        }

        // ---------------- Camera, HUD ----------------

        private void LateUpdate()
        {
            if (_run == null || State == Stage.Done)
                return;
            if (!Shot.hud)
                foreach (Canvas canvas in _run.GetComponentsInChildren<Canvas>(true))
                    canvas.enabled = false;
            if (Shot.camera.mode != "run")
            {
                PlaceCamera(1f - Mathf.Exp(-4f / Shot.fps)); // per video frame: a slow editor frame doesn't snap it
                _camera.transform.position = _framed + CameraShake.Tick(Time.deltaTime);
            }
        }

        // Move the framing towards where the mode wants it (blend 1: jump there).
        private void PlaceCamera(float blend)
        {
            ShotCamera cam = Shot.camera;
            float x, y, size;
            float ground = _anchor.y - 1f; // a strip of street under the feet
            switch (cam.mode)
            {
                case "pan":
                {
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(VideoTime / Mathf.Max(0.1f, cam.seconds)));
                    size = cam.size > 0f ? cam.size : 7f;
                    float share = ShotPlanning.SafeShare(Shot.width, Shot.height);
                    x = _anchor.x + Mathf.Lerp(cam.from, cam.to, k);
                    y = ground + size * share;
                    blend = 1f;
                    break;
                }
                case "follow":
                {
                    float front = float.MinValue;
                    foreach (RunController.Player p in _run.Players)
                        front = Mathf.Max(front, p.Transform.position.x);
                    size = cam.size > 0f ? cam.size : 6f;
                    x = front + cam.lead;
                    y = ground + size * ShotPlanning.SafeShare(Shot.width, Shot.height);
                    break;
                }
                default: // frame
                    ShotPlanning.FrameBuilding(Shot, _anchor.x, _anchor.y, _placement != null ? _placement.Spec.Width : 8,
                        _placement != null ? _placement.Spec.Height : 6, _level.transform.position.x, out x, out y, out size);
                    break;
            }
            x = ShotPlanning.ClampLeft(x, size, Shot, _level.transform.position.x);
            _framed = Vector3.Lerp(_framed, new Vector3(x, y, _framed.z), blend);
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, size, blend);
        }

        // ---------------- Log, end ----------------

        private void Record(string kind, int cells, Vector3 at)
        {
            if (State != Stage.Rolling)
                return;
            _log.events.Add(new ShotEvent { t = VideoTime, kind = kind, cells = cells, x = at.x, y = at.y });
        }

        private void OnCollapsed(CollapseInfo info) => Record("collapse", info.Cells.Count, info.Position);

        private void OnBlockDestroyed(BlockDestroyedInfo info) => Record("break", info.Cells, info.Position);

        private void OnExploded(Vector3 at) => Record("blast", 0, at);

        private void Finish()
        {
            _log.length = VideoTime;
            State = Stage.Done;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(OutputBase));
                File.WriteAllText(OutputBase + ".json", JsonUtility.ToJson(_log, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Clip Lab: log not written: {e.Message}");
            }
            Restore();
        }

        // Leave nothing behind for the next play session (statics survive without a domain reload).
        // The temporary profile stays until the director goes with play mode: a run that ends
        // after the shot must not bank into the real save.
        private void Restore()
        {
            SetScale(1f);
            Time.fixedDeltaTime = _fixedDelta;
            if (_setCaptureRate)
            {
                Time.captureFramerate = 0;
                Application.targetFrameRate = -1;
                _setCaptureRate = false;
            }
            LevelBootstrap.Players = 0;
        }
    }
}
#endif
