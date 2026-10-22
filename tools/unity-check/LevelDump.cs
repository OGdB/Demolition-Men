using System.Collections.Generic;
using System.IO;
using System.Text;
using Demolition.Generation;
using Demolition.Levels;

/// <summary>
/// `ModelTests.dll dump-levels <out-dir> [level-id...]`: every level spec under
/// Assets/Content as the layout the game generates from it (ground, buildings block by
/// block, pickups, segments), one JSON per level, for tools/artpipe/level_preview.py.
/// Run from the Unity project folder, like the tests.
/// </summary>
public static class LevelDump
{
    public static int Run(string[] args)
    {
        string outDir = args.Length > 1 ? args[1] : "content-work/levels";
        var only = new HashSet<string>();
        for (int i = 2; i < args.Length; i++)
            only.Add(args[i]);
        Directory.CreateDirectory(outDir);
        foreach (string path in Directory.GetFiles("Assets/Content", "*.json", SearchOption.AllDirectories))
        {
            if (!path.Replace('\\', '/').Contains("/Levels/"))
                continue;
            LevelSpec spec = LevelSpec.FromJson(File.ReadAllText(path));
            if (only.Count > 0 && !only.Contains(spec.id))
                continue;
            var archetypes = new List<BuildingArchetype>();
            string theme = Path.GetDirectoryName(Path.GetDirectoryName(path));
            foreach (string json in Directory.GetFiles(Path.Combine(theme, "Archetypes"), "*.json"))
                archetypes.Add(BuildingArchetype.FromJson(File.ReadAllText(json)));
            LevelLayout layout = LevelGrammar.Generate(spec, archetypes, JumpProfile.Default, out TraversalReport report);
            File.WriteAllText(Path.Combine(outDir, spec.id + ".json"), Json(layout, report.Passable));
            System.Console.WriteLine($"{spec.id}: {layout.Width} columns, {layout.Buildings.Count} buildings, {layout.TotalCells} cells, par {layout.Par:0}s, passable {report.Passable}");
        }
        return 0;
    }

    private static string Json(LevelLayout layout, bool passable)
    {
        var s = new StringBuilder();
        LevelSpec spec = layout.Spec;
        s.Append($"{{\"id\":\"{spec.id}\",\"name\":\"{spec.name}\",\"theme\":\"{spec.theme}\",\"seed\":{spec.seed},");
        s.Append($"\"scrollSpeed\":{F(spec.scrollSpeed)},\"par\":{F(layout.Par)},\"passable\":{(passable ? "true" : "false")},");
        s.Append($"\"spawnX\":{layout.SpawnX},\"finishX\":{layout.FinishX},\"caravanX\":{layout.CaravanX},\"cells\":{layout.TotalCells},\"ground\":[");
        for (int x = 0; x < layout.Width; x++)
            s.Append(x > 0 ? "," : "").Append(layout.Ground[x] == LevelLayout.NoGround ? "null" : layout.Ground[x].ToString());
        s.Append("],\"segments\":[");
        for (int i = 0; i < layout.Segments.Count; i++)
        {
            LevelSegment seg = layout.Segments[i];
            s.Append(i > 0 ? "," : "").Append($"{{\"kind\":\"{seg.Kind}\",\"x0\":{seg.X0},\"x1\":{seg.X1}}}");
        }
        s.Append("],\"buildings\":[");
        for (int i = 0; i < layout.Buildings.Count; i++)
        {
            BuildingPlacement b = layout.Buildings[i];
            s.Append(i > 0 ? "," : "").Append($"{{\"archetype\":\"{b.Spec.Archetype}\",\"x\":{b.X},\"y\":{b.GroundY},\"w\":{b.Spec.Width},\"h\":{b.Spec.Height},\"blocks\":[");
            for (int k = 0; k < b.Spec.Blocks.Count; k++)
            {
                SpecBlock block = b.Spec.Blocks[k];
                s.Append(k > 0 ? "," : "").Append($"[\"{block.Kind}\",{block.Rect.x},{block.Rect.y},{block.Rect.width},{block.Rect.height},{block.Variant},{(TileKinds.WalkThrough(block.Kind) ? 1 : 0)}]");
            }
            s.Append("],\"interior\":[");
            List<InteriorCell> interior = b.Spec.Interior();
            for (int k = 0; k < interior.Count; k++)
                s.Append(k > 0 ? "," : "").Append($"[{interior[k].Cell.x},{interior[k].Cell.y},{(interior[k].UnderCeiling ? 1 : 0)}]");
            s.Append("]}");
        }
        s.Append("],\"decor\":[");
        for (int i = 0; i < layout.Decor.Count; i++)
        {
            DecorPlacement d = layout.Decor[i];
            s.Append(i > 0 ? "," : "").Append($"[\"{d.Kind}\",{d.X},{d.GroundY},{d.Variant}]");
        }
        s.Append("],\"pickups\":[");
        for (int i = 0; i < layout.Pickups.Count; i++)
        {
            PickupPlacement p = layout.Pickups[i];
            s.Append(i > 0 ? "," : "").Append($"{{\"kind\":\"{p.Kind}\",\"x\":{F(p.X)},\"y\":{F(p.Y)}}}");
        }
        s.Append("]}");
        return s.ToString();
    }

    private static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
