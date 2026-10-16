using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Demolition;
using Demolition.Generation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools ▸ Demolition ▸ Building Lab. Pick an archetype and a seed, see the building in
/// the open scene (weak points tinted red), read the validator's metrics, bake it to a
/// prefab, validate many seeds, or render a contact sheet. The static methods are the
/// entry points for agents and batch mode (<c>-executeMethod BuildingLab.ValidateAllCli</c>).
/// </summary>
public class BuildingLab : EditorWindow
{
    private const string ContentRoot = "Assets/Content";
    private const string PreviewName = "BuildingLab Preview";
    private static readonly Vector3 PreviewOrigin = new Vector3(-500f, 0f, 0f);

    private int _archetypeIndex;
    private BuildingTileset _tileset;
    private int _seed;
    private string _status = "";

    [MenuItem("Tools/Demolition/Building Lab")]
    private static void Open() => GetWindow<BuildingLab>("Building Lab");

    private void OnGUI()
    {
        string[] paths = ArchetypePaths();
        if (paths.Length == 0)
        {
            EditorGUILayout.HelpBox($"No archetypes under {ContentRoot}/*/Archetypes.", MessageType.Warning);
            return;
        }

        var names = new string[paths.Length];
        for (int i = 0; i < paths.Length; i++)
            names[i] = Path.GetFileNameWithoutExtension(paths[i]);
        _archetypeIndex = Mathf.Clamp(EditorGUILayout.Popup("Archetype", _archetypeIndex, names), 0, paths.Length - 1);
        if (_tileset == null)
            _tileset = DefaultTileset();
        _tileset = (BuildingTileset)EditorGUILayout.ObjectField("Tileset", _tileset, typeof(BuildingTileset), false);

        using (new EditorGUILayout.HorizontalScope())
        {
            _seed = EditorGUILayout.IntField("Seed", _seed);
            if (GUILayout.Button("◀", GUILayout.Width(28))) { _seed--; Preview(paths); }
            if (GUILayout.Button("▶", GUILayout.Width(28))) { _seed++; Preview(paths); }
            if (GUILayout.Button("Random", GUILayout.Width(64))) { _seed = Random.Range(0, 100000); Preview(paths); }
        }

        if (GUILayout.Button("Generate preview"))
            Preview(paths);
        if (GUILayout.Button("Bake preview to prefab"))
            _status = Bake(paths[_archetypeIndex], _seed, _tileset);
        if (GUILayout.Button("Validate 1000 seeds (this archetype)"))
            _status = Validate(paths[_archetypeIndex], 1000);
        if (GUILayout.Button("Render contact sheet (12 seeds)"))
            _status = "Saved " + RenderContactSheet(paths[_archetypeIndex], _seed, 12, _tileset);

        EditorGUILayout.HelpBox(_status, MessageType.None);
    }

    private void Preview(string[] paths)
    {
        _status = BuildPreview(paths[_archetypeIndex], _seed, _tileset, out GameObject root);
        if (root != null && SceneView.lastActiveSceneView != null)
        {
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView.FrameSelected();
        }
    }

    // ---------------- Entry points ----------------

    public static string[] ArchetypePaths()
    {
        var paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { ContentRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/Archetypes/") && path.EndsWith(".json"))
                paths.Add(path);
        }
        paths.Sort();
        return paths.ToArray();
    }

    public static BuildingArchetype Load(string path) =>
        BuildingArchetype.FromJson(AssetDatabase.LoadAssetAtPath<TextAsset>(path).text);

    public static BuildingTileset DefaultTileset()
    {
        string[] guids = AssetDatabase.FindAssets("t:BuildingTileset", new[] { ContentRoot });
        return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<BuildingTileset>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
    }

    /// <summary>Build a preview in the open scene, weak points tinted red. Returns the metrics.</summary>
    public static string BuildPreview(string archetypePath, int seed, BuildingTileset tileset, out GameObject root)
    {
        GameObject old = GameObject.Find(PreviewName);
        if (old != null)
            DestroyImmediate(old);

        BuildingArchetype archetype = Load(archetypePath);
        var watch = Stopwatch.StartNew();
        BuildingSpec spec = BuildingGrammar.GenerateValid(archetype, seed, out BuildingReport report);
        watch.Stop();

        root = new GameObject(PreviewName);
        DestructibleBuilding building = BuildingBuilder.Build(spec, tileset, PreviewOrigin, root.transform);
        TintWeakPoints(building.transform, spec, report);
        return $"{spec.Name}  {spec.Width}x{spec.Height}  {spec.Blocks.Count} blocks\n{report}\n{watch.ElapsedMilliseconds} ms";
    }

    public static string Bake(string archetypePath, int seed, BuildingTileset tileset)
    {
        BuildingSpec spec = BuildingGrammar.GenerateValid(Load(archetypePath), seed, out _);
        DestructibleBuilding building = BuildingBuilder.Build(spec, tileset, Vector3.zero);
        const string folder = "Assets/Prefabs/Generated";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Generated");
        string path = $"{folder}/{spec.Archetype.Replace('.', '_')}_{spec.Seed}.prefab";
        PrefabUtility.SaveAsPrefabAsset(building.gameObject, path);
        DestroyImmediate(building.gameObject);
        return "Baked " + path;
    }

    /// <summary>Generate <paramref name="seeds"/> buildings and summarise how many meet the
    /// archetype's targets. Also written to Temp/building-validation.txt.</summary>
    public static string Validate(string archetypePath, int seeds)
    {
        BuildingArchetype archetype = Load(archetypePath);
        int failures = 0, unsupported = 0, weakPoints = 0, attempts = 0;
        float cascade = 0f;
        var failedSeeds = new List<int>();
        var watch = Stopwatch.StartNew();
        for (int seed = 0; seed < seeds; seed++)
        {
            BuildingGrammar.GenerateValid(archetype, seed, out BuildingReport report);
            if (!report.MeetsTargets)
            {
                failures++;
                if (failedSeeds.Count < 10)
                    failedSeeds.Add(seed);
            }
            if (!report.Supported)
                unsupported++;
            weakPoints += report.WeakPoints.Count;
            attempts += report.Attempts;
            cascade += report.MaxCascade;
        }
        watch.Stop();
        string summary = $"{archetype.id}: {seeds} seeds, {failures} failed (first: {string.Join(",", failedSeeds)}), " +
                         $"{unsupported} unsupported, avg weak points {(float)weakPoints / seeds:0.0}, " +
                         $"avg max cascade {cascade / seeds:P0}, avg attempts {(float)attempts / seeds:0.0}, " +
                         $"{watch.ElapsedMilliseconds / (float)seeds:0.0} ms/building";
        File.AppendAllText("Temp/building-validation.txt", summary + "\n");
        return summary;
    }

    /// <summary>For batch mode: validate every archetype, exit code 1 on any failure.</summary>
    public static void ValidateAllCli()
    {
        File.WriteAllText("Temp/building-validation.txt", "");
        bool ok = true;
        foreach (string path in ArchetypePaths())
            ok &= Validate(path, 1000).Contains(" 0 failed");
        EditorApplication.Exit(ok ? 0 : 1);
    }

    /// <summary>Render a grid of consecutive seeds to a PNG in Temp/ (for reviewing the look).</summary>
    public static string RenderContactSheet(string archetypePath, int firstSeed, int count, BuildingTileset tileset)
    {
        const int cell = 384, columns = 4;
        int rows = Mathf.CeilToInt(count / (float)columns);
        BuildingArchetype archetype = Load(archetypePath);
        var sheet = new Texture2D(cell * columns, cell * rows, TextureFormat.RGB24, false);
        var rt = new RenderTexture(cell, cell, 24);
        var camGo = new GameObject("ContactSheetCam");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.85f, 0.9f);
        cam.targetTexture = rt;

        for (int i = 0; i < count; i++)
        {
            BuildingSpec spec = BuildingGrammar.GenerateValid(archetype, firstSeed + i, out BuildingReport report);
            DestructibleBuilding building = BuildingBuilder.Build(spec, tileset, PreviewOrigin + Vector3.down * 500f);
            TintWeakPoints(building.transform, spec, report);
            float extent = Mathf.Max(spec.Width, spec.Height) * 0.5f + 1f;
            cam.orthographicSize = extent;
            cam.transform.position = PreviewOrigin + Vector3.down * 500f + new Vector3(spec.Width * 0.5f, spec.Height * 0.5f, -50f);
            cam.Render();
            RenderTexture.active = rt;
            sheet.ReadPixels(new Rect(0, 0, cell, cell), (i % columns) * cell, (rows - 1 - i / columns) * cell);
            RenderTexture.active = null;
            DestroyImmediate(building.gameObject);
        }
        sheet.Apply();
        string path = $"Temp/building-sheet-{archetype.id}.png";
        File.WriteAllBytes(path, sheet.EncodeToPNG());
        DestroyImmediate(camGo);
        DestroyImmediate(rt);
        DestroyImmediate(sheet);
        return path;
    }

    private static void TintWeakPoints(Transform building, BuildingSpec spec, BuildingReport report)
    {
        for (int i = 0; i < spec.Blocks.Count && i < building.childCount; i++)
        {
            if (!report.WeakPoints.Contains(spec.Blocks[i]))
                continue;
            foreach (SpriteRenderer sr in building.GetChild(i).GetComponentsInChildren<SpriteRenderer>())
                sr.color = new Color(1f, 0.35f, 0.35f);
        }
    }
}
