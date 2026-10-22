using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds the Android App Bundle for Play into <c>Builds/Android/</c>, raising the
/// versionCode first (Play needs a higher one per upload). The upload keystore comes from
/// environment variables, so no password ever sits in the project: <c>DEMOLITION_KEYSTORE</c>
/// (path), <c>DEMOLITION_KEYSTORE_PASS</c>, <c>DEMOLITION_KEYALIAS</c>, <c>DEMOLITION_KEYALIAS_PASS</c>.
/// Without them the bundle is debug-signed: fine on your own phone, refused by Play.
/// </summary>
public static class AndroidBuild
{
    [MenuItem("Tools/Demolition/Build/Android AAB (release)")]
    public static void Release() => Build(false);

    [MenuItem("Tools/Demolition/Build/Android AAB (development)")]
    public static void Development() => Build(true);

    /// <summary>Batch mode: <c>-buildTarget Android -executeMethod AndroidBuild.ReleaseCli</c>.</summary>
    public static void ReleaseCli()
    {
        if (!Build(false))
            EditorApplication.Exit(1);
    }

    private static bool Build(bool development)
    {
        string keystore = Environment.GetEnvironmentVariable("DEMOLITION_KEYSTORE");
        bool signed = !string.IsNullOrEmpty(keystore);
        if (!signed && !development)
            Debug.LogWarning("DEMOLITION_KEYSTORE not set: the bundle is debug-signed and Play will refuse it.");
        PlayerSettings.Android.useCustomKeystore = signed;
        if (signed)
        {
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("DEMOLITION_KEYSTORE_PASS");
            PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("DEMOLITION_KEYALIAS");
            PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("DEMOLITION_KEYALIAS_PASS");
        }

        int code = ++PlayerSettings.Android.bundleVersionCode;
        EditorUserBuildSettings.buildAppBundle = true;
        string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds", "Android");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"DemolitionMen-{PlayerSettings.bundleVersion}-{code}{(development ? "-dev" : "")}.aab");

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = path,
            target = BuildTarget.Android,
            options = development ? BuildOptions.Development : BuildOptions.None,
        });

        // The keystore path and alias would otherwise be serialized into ProjectSettings.
        PlayerSettings.Android.useCustomKeystore = false;
        PlayerSettings.Android.keystoreName = "";
        PlayerSettings.Android.keyaliasName = "";
        AssetDatabase.SaveAssets();

        bool ok = report.summary.result == BuildResult.Succeeded;
        if (ok)
            Debug.Log($"Android AAB {path} ({report.summary.totalSize / (1024 * 1024)} MB, versionCode {code})");
        else
            Debug.LogError($"Android AAB failed: {report.summary.result}, {report.summary.totalErrors} errors");
        return ok;
    }
}
