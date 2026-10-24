using System.Collections;
using System.Collections.Generic;
using Demolition.Levels;
using Demolition.Meta;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Demolition.Lighting
{
    /// <summary>
    /// URP 2D light and post-processing for every scene, built in code (docs/rendering.md): a
    /// global light for the ambient (the level theme's in a run, plain daylight elsewhere), a
    /// global volume with bloom, vignette and a little contrast, point lights on lamp posts and
    /// neon signs, a flash for every blast, and headlamps where the theme is dark. The EFFECTS
    /// setting turns everything but a plain daylight ambient off, for slow phones.
    /// </summary>
    public sealed class SceneLighting : MonoBehaviour
    {
        private static readonly Color LampColor = new Color(1f, 0.82f, 0.55f);
        private static readonly Color FlashColor = new Color(1f, 0.7f, 0.35f);
        private static readonly Color HeadlampColor = new Color(1f, 0.96f, 0.85f);
        private static readonly string[] PlayerNames = { "Player_1", "Player_2" };
        private static VolumeProfile _profile;

        private LevelTheme _theme;
        private Light2D _ambient;
        private Volume _volume;
        private Camera _camera;
        private float _nextPlayerSearch;
        // Lamps, signs and headlamps: off with the effects.
        private readonly List<Light2D> _points = new List<Light2D>();
        private readonly HashSet<GameObject> _lamped = new HashSet<GameObject>();

        private static bool Effects => Profile.Data.settings.effects;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single); // the first scene loaded before this ran
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single && FindAnyObjectByType<SceneLighting>() == null)
                new GameObject("SceneLighting").AddComponent<SceneLighting>();
        }

        private void Awake()
        {
            // Only a run has a level theme; menus and the tutorial keep daylight.
            _theme = FindAnyObjectByType<LevelBootstrap>() != null ? LevelSelection.Theme : null;

            _ambient = gameObject.AddComponent<Light2D>();
            _ambient.lightType = Light2D.LightType.Global;
            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.sharedProfile = PostProfile();

            LevelBuilder.DecorSpawned += OnDecor;
            Dynamite.Exploded += OnExploded;
            Profile.SettingsChanged += Apply;
            Apply();
        }

        private void OnDestroy()
        {
            LevelBuilder.DecorSpawned -= OnDecor;
            Dynamite.Exploded -= OnExploded;
            Profile.SettingsChanged -= Apply;
        }

        private void Update()
        {
            if (Camera.main != _camera)
                Apply(); // the Level scene's camera arrives after this
            if (_theme != null && _theme.headlamps && Time.time >= _nextPlayerSearch)
            {
                _nextPlayerSearch = Time.time + 1f;
                foreach (string name in PlayerNames)
                {
                    GameObject player = GameObject.Find(name);
                    if (player != null && _lamped.Add(player))
                        AddPoint(player.transform, new Vector3(0f, 1.55f, 0f), HeadlampColor, 0.9f, _theme.headlampRange);
                }
            }
        }

        private void Apply()
        {
            bool on = Effects;
            _ambient.color = on && _theme != null ? _theme.ambient : Color.white;
            _ambient.intensity = on && _theme != null ? _theme.ambientIntensity : 1f;
            _volume.enabled = on;
            _points.RemoveAll(light => light == null); // streamed out with their decor
            foreach (Light2D light in _points)
                light.enabled = on;
            _camera = Camera.main;
            if (_camera != null)
                _camera.GetUniversalAdditionalCameraData().renderPostProcessing = on; // adds the data if missing
        }

        private void OnDecor(GameObject decor, DecorKind kind)
        {
            if (_theme == null || !decor.TryGetComponent(out SpriteRenderer sr))
                return;
            if (kind == DecorKind.Lamp && _theme.lampLight > 0f)
                AddPoint(decor.transform, new Vector3(0f, sr.bounds.max.y - decor.transform.position.y - 0.4f, 0f), LampColor, _theme.lampLight, 4.5f);
            else if (kind == DecorKind.Sign && _theme.signLight > 0f)
                AddPoint(decor.transform, sr.bounds.center - decor.transform.position, _theme.signColor, _theme.signLight, 3f);
        }

        private void OnExploded(Vector3 position)
        {
            if (!Effects)
                return;
            var go = new GameObject("BlastFlash");
            go.transform.position = position;
            Light2D flash = Point(go.transform, FlashColor, 3f, 7f);
            StartCoroutine(Fade(flash, 0.4f));
        }

        private static IEnumerator Fade(Light2D flash, float seconds)
        {
            float start = flash.intensity;
            for (float t = 0f; t < seconds && flash != null; t += Time.deltaTime)
            {
                flash.intensity = start * (1f - t / seconds);
                yield return null;
            }
            if (flash != null)
                Destroy(flash.gameObject);
        }

        private void AddPoint(Transform parent, Vector3 local, Color color, float intensity, float radius)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            Light2D light = Point(go.transform, color, intensity, radius);
            light.enabled = Effects;
            _points.Add(light);
        }

        private static Light2D Point(Transform at, Color color, float intensity, float radius)
        {
            var light = at.gameObject.AddComponent<Light2D>(); // a point light unless told otherwise
            light.color = color;
            light.intensity = intensity;
            light.pointLightOuterRadius = radius;
            light.pointLightInnerRadius = radius * 0.15f;
            light.falloffIntensity = 0.6f;
            return light;
        }

        // Built once in code, so there is no profile asset to keep in step: a soft glow on what
        // the lights push past white, a dark edge, a touch more punch. No tonemapping: it would
        // shift the pixel-art palette.
        private static VolumeProfile PostProfile()
        {
            if (_profile != null)
                return _profile;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = _profile.Add<Bloom>();
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.6f);
            var vignette = _profile.Add<Vignette>();
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);
            var grade = _profile.Add<ColorAdjustments>();
            grade.contrast.Override(8f);
            grade.saturation.Override(10f);
            return _profile;
        }
    }
}
