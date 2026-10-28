using System.Collections;
using Demolition.Meta;
using Demolition.Run;
using UnityEngine;

namespace Demolition.Levels
{
    /// <summary>
    /// The Level scene's only authored object. Generates the selected level from its spec
    /// and seed, then assembles the scene from <see cref="GameConfig"/>: camera, backdrop,
    /// players (named Player_1 / Player_2 for the run), the level itself, the run and
    /// the music. Order matters: the run finds the camera and players when it wakes.
    /// </summary>
    public sealed class LevelBootstrap : MonoBehaviour
    {
        /// <summary>Backdrop height above the street, as authored for the hand-made levels.</summary>
        private const float BackdropHeight = 12.6f;
        /// <summary>Ground visible below the players' feet at the start.</summary>
        private const float GroundShown = 0.5f;

        /// <summary>Let the <see cref="BotPilot"/> play player 1 (PlayMode tests, the
        /// Tools ▸ Demolition ▸ Bot Run menu).</summary>
        public static bool AutoPilot;

        /// <summary>Spawn this many players whoever joined (the Clip Lab's two-player shots); 0: the session's.</summary>
        public static int Players;

        private void Awake()
        {
            GameConfig config = GameConfig.Instance;
            if (config == null)
            {
                Debug.LogError("No Resources/GameConfig asset: can't build the level.");
                return;
            }
            LevelSelection.EnsureSelected();
            LevelSpec spec = LevelSelection.Spec;
            LevelTheme theme = LevelSelection.Theme;

            LevelLayout layout = LevelGrammar.Generate(spec, config.Archetypes(theme), JumpProfile.Default, out TraversalReport report);
            if (!report.Passable)
                Debug.LogWarning($"Level {spec.id} seed {spec.seed}: {report}");

            float ground = layout.GroundAt(layout.SpawnX);
            var spawn = new Vector3(layout.SpawnX + 0.5f, ground + 0.1f, 0f);

            ScrollCamera scroll = SpawnCamera(config, theme, spec, spawn, ground);
            SpawnBackdrop(config, theme, spawn.x, ground);
            // The players start back over the run-up, so they are never seen standing at the spawn.
            float runUp = RunUp(config);
            Vector3 start = spawn + Vector3.left * runUp;
            int players = NetRole.IsOnline ? NetRole.SpawnPlayers?.Invoke(start) ?? 0 : SpawnPlayers(config, start);
            UiFactory.EnsureEventSystem();

            var level = new GameObject("Level").AddComponent<LevelBuilder>();
            level.Build(layout, theme, config.pickupPrefab, config.caravan, players > 1 || NetRole.IsOnline);
            if (scroll != null)
                scroll.LowestGround = level.LowestGroundBetween; // show trench floors before the drop
            NetRole.LevelBuilt?.Invoke(layout);

            // Online the players arrive over the network; the run finds them by name when it wakes.
            if (NetRole.IsOnline)
                StartCoroutine(StartRunWhenPlayersExist(config, spec, layout, runUp));
            else
                StartRun(config, spec, layout, runUp);

            SpawnMusic(theme);
        }

        // The countdown in the boots every profile starts with; faster ones wait a moment before running in.
        private static float RunUp(GameConfig config)
        {
#if UNITY_EDITOR
            if (Clips.ClipDirector.Current != null)
                return 0f; // a Clip Lab shot places its players itself
#endif
            float speed = config.playerPrefabs[0].GetComponent<PlayerMotor>().MaxSpeed;
            return Mathf.Min(speed * config.runPrefab.GetComponent<RunController>().CountdownTime, LevelGrammar.RunUp);
        }

        private static void StartRun(GameConfig config, LevelSpec spec, LevelLayout layout, float runUp)
        {
            var run = Instantiate(config.runPrefab).GetComponent<RunController>();
            run.name = "Run";
            run.Configure(spec.id, layout.Par, layout.TotalCells, LevelSelection.NextAction());
            run.Sheet = Objectives.Sheet(spec.seed, spec.objectives);
            if (LevelSelection.IsRanked)
            {
                if (!NetRole.IsClient) // the deciding device keeps the log
                    run.gameObject.AddComponent<Ranked.RunRecorder>().Begin(run, layout, LevelSelection.Ranked);
                run.gameObject.AddComponent<Ranked.RankedResultView>();
                run.gameObject.AddComponent<Ranked.GhostRunner>();
            }
            if (runUp > 0f)
                run.RunIn(runUp);
        }

        private static IEnumerator StartRunWhenPlayersExist(GameConfig config, LevelSpec spec, LevelLayout layout, float runUp)
        {
            while (GameObject.Find("Player_1") == null || GameObject.Find("Player_2") == null)
                yield return null;
            StartRun(config, spec, layout, runUp);
        }

        private static ScrollCamera SpawnCamera(GameConfig config, LevelTheme theme, LevelSpec spec, Vector3 spawn, float ground)
        {
            GameObject go = Instantiate(config.cameraPrefab);
            go.name = "Main Camera 1";
            var cam = go.GetComponent<Camera>();
            float size = cam.orthographicSize;
            // Placed before the scroll camera reads its floor: the street sits at the bottom.
            float x = Mathf.Max(spawn.x + size * 0.6f, size * cam.aspect - 0.5f); // no void left of the level
            go.transform.position = new Vector3(x, ground - GroundShown + size, -10f);
            cam.backgroundColor = theme.sky;
            // The sky sprites riding on the camera: tinted for the shared backdrop, hidden when
            // the theme brings its own layers (painted against its flat sky colour).
            bool ownBackdrop = theme.backdrop != null && theme.backdrop.Length > 0;
            foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>())
            {
                sr.enabled = !ownBackdrop;
                sr.color *= theme.backdropTint;
            }
            var scroll = go.GetComponent<ScrollCamera>();
            if (scroll != null)
            {
                scroll.ScrollSpeed = spec.scrollSpeed;
                scroll.ResetFloor();
            }
            return scroll;
        }

        /// <summary>One repeat of a backdrop layer's wrap: wider than the widest camera view.</summary>
        private const float BackdropPeriod = 160f;

        private static void SpawnBackdrop(GameConfig config, LevelTheme theme, float x, float ground)
        {
            if (theme.backdrop != null && theme.backdrop.Length > 0)
            {
                var root = new GameObject("Backdrop").transform;
                for (int i = 0; i < theme.backdrop.Length; i++)
                    SpawnBackdropLayer(theme.backdrop[i], root, x, ground, i - theme.backdrop.Length - 1);
                return;
            }
            if (config.backdropPrefab == null)
                return;
            GameObject backdrop = Instantiate(config.backdropPrefab, new Vector3(x, ground + BackdropHeight, 0f), Quaternion.identity);
            foreach (SpriteRenderer sr in backdrop.GetComponentsInChildren<SpriteRenderer>())
                sr.color *= theme.backdropTint;
        }

        // A tiled strip four wrap periods wide: the view (up to aspect 4 at max zoom) stays
        // covered however far it has drifted before the next wrap. A single layer (the sun)
        // is one sprite that never wraps.
        private static void SpawnBackdropLayer(BackdropLayer layer, Transform root, float x, float ground, int order)
        {
            if (layer.sprite == null)
                return;
            var go = new GameObject(layer.sprite.name, typeof(SpriteRenderer));
            go.transform.SetParent(root, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = layer.sprite;
            sr.sortingOrder = order;
            Vector2 tile = layer.sprite.bounds.size;
            float period = -1f;
            if (!layer.single)
            {
                sr.drawMode = SpriteDrawMode.Tiled;
                period = Mathf.Ceil(BackdropPeriod / tile.x) * tile.x;
                sr.size = new Vector2(period * 4f, tile.y);
            }
            else if (Camera.main != null)
            {
                // Starts `offset` right of the camera's centre, wherever the parallax carries it from there.
                float camX = Camera.main.transform.position.x;
                x = camX * (1f - layer.parallax) + layer.offset;
            }
            go.transform.position = new Vector3(x, ground + layer.bottom + tile.y * 0.5f, 0f);
            go.AddComponent<ParallaxLayer>().Configure(layer.parallax, period);
        }

        private static int SpawnPlayers(GameConfig config, Vector3 spawn)
        {
            int count = Mathf.Clamp(Players > 0 ? Players : Session.PlayerCount, 1, config.playerPrefabs.Length);
            for (int i = 0; i < count; i++)
            {
                GameObject player = Instantiate(config.playerPrefabs[i], spawn + new Vector3(-1.5f * i, 0f, 0f), Quaternion.identity);
                player.name = $"Player_{i + 1}";
                if (i == 0 && AutoPilot)
                    player.AddComponent<BotPilot>();
            }
            return count;
        }

        private static void SpawnMusic(LevelTheme theme)
        {
            if (theme.music == null)
                return;
            var go = new GameObject(AppBootstrap.MusicObjectName);
            var source = go.AddComponent<AudioSource>();
            source.clip = theme.music;
            source.loop = true;
            source.volume = Profile.Data.settings.musicVolume;
            source.Play();
        }
    }
}
