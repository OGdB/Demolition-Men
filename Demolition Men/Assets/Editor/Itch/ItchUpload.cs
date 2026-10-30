using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Sends the WebGL build to itch.io with butler: Tools ▸ Demolition ▸ Build ▸ WebGL to itch.io.
/// It builds into <c>Builds/WebGL/</c> first, unless that build was made from exactly what is
/// in the project now. Butler is installed by hand and logged in once (<c>butler login</c>,
/// or <c>BUTLER_API_KEY</c>); the itch.io project is kept per user in <c>UserSettings</c>.
/// </summary>
public class ItchUpload : EditorWindow
{
    private const string Channel = "html";
    private const string ProjectKey = "Demolition.ItchProject";
    private const string ButlerPage = "https://itchio.itch.io/butler";

    private static string Root => Path.GetDirectoryName(Application.dataPath);
    private static string BuildDir => Path.Combine(Root, "Builds", "WebGL");
    private static string StampPath => Path.Combine(Root, "Builds", "WebGL.stamp");

    private static Process _push;
    private static int _progress;

    private string _project;
    private string _butler;
    private bool _loggedIn;
    private bool _current;

    [MenuItem("Tools/Demolition/Build/WebGL to itch.io")]
    private static void Open() => GetWindow<ItchUpload>("itch.io").minSize = new Vector2(420f, 170f);

    private void OnEnable()
    {
        _project = EditorUserSettings.GetConfigValue(ProjectKey) ?? "";
        Refresh();
    }

    private void OnFocus() => Refresh();

    private void Refresh()
    {
        _butler = FindButler();
        _loggedIn = LoggedIn();
        _current = BuildIsCurrent();
    }

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        _project = EditorGUILayout.TextField("Project (user/game)", _project).Trim();
        if (EditorGUI.EndChangeCheck())
            EditorUserSettings.SetConfigValue(ProjectKey, _project);

        bool validProject = Regex.IsMatch(_project, @"^[\w-]+/[\w-]+$");
        if (!validProject)
            EditorGUILayout.HelpBox("Needs your itch.io user name as well: the page https://user.itch.io/game is user/game.", MessageType.Warning);
        if (_butler == null)
        {
            EditorGUILayout.HelpBox("Butler isn't installed. Unpack it into ~/bin (Windows: C:\\butler), then come back here.", MessageType.Warning);
            if (GUILayout.Button("Get butler"))
                Application.OpenURL(ButlerPage);
        }
        else if (!_loggedIn)
            EditorGUILayout.HelpBox($"Log in once, in a terminal:\n\"{_butler}\" login", MessageType.Warning);

        if (_push != null)
            EditorGUILayout.HelpBox("Uploading, the Console shows butler's output.", MessageType.Info);
        else if (_current)
            EditorGUILayout.HelpBox("No changes since the last build: this uploads it straight away.", MessageType.Info);
        else
            EditorGUILayout.HelpBox("The project has changed since the last build, or there is none: this builds first, which takes minutes.", MessageType.Info);

        using (new EditorGUI.DisabledScope(!validProject || _butler == null || !_loggedIn || _push != null
            || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
        {
            if (GUILayout.Button(_current ? "UPLOAD" : "BUILD AND UPLOAD", GUILayout.Height(32f)))
                EditorApplication.delayCall += Run; // not while this window is mid-layout
        }
    }

    private void Run()
    {
        Refresh();
        if (!_current && !Build())
        {
            Refresh();
            return;
        }
        Refresh();
        Push(_butler, _project);
    }

    /// <summary>Builds <c>Builds/WebGL/</c> from scratch and stamps it with what it was built from.</summary>
    public static bool Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return false;

        // Compressed files only load if the host sends the right header; with the fallback
        // the page unpacks them itself, so it runs wherever it's served.
        PlayerSettings.WebGL.decompressionFallback = true;
        AssetDatabase.SaveAssets();

        File.Delete(StampPath);
        if (Directory.Exists(BuildDir))
            Directory.Delete(BuildDir, true); // an older build's files would be uploaded too
        Directory.CreateDirectory(BuildDir);

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = BuildDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"WebGL build failed: {report.summary.result}, {report.summary.totalErrors} errors");
            return false;
        }

        // Hashed after the build: what the build itself saves in the project isn't a change.
        AssetDatabase.SaveAssets();
        File.WriteAllText(StampPath, Fingerprint());
        Debug.Log($"WebGL build {BuildDir} ({report.summary.totalSize / (1024 * 1024)} MB)");
        return true;
    }

    public static bool BuildIsCurrent() =>
        File.Exists(StampPath) && File.Exists(Path.Combine(BuildDir, "index.html"))
        && File.ReadAllText(StampPath) == Fingerprint();

    /// <summary>A hash of everything a build is made from: file names and contents, not dates,
    /// because the editor saves some settings files again without changing them.</summary>
    private static string Fingerprint()
    {
        using (IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Application.unityVersion));
            foreach (string folder in new[] { "Assets", "Packages", "ProjectSettings" })
            {
                string[] files = Directory.GetFiles(Path.Combine(Root, folder), "*", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.Ordinal);
                foreach (string file in files)
                {
                    hash.AppendData(Encoding.UTF8.GetBytes(file.Substring(Root.Length) + "\0"));
                    hash.AppendData(File.ReadAllBytes(file));
                }
            }
            return BitConverter.ToString(hash.GetHashAndReset());
        }
    }

    private static void Push(string butler, string project)
    {
        _push = Process.Start(new ProcessStartInfo(butler, $"push \"{BuildDir}\" {project}:{Channel}")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true, // closed below, so butler can never sit waiting for an answer
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });
        _push.OutputDataReceived += LogButler;
        _push.ErrorDataReceived += LogButler;
        _push.BeginOutputReadLine();
        _push.BeginErrorReadLine();
        _push.StandardInput.Close();

        // A script reload would lose this process, and with it the upload's result.
        EditorApplication.LockReloadAssemblies();
        _progress = Progress.Start("itch.io upload", $"{project}:{Channel}", Progress.Options.Indefinite);
        EditorApplication.update += WatchPush;
        void WatchPush()
        {
            if (!_push.HasExited)
                return;
            EditorApplication.update -= WatchPush;
            _push.WaitForExit(); // the last of the output
            bool ok = _push.ExitCode == 0;
            if (ok)
                Debug.Log($"Sent to itch.io: https://{project.Replace("/", ".itch.io/")}");
            else
                Debug.LogError($"The upload failed: butler exited with {_push.ExitCode}, its output is above.");
            Progress.Finish(_progress, ok ? Progress.Status.Succeeded : Progress.Status.Failed);
            _push.Dispose();
            _push = null;
            EditorApplication.UnlockReloadAssemblies();
            if (HasOpenInstances<ItchUpload>())
                GetWindow<ItchUpload>("itch.io", false).Repaint();
        }
    }

    private static void LogButler(object sender, DataReceivedEventArgs line)
    {
        if (!string.IsNullOrWhiteSpace(line.Data))
            Debug.Log("butler: " + line.Data);
    }

    /// <summary>The editor started from the Hub may not have the terminal's PATH, so the usual
    /// places are tried as well, the itch app's own copy among them.</summary>
    private static string FindButler()
    {
        string exe = Application.platform == RuntimePlatform.WindowsEditor ? "butler.exe" : "butler";
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Concat(new[]
            {
                Path.Combine(home, "bin"),
                Path.Combine(home, ".local", "bin"),
                @"C:\butler",
                Path.Combine(appData, "itch", "apps", "butler"),
            })
            .Where(dir => dir.Length > 0)
            .Select(dir => Path.Combine(dir, exe))
            .FirstOrDefault(File.Exists);
    }

    /// <summary>Butler has no command to ask this, so it goes by the places its docs give.</summary>
    private static bool LoggedIn()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BUTLER_API_KEY")))
            return true;
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string creds = Application.platform == RuntimePlatform.OSXEditor
            ? Path.Combine(home, "Library", "Application Support", "itch", "butler_creds")
            : Path.Combine(home, ".config", "itch", "butler_creds");
        return File.Exists(creds);
    }
}
