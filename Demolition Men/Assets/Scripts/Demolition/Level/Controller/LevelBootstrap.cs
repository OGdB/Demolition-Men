using Demolition.Meta;
using Demolition.Run;
using UnityEngine;

namespace Demolition.Levels
{
    /// <summary>
    /// The run scene's only authored object. Generates the selected level from its spec
    /// and seed, then assembles the scene from <see cref="GameConfig"/>: camera, backdrop,
    /// players (named Player_1 / Player_2 for the run), the level itself, the run and
    /// the music. Order matters: the run finds the camera and players when it wakes.
    /// </summary>
    public sealed class LevelBootstrap : MonoBehaviour
    {
        /// <summary>Backdrop height above the street, as authored for Level 1.</summary>
        private const float BackdropHeight = 12.6f;
        /// <summary>Ground visible below the players' feet at the start.</summary>
        private const float GroundShown = 0.5f;

        /// <summary>Let the <see cref="BotPilot"/> play player 1 (PlayMode tests, the
        /// Tools ▸ Demolition ▸ Bot Run menu).</summary>
        public static bool AutoPilot;

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

            SpawnCamera(config, theme, spec, spawn, ground);
            SpawnBackdrop(config, theme, spawn.x, ground);
            int players = SpawnPlayers(config, spawn);
            UiFactory.EnsureEventSystem();

            var level = new GameObject("Level").AddComponent<LevelBuilder>();
            level.Build(layout, theme, config.pickupPrefab, players > 1);

            var run = Instantiate(config.runPrefab).GetComponent<RunController>();
            run.name = "Run";
            run.Configure(spec.id, layout.Par, layout.TotalCells, LevelSelection.NextAction());

            SpawnMusic(theme);
        }

        private static void SpawnCamera(GameConfig config, LevelTheme theme, LevelSpec spec, Vector3 spawn, float ground)
        {
            GameObject go = Instantiate(config.cameraPrefab);
            go.name = "Main Camera 1";
            var cam = go.GetComponent<Camera>();
            float size = cam.orthographicSize;
            // Placed before the scroll camera reads its floor: the street sits at the bottom.
            go.transform.position = new Vector3(spawn.x + size * 0.6f, ground - GroundShown + size, -10f);
            cam.backgroundColor = theme.sky;
            var scroll = go.GetComponent<ScrollCamera>();
            if (scroll != null)
            {
                scroll.ScrollSpeed = spec.scrollSpeed;
                scroll.ResetFloor();
            }
        }

        private static void SpawnBackdrop(GameConfig config, LevelTheme theme, float x, float ground)
        {
            if (config.backdropPrefab == null)
                return;
            GameObject backdrop = Instantiate(config.backdropPrefab, new Vector3(x, ground + BackdropHeight, 0f), Quaternion.identity);
            foreach (SpriteRenderer sr in backdrop.GetComponentsInChildren<SpriteRenderer>())
                sr.color *= theme.backdropTint;
        }

        private static int SpawnPlayers(GameConfig config, Vector3 spawn)
        {
            int count = Mathf.Clamp(Session.PlayerCount, 1, config.playerPrefabs.Length);
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
