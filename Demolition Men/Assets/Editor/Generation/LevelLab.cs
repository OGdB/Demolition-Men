using System.Collections.Generic;
using System.IO;
using System.Text;
using Demolition;
using Demolition.Generation;
using Demolition.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools ▸ Demolition ▸ Level Lab. Pick a level spec and a seed, see the generated layout
/// (ground, buildings by material, pickups, spawn and finish), validate many seeds, or
/// play it — optionally with the bot. <c>-executeMethod LevelLab.ValidateAllCli</c>
/// validates every level in batch mode and writes Temp/level-validation.txt.
/// </summary>
public class LevelLab : EditorWindow
{
    private const string ContentRoot = "Assets/Content";
    private const int Pixel = 4;

    private int _levelIndex;
    private int _seed;
    private string _loadedPath;
    private Texture2D _preview;
    private string _status = "";
    private Vector2 _scroll;

    [MenuItem("Tools/Demolition/Level Lab")]
    private static void Open() => GetWindow<LevelLab>("Level Lab");

    private void OnGUI()
    {
        string[] paths = LevelPaths();
        if (paths.Length == 0)
        {
            EditorGUILayout.HelpBox($"No level specs under {ContentRoot}/*/Levels.", MessageType.Warning);
            return;
        }
        var names = new string[paths.Length];
        for (int i = 0; i < paths.Length; i++)
            names[i] = Path.GetFileNameWithoutExtension(paths[i]);
        int index = Mathf.Clamp(EditorGUILayout.Popup("Level", _levelIndex, names), 0, paths.Length - 1);
        string path = paths[index];
        if (index != _levelIndex || _loadedPath != path)
        {
            _levelIndex = index;
            _loadedPath = path;
            _seed = Load(path).seed;
            Generate(path);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            int seed = EditorGUILayout.IntField("Seed", _seed);
            if (GUILayout.Button("◀", GUILayout.Width(28))) seed--;
            if (GUILayout.Button("▶", GUILayout.Width(28))) seed++;
            if (seed != _seed)
            {
                _seed = seed;
                Generate(path);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Validate 100 seeds"))
                _status = Validate(path, 100);
            if (GUILayout.Button("Play"))
                Play(path, false);
            if (GUILayout.Button("Play with bot"))
                Play(path, true);
        }

        EditorGUILayout.HelpBox(_status, MessageType.None);
        if (_preview == null)
            return;
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        Rect rect = GUILayoutUtility.GetRect(_preview.width, _preview.height, GUILayout.ExpandWidth(false));
        GUI.DrawTexture(rect, _preview);
        EditorGUILayout.EndScrollView();
    }

    private static string[] LevelPaths()
    {
        var list = new List<string>();
        if (!Directory.Exists(ContentRoot))
            return list.ToArray();
        foreach (string theme in Directory.GetDirectories(ContentRoot))
        {
            string levels = Path.Combine(theme, "Levels");
            if (Directory.Exists(levels))
                list.AddRange(Directory.GetFiles(levels, "*.json"));
        }
        list.Sort();
        return list.ToArray();
    }

    private static LevelSpec Load(string path) => LevelSpec.FromJson(File.ReadAllText(path));

    private static List<BuildingArchetype> Archetypes(string levelPath)
    {
        string folder = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(levelPath)), "Archetypes");
        var list = new List<BuildingArchetype>();
        if (Directory.Exists(folder))
            foreach (string json in Directory.GetFiles(folder, "*.json"))
                list.Add(BuildingArchetype.FromJson(File.ReadAllText(json)));
        return list;
    }

    private void Generate(string path)
    {
        LevelSpec spec = Load(path).WithSeed(_seed, Load(path).id);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        LevelLayout layout = LevelGrammar.Generate(spec, Archetypes(path), JumpProfile.Default, out TraversalReport report);
        watch.Stop();
        _preview = Render(layout);
        _status = $"{spec.id} seed {_seed}: {layout.Width} columns, {layout.Buildings.Count} buildings, {layout.TotalCells} cells, "
                  + $"{layout.Pickups.Count} pickups, {layout.Attempts} attempt(s), {watch.ElapsedMilliseconds} ms\n{report}";
    }

    private static Texture2D Render(LevelLayout layout)
    {
        int top = 4;
        foreach (BuildingPlacement b in layout.Buildings)
            top = Mathf.Max(top, b.GroundY + b.Spec.Height + 2);
        int bottom = layout.LowestGround - 3;
        int width = layout.Width, height = top - bottom;
        var tex = new Texture2D(width * Pixel, height * Pixel, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var pixels = new Color[tex.width * tex.height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(0.55f, 0.75f, 0.9f);

        void Cell(int x, int y, Color c)
        {
            int row = y - bottom;
            if (x < 0 || x >= width || row < 0 || row >= height)
                return;
            for (int py = 0; py < Pixel; py++)
                for (int px = 0; px < Pixel; px++)
                    pixels[(row * Pixel + py) * tex.width + x * Pixel + px] = c;
        }

        for (int x = 0; x < width; x++)
        {
            int g = layout.Ground[x];
            if (g == LevelLayout.NoGround)
                continue;
            for (int y = bottom; y < g; y++)
                Cell(x, y, y == g - 1 ? new Color(0.35f, 0.35f, 0.38f) : new Color(0.2f, 0.18f, 0.16f));
        }
        foreach (BuildingPlacement b in layout.Buildings)
            foreach (SpecBlock block in b.Spec.Blocks)
                foreach (Vector2Int c in block.Cells())
                    Cell(b.X + c.x, b.GroundY + c.y, BlockMaterialInfo.Tint(block.Material));
        foreach (PickupPlacement p in layout.Pickups)
            Cell(Mathf.FloorToInt(p.X), Mathf.FloorToInt(p.Y), p.Kind == PickupKind.Dynamite ? Color.red : Color.yellow);
        Cell(layout.SpawnX, layout.GroundAt(layout.SpawnX), Color.green);
        Cell(layout.SpawnX, layout.GroundAt(layout.SpawnX) + 1, Color.green);
        for (int y = 0; y < 6; y++)
            Cell(layout.FinishX, layout.GroundAt(layout.FinishX) + y, y % 2 == 0 ? Color.white : Color.black);

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    public static string Validate(string path, int seeds)
    {
        LevelSpec spec = Load(path);
        List<BuildingArchetype> archetypes = Archetypes(path);
        int failures = 0, cells = 0, retries = 0;
        string firstFailure = null;
        for (int seed = 0; seed < seeds; seed++)
        {
            LevelLayout layout = LevelGrammar.Generate(spec.WithSeed(spec.seed + seed, spec.id), archetypes, JumpProfile.Default,
                out TraversalReport report);
            cells += layout.TotalCells;
            retries += layout.Attempts - 1;
            if (report.Passable)
                continue;
            failures++;
            firstFailure ??= $"seed {spec.seed + seed}: {report}";
        }
        return $"{spec.id}: {seeds - failures}/{seeds} passable, avg {cells / Mathf.Max(1, seeds)} cells, {retries} re-rolls"
               + (firstFailure != null ? $"\nfirst failure: {firstFailure}" : string.Empty);
    }

    private void Play(string path, bool bot)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        PlayerPrefs.SetString(LevelSelection.EditorRequestKey, $"{Load(path).id}|{_seed}|{(bot ? "bot" : "")}");
        PlayerPrefs.Save();
        EditorSceneManager.OpenScene("Assets/Scenes/Run.unity");
        EditorApplication.isPlaying = true;
    }

    /// <summary>Batch mode: validate every level spec over 50 seeds; non-zero exit on any failure.</summary>
    public static void ValidateAllCli()
    {
        var report = new StringBuilder();
        bool ok = true;
        foreach (string path in LevelPaths())
        {
            string line = Validate(path, 50);
            ok &= !line.Contains("first failure");
            report.AppendLine(line);
        }
        Directory.CreateDirectory("Temp");
        File.WriteAllText("Temp/level-validation.txt", report.ToString());
        Debug.Log(report.ToString());
        if (Application.isBatchMode)
            EditorApplication.Exit(ok ? 0 : 1);
    }
}
