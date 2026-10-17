using UnityEditor;
using UnityEngine;

/// <summary>
/// Import rules for generated art (roadmap §6): anything under
/// <c>Assets/Content/&lt;Theme&gt;/Sprites</c> imports as a single pixel-art sprite at
/// 32 pixels per unit, point-filtered, uncompressed, no mipmaps, pivot centre — whatever
/// .meta it arrived with.
/// </summary>
public class ContentSpriteImporter : AssetPostprocessor
{
    public const int PixelsPerUnit = 32;

    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.StartsWith("Assets/Content/") || !path.Contains("/Sprites/"))
            return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }
}
