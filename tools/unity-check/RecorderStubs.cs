// Signatures of the Unity Recorder 5.1.7 members the Clip Lab uses (Assets/Editor/Clips), copied
// from the package's Editor/Unity.Recorder.Editor.api. The Recorder isn't built here: this is a
// signature check, like UrpStubs.cs. Add a member here before the Clip Lab uses it.
using UnityEngine;

namespace UnityEditor.Recorder
{
    public enum FrameRatePlayback { Constant = 0, Variable = 1 }

    public abstract class RecorderSettings : ScriptableObject
    {
        public bool Enabled { get; set; }
        public string OutputFile { get; set; }
    }

    public class MovieRecorderSettings : RecorderSettings
    {
        public bool CaptureAudio { get; set; }
        public Encoder.IEncoderSettings EncoderSettings { get; set; }
        public Input.ImageInputSettings ImageInputSettings { get; set; }
    }

    public class RecorderControllerSettings : ScriptableObject
    {
        public bool CapFrameRate { get; set; }
        public float FrameRate { get; set; }
        public FrameRatePlayback FrameRatePlayback { get; set; }
        public void AddRecorderSettings(RecorderSettings recorder) { }
        public void SetRecordModeToManual() { }
    }

    public class RecorderController
    {
        public RecorderController(RecorderControllerSettings settings) { }
        public bool IsRecording() => false;
        public void PrepareRecording() { }
        public bool StartRecording() => false;
        public void StopRecording() { }
    }
}

namespace UnityEditor.Recorder.Encoder
{
    public interface IEncoderSettings { }

    public sealed class CoreEncoderSettings : IEncoderSettings
    {
        public OutputCodec Codec { get; set; }
        public VideoEncodingQuality EncodingQuality { get; set; }
        public float TargetBitRate { get; set; }
        public uint KeyframeDistance { get; set; }
        public enum OutputCodec { MP4 = 0, WEBM = 1 }
        public enum VideoEncodingQuality { Low = 0, Medium = 1, High = 2, Custom = 3 }
    }
}

namespace UnityEditor.Recorder.Input
{
    public abstract class ImageInputSettings
    {
        public abstract int OutputHeight { get; set; }
        public abstract int OutputWidth { get; set; }
    }

    public abstract class StandardImageInputSettings : ImageInputSettings
    {
        public override int OutputHeight { get; set; }
        public override int OutputWidth { get; set; }
    }

    public class GameViewInputSettings : StandardImageInputSettings { }
}
