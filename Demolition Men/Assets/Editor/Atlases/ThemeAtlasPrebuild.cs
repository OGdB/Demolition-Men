// [UNITY-SKILL:SPRITEATLAS]
// [TYPE:PREBUILD]
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// Before every build: one sprite atlas per theme (<c>Assets/Content/&lt;Theme&gt;/&lt;Theme&gt;Atlas.spriteatlasv2</c>,
/// shipped in the build) with the theme's generated sprites, so a street's blocks, back walls,
/// props and dressing draw from one texture and batch instead of costing a draw call each.
/// Town's also holds the hand-drawn object set its tileset borrows from. Backdrops stay out:
/// they draw tiled. Pixel art: point filter, no mipmaps, uncompressed.
/// </summary>
public class ThemeAtlasPrebuild : IPreprocessBuildWithReport
{
    private const string ContentRoot = "Assets/Content";
    private const string ObjectSet = "Assets/Sprites/Demolition men object set.png";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        // V2 packing must be on, or the atlases import and look fine but never pack.
        EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
        SpritePackerMode mode = EditorSettings.spritePackerMode;
        if (mode != SpritePackerMode.SpriteAtlasV2)
            Debug.LogError($"[SpriteAtlas] Sprite packer mode is {mode}, not SpriteAtlasV2: the theme atlases won't pack.");
        else
            Debug.Log($"[SpriteAtlas] Sprite packer mode: {mode}");

        foreach (string theme in AssetDatabase.GetSubFolders(ContentRoot))
        {
            string sprites = theme + "/Sprites";
            if (!AssetDatabase.IsValidFolder(sprites))
                continue;
            string name = Path.GetFileName(theme);
            List<string> textures = AssetDatabase.FindAssets("t:Texture2D", new[] { sprites })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.StartsWith("Assets/") && !Path.GetFileName(path).StartsWith("backdrop_"))
                .ToList();
            if (name == "Town")
                textures.Add(ObjectSet);
            Write($"{theme}/{name}Atlas.spriteatlasv2", textures);
        }
        AssetDatabase.SaveAssets();
    }

    private static void Write(string atlasPath, List<string> texturePaths)
    {
        Object[] packables = texturePaths
            .Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
            .Where(texture => texture != null)
            .Cast<Object>()
            .ToArray();
        if (packables.Length == 0)
            return;

        SpriteAtlasAsset atlas;
        if (File.Exists(atlasPath))
        {
            atlas = SpriteAtlasAsset.Load(atlasPath);
            atlas.Remove(AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath).GetPackables());
        }
        else
        {
            atlas = new SpriteAtlasAsset();
        }
        atlas.Add(packables);
        SpriteAtlasAsset.Save(atlas, atlasPath);
        AssetDatabase.ImportAsset(atlasPath);

        var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(atlasPath);
        importer.includeInBuild = true;
        SpriteAtlasTextureSettings texture = importer.textureSettings;
        texture.filterMode = FilterMode.Point;
        texture.generateMipMaps = false;
        texture.readable = false;
        importer.textureSettings = texture;
        SpriteAtlasPackingSettings packing = importer.packingSettings;
        packing.padding = 2;
        packing.enableRotation = false;
        packing.enableTightPacking = false; // full-rect sprites: tiles and back walls fill their cell
        packing.enableAlphaDilation = false;
        importer.packingSettings = packing;
        importer.SetPlatformSettings(new TextureImporterPlatformSettings
        {
            name = "DefaultTexturePlatform",
            maxTextureSize = 2048,
            textureCompression = TextureImporterCompression.Uncompressed, // compression smears pixel art
        });
        importer.SaveAndReimport();
        Debug.Log($"[SpriteAtlas] {atlasPath}: {packables.Length} textures");
    }
}
