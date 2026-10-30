using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
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

        if (!development)
            HardenForRelease();

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

    /// <summary>
    /// Pins the settings that make a shipped release hard to reverse-engineer, so a release
    /// upload can never silently regress. IL2CPP compiles the C# to C++ and then to native
    /// ARM64 machine code: the AAB carries no managed IL for a .NET decompiler (dnSpy, ILSpy,
    /// dotPeek) to read back into source. Managed stripping then drops unused code, shrinking
    /// what little metadata survives. This raises the cost of decompiling the app; it does not
    /// make it impossible, so nothing secret (server keys, scoring authority) belongs in the
    /// client — ranked scores are verified server-side by replaying the run log.
    ///
    /// Stripping stays at Medium: High can drop types reached only through reflection or
    /// JsonUtility (the level, archetype and look JSON), which would need a link.xml and a
    /// full on-device retest before it's safe to raise.
    /// </summary>
    private static void HardenForRelease()
    {
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
        PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Release);
    }
}
