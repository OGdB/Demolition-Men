using System;
using System.Collections.Generic;
using System.IO;
using Demolition.Clips;
using Demolition.Levels;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools ▸ Demolition ▸ Clip Lab (docs/store-media.md §3): the shot files in
/// <c>content-work/shots</c>, previewed or recorded with the Unity Recorder into
/// <c>content-work/media/raw/&lt;shot&gt;/take&lt;n&gt;.mp4</c>, with the take's event log (.json)
/// and stills next to it. Takes run one after another: each enters play mode in the Run
/// scene, waits for the <see cref="ClipDirector"/> to be ready, records at a constant frame
/// rate until the shot ends, and leaves play mode. The queue lives in SessionState, so it
/// survives the domain reload of every play mode. Agents call <see cref="Record"/>,
/// <see cref="RecordAll"/>, <see cref="Preview"/> and <see cref="Stop"/> through the Unity MCP.
/// </summary>
[InitializeOnLoad]
public sealed class ClipLab : EditorWindow
{
    private const string QueueKey = "Demolition.Clips.Queue";
    private const string CurrentKey = "Demolition.Clips.Current";
    private const double BootTimeout = 30;
    /// <summary>A director still setting up after this long (the building never streamed in,
    /// the run never started) ends its take, so an unattended batch moves on.</summary>
    private const double SetupTimeout = 60;
    /// <summary>A take that was started but whose play mode never came (a compile error) is
    /// given up after this long, so the queue doesn't wait for it forever.</summary>
    private const double StartTimeout = 15;

    public static string ShotsFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "content-work", "shots"));
    public static string MediaFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "content-work", "media", "raw"));

    private static RecorderController _recorder;
    private static string _recording; // the output base of the take being recorded
    private static bool _wasMuted;
    private static double _enteredAt;
    private static double _requestedAt; // 0 once play mode came
    private static bool _skipping;      // the take is given up, the queue goes on
    private Vector2 _scroll;
    private int _takes;

    static ClipLab()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Demolition/Clip Lab")]
    private static void Open() => GetWindow<ClipLab>("Clip Lab");

    // ---------------- For the window and for agents ----------------

    /// <summary>Record a shot's takes (0: as many as its file asks for).</summary>
    public static void Record(string id, int takes = 0) => Enqueue(id, takes, true);

    /// <summary>Play a shot once without recording.</summary>
    public static void Preview(string id) => Enqueue(id, 1, false);

    public static void RecordAll(int takes = 0)
    {
        foreach (string path in ShotFiles())
            Enqueue(Path.GetFileNameWithoutExtension(path), takes, true);
    }

    /// <summary>Drop the queue and end the take that is playing.</summary>
    public static void Stop()
    {
        SessionState.EraseString(QueueKey);
        if (EditorApplication.isPlaying && Current != null)
            EditorApplication.isPlaying = false;
        else
            SessionState.EraseString(CurrentKey); // a take whose play mode never came
    }

    /// <summary>A still of the Game view as it is now (menus, results, the shop: the store
    /// screenshots that aren't shots), <paramref name="scale"/> times its size, into
    /// content-work/media/stills. Play mode only.</summary>
    public static string Still(int scale = 2)
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("Clip Lab: a still needs play mode (it captures the Game view).");
            return null;
        }
        string folder = Path.Combine(Path.GetDirectoryName(MediaFolder), "stills");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"still-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        ScreenCapture.CaptureScreenshot(path, scale);
        Debug.Log($"Clip Lab: still {path}");
        return path;
    }

    /// <summary>Record the Game view as it plays now, until <see cref="StopTake"/> or the end of play
    /// mode: takes that aren't shots (online play, a run by hand), into
    /// content-work/media/raw/<paramref name="name"/>/take&lt;n&gt;.mp4. At a fixed step like a shot, so
    /// the video is smooth, but the game slows down for whoever is playing while the encoder can't
    /// keep up (on Linux, about a quarter of real speed at 1080p). Returns the take's path without
    /// extension, or null.</summary>
    public static string RecordTake(string name, int width = 1920, int height = 1080, int fps = 60)
    {
        if (!EditorApplication.isPlaying || _recorder != null)
        {
            Debug.LogWarning("Clip Lab: a take needs play mode, and nothing else recording.");
            return null;
        }
        string outputBase = Path.Combine(MediaFolder, name, "take" + NextTake(name, new List<string>()));
        Directory.CreateDirectory(Path.GetDirectoryName(outputBase));
        _recorder = StartRecorder(new ShotSpec { id = name, width = width, height = height, fps = fps }, outputBase);
        if (_recorder == null)
            return null;
        Debug.Log($"Clip Lab: recording {outputBase}");
        return outputBase;
    }

    /// <summary>End the take <see cref="RecordTake"/> started.</summary>
    public static void StopTake()
    {
        if (Current == null)
            StopRecorder();
    }

    /// <summary>What is playing and how many takes wait, for agents polling the Clip Lab.</summary>
    public static string Status()
    {
        int waiting = Queue().Count;
        string status = Current == null ? $"idle, {waiting} queued" : $"playing {Current} ({ClipDirector.Current?.State}), {waiting} queued";
#if UNITY_EDITOR_LINUX
        int converting = Directory.Exists(MediaFolder) ? Directory.GetFiles(MediaFolder, "*.webm", SearchOption.AllDirectories).Length : 0;
        if (converting > 0)
            status += $", {converting} converting to MP4";
#endif
        return status;
    }

    public static IEnumerable<string> ShotFiles()
    {
        if (!Directory.Exists(ShotsFolder))
            yield break;
        string[] files = Directory.GetFiles(ShotsFolder, "*.json");
        Array.Sort(files, StringComparer.Ordinal);
        foreach (string file in files)
            yield return file;
    }

    // ---------------- Queue ----------------

    private static string Current
    {
        get
        {
            string entry = SessionState.GetString(CurrentKey, string.Empty);
            return entry.Length > 0 ? entry : null;
        }
    }

    private static List<string> Queue()
    {
        string stored = SessionState.GetString(QueueKey, string.Empty);
        return new List<string>(stored.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private static void SaveQueue(List<string> queue) => SessionState.SetString(QueueKey, string.Join("\n", queue));

    private static string PathOf(string id) => Path.Combine(ShotsFolder, id + ".json");

    private static ShotSpec Load(string id, out List<string> errors)
    {
        errors = new List<string>();
        try
        {
            ShotSpec shot = ShotSpec.FromJson(File.ReadAllText(PathOf(id)));
            errors = shot.Errors();
            return shot;
        }
        catch (Exception e)
        {
            errors.Add(e.Message);
            return null;
        }
    }

    private static void Enqueue(string id, int takes, bool record)
    {
        ShotSpec shot = Load(id, out List<string> errors);
        if (errors.Count > 0)
        {
            Debug.LogError($"Clip Lab: {id}: {string.Join("; ", errors)}");
            return;
        }
        if (EditorApplication.isPlaying && Current == null)
        {
            Debug.LogWarning("Clip Lab: stop play mode first; each take starts its own.");
            return;
        }
        List<string> queue = Queue();
        int count = takes > 0 ? takes : record ? shot.takes : 1;
        int first = record ? NextTake(id, queue) : 1;
        for (int take = first; take < first + count; take++)
            queue.Add($"{id}|{take}|{(record ? 1 : 0)}");
        SaveQueue(queue);
        if (EditorApplication.isPlayingOrWillChangePlaymode || Current != null)
            return; // the take that is playing starts the next one when it ends
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            StartNext();
        else
            SessionState.EraseString(QueueKey);
    }

    // New takes never overwrite recorded ones: the cut lists in content-work/edits point at them.
    private static int NextTake(string id, List<string> queue)
    {
        int last = 0;
        string folder = Path.Combine(MediaFolder, id);
        if (Directory.Exists(folder))
            foreach (string file in Directory.GetFiles(folder, "take*"))
            {
                // take<n>.mp4, its log, crops and stills, and on Linux the .webm still converting
                string name = Path.GetFileName(file);
                int end = 4;
                while (end < name.Length && char.IsDigit(name[end]))
                    end++;
                if (int.TryParse(name.Substring(4, end - 4), out int n))
                    last = Mathf.Max(last, n);
            }
        var entries = new List<string>(queue);
        if (Current != null)
            entries.Add(Current); // still setting up: nothing of it on disk yet
        foreach (string entry in entries)
        {
            string[] parts = entry.Split('|');
            if (parts[0] == id && parts[2] == "1" && int.TryParse(parts[1], out int n))
                last = Mathf.Max(last, n);
        }
        return last + 1;
    }

    private static void StartNext()
    {
        List<string> queue = Queue();
        if (queue.Count == 0)
            return;
        string entry = queue[0];
        queue.RemoveAt(0);
        SaveQueue(queue);

        string[] parts = entry.Split('|');
        string id = parts[0];
        bool record = parts[2] == "1";
        ShotSpec shot = Load(id, out List<string> errors);
        if (errors.Count > 0)
        {
            Debug.LogError($"Clip Lab: {id}: {string.Join("; ", errors)}");
            StartNext();
            return;
        }
        string outputBase = Path.Combine(MediaFolder, id, record ? "take" + parts[1] : "preview");
        Directory.CreateDirectory(Path.GetDirectoryName(outputBase));

        // Both requests are read when play mode starts: the level by LevelSelection (as for the
        // Level Lab; an empty seed keeps the level's own), the shot by the ClipDirector.
        long now = DateTime.UtcNow.Ticks;
        PlayerPrefs.SetString(LevelSelection.EditorRequestKey, $"{shot.level}|{(shot.seed != 0 ? shot.seed.ToString() : "")}||{now}");
        PlayerPrefs.SetString(ClipDirector.RequestKey, $"{PathOf(id)}|{parts[1]}|{outputBase}|{(record ? 1 : 0)}|{now}");
        PlayerPrefs.Save();
        SessionState.SetString(CurrentKey, entry);
        _requestedAt = EditorApplication.timeSinceStartup;
        EditorSceneManager.OpenScene("Assets/Scenes/Level.unity");
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        switch (change)
        {
            case PlayModeStateChange.EnteredPlayMode:
                _enteredAt = EditorApplication.timeSinceStartup;
                _requestedAt = 0;
                _skipping = false;
                break;
            case PlayModeStateChange.ExitingPlayMode:
                StopRecorder();
                if (Current != null && ClipDirector.Current != null && ClipDirector.Current.State != ClipDirector.Stage.Done && !_skipping)
                {
                    Debug.Log("Clip Lab: stopped before the shot ended; the queue is dropped.");
                    SessionState.EraseString(QueueKey);
                }
                break;
            case PlayModeStateChange.EnteredEditMode:
                if (Current == null)
                    break;
                SessionState.EraseString(CurrentKey);
                EditorApplication.delayCall += StartNext;
                break;
        }
    }

    // While a take plays: start the recorder when the director is ready, stop at the end.
    private static void Tick()
    {
        if (Current == null)
            return;
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (_requestedAt > 0 && EditorApplication.timeSinceStartup - _requestedAt > StartTimeout)
            {
                Debug.LogError($"Clip Lab: {Current} never entered play mode (compile errors?); the queue is dropped.");
                SessionState.EraseString(CurrentKey);
                SessionState.EraseString(QueueKey);
            }
            return;
        }
        if (!EditorApplication.isPlaying)
            return;
        ClipDirector director = ClipDirector.Current;
        if (director == null)
        {
            if (EditorApplication.timeSinceStartup - _enteredAt > BootTimeout)
            {
                Debug.LogError("Clip Lab: the shot never started; see the Console for why.");
                EditorApplication.isPlaying = false;
            }
            return;
        }
        if (director.State == ClipDirector.Stage.Setup && EditorApplication.timeSinceStartup - _enteredAt > SetupTimeout)
        {
            Debug.LogError($"Clip Lab: {Current} never got ready (its building didn't stream in, or the run didn't start); skipped.");
            _skipping = true;
            EditorApplication.isPlaying = false;
            return;
        }
        if (director.State == ClipDirector.Stage.Ready && director.Records && _recorder == null)
        {
            _recorder = StartRecorder(director.Shot, director.OutputBase);
            if (_recorder == null)
            {
                Debug.LogError("Clip Lab: the recorder didn't start.");
                EditorApplication.isPlaying = false;
                return;
            }
            director.Roll();
        }
        else if (director.State == ClipDirector.Stage.Done)
        {
            StopRecorder();
            Debug.Log($"Clip Lab: {Current} done: {director.OutputBase}");
            EditorApplication.isPlaying = false;
        }
    }

    // MP4 (H.264, high quality) of the Game view at the shot's size and a constant frame rate:
    // the Recorder sets Time.captureFramerate, so a heavy collapse never drops a frame in the video.
    private static RecorderController StartRecorder(ShotSpec shot, string outputBase)
    {
        var settings = CreateInstance<RecorderControllerSettings>();
        var movie = CreateInstance<MovieRecorderSettings>();
        movie.name = shot.id;
        movie.Enabled = true;
#if UNITY_EDITOR_LINUX
        // The Recorder has no H.264 on Linux: VP8 at a bitrate that keeps the pixel art crisp
        // (0.25 bits a pixel), turned into the MP4 the media tools expect when the take ends.
        movie.EncoderSettings = new CoreEncoderSettings
        {
            Codec = CoreEncoderSettings.OutputCodec.WEBM,
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.Custom,
            TargetBitRate = Mathf.Max(20f, shot.width * shot.height * shot.fps * 0.25f / 1e6f),
            KeyframeDistance = (uint)shot.fps,
        };
#else
        movie.EncoderSettings = new CoreEncoderSettings
        {
            Codec = CoreEncoderSettings.OutputCodec.MP4,
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
        };
#endif
        movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = shot.width, OutputHeight = shot.height };
        movie.CaptureAudio = true;
        movie.OutputFile = outputBase; // the Recorder adds .mp4 (.webm on Linux)
        settings.AddRecorderSettings(movie);
        settings.SetRecordModeToManual();
        settings.FrameRatePlayback = FrameRatePlayback.Constant;
        settings.FrameRate = shot.fps;
        settings.CapFrameRate = true;
        var recorder = new RecorderController(settings);
        recorder.PrepareRecording();
        _recording = outputBase;
        // The Recorder hears what the Game view plays: its Mute Audio would make a silent take.
        _wasMuted = EditorUtility.audioMasterMute;
        EditorUtility.audioMasterMute = false;
        if (recorder.StartRecording())
            return recorder;
        EditorUtility.audioMasterMute = _wasMuted;
        return null;
    }

    private static void StopRecorder()
    {
        if (_recorder == null)
            return;
        if (_recorder.IsRecording())
            _recorder.StopRecording();
        _recorder = null;
        EditorUtility.audioMasterMute = _wasMuted;
#if UNITY_EDITOR_LINUX
        Transcode(_recording);
#endif
    }

#if UNITY_EDITOR_LINUX
    // H.264 encoders in the order tried: x264, else the GPU's (Fedora's ffmpeg leaves x264 out).
    private static readonly string[] H264 =
    {
        "-c:v libx264 -crf 14 -preset medium",
        "-c:v h264_nvenc -preset p7 -tune hq -rc vbr -cq 16 -b:v 0 -profile:v high",
        "-c:v libopenh264 -b:v 40M",
    };

    // take<n>.webm → take<n>.mp4 (H.264, near lossless) with ffmpeg, in the background while the
    // next take records. A shell does it, so it finishes across the next play mode's domain
    // reload: the MP4 appears whole (renamed from .part) and the WebM goes once it worked;
    // a failure leaves the WebM and ffmpeg's errors in take<n>.ffmpeg.txt.
    private static void Transcode(string outputBase)
    {
        string webm = outputBase + ".webm", mp4 = outputBase + ".mp4", part = mp4 + ".part", errors = outputBase + ".ffmpeg.txt";
        if (!File.Exists(webm))
        {
            Debug.LogError($"Clip Lab: no {webm} to convert.");
            return;
        }
        var attempts = new List<string>();
        foreach (string encoder in H264)
            attempts.Add($"ffmpeg -y -hide_banner -loglevel error -i {Quote(webm)} {encoder} -pix_fmt yuv420p "
                         + $"-c:a aac -b:a 192k -movflags +faststart -f mp4 {Quote(part)} 2>> {Quote(errors)}");
        string script = $"({string.Join(" || ", attempts)}) && mv {Quote(part)} {Quote(mp4)} && rm -f {Quote(webm)} {Quote(errors)}";
        var start = new System.Diagnostics.ProcessStartInfo("/bin/sh", $"-c \"{script}\"") { UseShellExecute = false, CreateNoWindow = true };
        System.Diagnostics.Process.Start(start)?.Dispose();
    }

    private static string Quote(string path) => "'" + path.Replace("'", "'\\''") + "'";
#endif

    // ---------------- Window ----------------

    private void OnInspectorUpdate() => Repaint();

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Status", Status());
        EditorGUILayout.LabelField("Shots", ShotsFolder, EditorStyles.miniLabel);
        _takes = EditorGUILayout.IntField(new GUIContent("Takes", "0: each shot's own count"), Mathf.Max(0, _takes));
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Record all"))
                RecordAll(_takes);
            if (GUILayout.Button("Stop"))
                Stop();
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                if (GUILayout.Button(new GUIContent("Still", "The Game view at twice its size, into content-work/media/stills")))
                    Still();
            if (GUILayout.Button("Open media folder"))
            {
                Directory.CreateDirectory(MediaFolder);
                EditorUtility.RevealInFinder(MediaFolder);
            }
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (string path in ShotFiles())
        {
            string id = Path.GetFileNameWithoutExtension(path);
            ShotSpec shot = Load(id, out List<string> errors);
            using (new EditorGUILayout.HorizontalScope())
            {
                string about = shot == null ? id
                    : $"{id}   {shot.level} #{shot.building}, {shot.camera?.mode}, {shot.width}x{shot.height}, {shot.takes} takes";
                EditorGUILayout.LabelField(about);
                using (new EditorGUI.DisabledScope(errors.Count > 0))
                {
                    if (GUILayout.Button("Preview", GUILayout.Width(70)))
                        Preview(id);
                    if (GUILayout.Button("Record", GUILayout.Width(70)))
                        Record(id, _takes);
                }
            }
            if (errors.Count > 0)
                EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Error);
        }
        EditorGUILayout.EndScrollView();
    }
}
