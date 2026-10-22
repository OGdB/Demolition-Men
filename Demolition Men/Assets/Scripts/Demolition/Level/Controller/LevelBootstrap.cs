using System.Collections;
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

            ScrollCamera scroll = SpawnCamera(config, theme, spec, spawn, ground);
            SpawnBackdrop(config, theme, spawn.x, ground);
            int players = NetRole.IsOnline ? NetRole.SpawnPlayers?.Invoke(spawn) ?? 0 : SpawnPlayers(config, spawn);
            UiFactory.EnsureEventSystem();

            var level = new GameObject("Level").AddComponent<LevelBuilder>();
            level.Build(layout, theme, config.pickupPrefab, config.caravan, players > 1 || NetRole.IsOnline);
            if (scroll != null)
                scroll.LowestGround = level.LowestGroundBetween; // show trench floors before the drop
            NetRole.LevelBuilt?.Invoke(layout);

            // Online the players arrive over the network; the run finds them by name when it wakes.
            if (NetRole.IsOnline)
                StartCoroutine(StartRunWhenPlayersExist(config, spec, layout));
            else
                StartRun(config, spec, layout);

            SpawnMusic(theme);
        }

        private static void StartRun(GameConfig config, LevelSpec spec, LevelLayout layout)
        {
            var run = Instantiate(config.runPrefab).GetComponent<RunController>();
            run.name = "Run";
            run.Configure(spec.id, layout.Par, layout.TotalCells, LevelSelection.NextAction());
        }

        private static IEnumerator StartRunWhenPlayersExist(GameConfig config, LevelSpec spec, LevelLayout layout)
        {
            while (GameObject.Find("Player_1") == null || GameObject.Find("Player_2") == null)
                yield return null;
            StartRun(config, spec, layout);
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
        // covered however far it has drifted before the next wrap.
        private static void SpawnBackdropLayer(BackdropLayer layer, Transform root, float x, float ground, int order)
        {
            if (layer.sprite == null)
                return;
            var go = new GameObject(layer.sprite.name, typeof(SpriteRenderer));
            go.transform.SetParent(root, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = layer.sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            Vector2 tile = layer.sprite.bounds.size;
            float period = Mathf.Ceil(BackdropPeriod / tile.x) * tile.x;
            sr.size = new Vector2(period * 4f, tile.y);
            sr.sortingOrder = order;
            go.transform.position = new Vector3(x, ground + layer.bottom + tile.y * 0.5f, 0f);
            go.AddComponent<ParallaxLayer>().Configure(layer.parallax, period);
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
