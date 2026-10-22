// Compile-only stand-ins for the URP 17.4 / Core RP members Demolition.Lighting uses: URP ships
// inside the editor since Unity 6, so the package registry has nothing to build from source.
// Signatures copied from Unity-Technologies/Graphics, branch 6000.4/staging. Never run.
using System;

namespace UnityEngine.Rendering
{
    public abstract class VolumeParameter { public bool overrideState; }
    public class VolumeParameter<T> : VolumeParameter
    {
        public T value;
        public void Override(T x) { overrideState = true; value = x; }
    }
    public class FloatParameter : VolumeParameter<float> { }
    public class MinFloatParameter : FloatParameter { }
    public class ClampedFloatParameter : FloatParameter { }
    public class VolumeComponent : ScriptableObject { }
    public sealed class VolumeProfile : ScriptableObject
    {
        public T Add<T>(bool overrides = false) where T : VolumeComponent => throw new NotImplementedException();
    }
    public class Volume : MonoBehaviour
    {
        public bool isGlobal { get; set; }
        public float priority = 0f;
        public float weight = 1f;
        public VolumeProfile sharedProfile = null;
    }
}

namespace UnityEngine.Rendering.Universal
{
    public sealed class Bloom : VolumeComponent
    {
        public MinFloatParameter threshold;
        public MinFloatParameter intensity;
        public ClampedFloatParameter scatter;
    }
    public sealed class Vignette : VolumeComponent
    {
        public ClampedFloatParameter intensity;
        public ClampedFloatParameter smoothness;
    }
    public sealed class ColorAdjustments : VolumeComponent
    {
        public FloatParameter postExposure;
        public ClampedFloatParameter contrast;
        public ClampedFloatParameter saturation;
    }
    public class Light2DBase : MonoBehaviour { }
    public sealed class Light2D : Light2DBase
    {
        public enum LightType { Parametric = 0, Freeform = 1, Sprite = 2, Point = 3, Global = 4 }
        public LightType lightType { get; set; }
        public bool shadowsEnabled { get; set; }
        public Color color { get; set; }
        public float intensity { get; set; }
        public float falloffIntensity { get; set; }
        public float pointLightInnerRadius { get; set; }
        public float pointLightOuterRadius { get; set; }
    }
    // The real one derives from RenderPipelineAsset<UniversalRenderPipeline>.
    public partial class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        public override RenderPipeline CreatePipeline() => throw new NotImplementedException(); // protected in Unity; the reference assemblies make it public
        public float renderScale { get; set; }
    }
    public class UniversalAdditionalCameraData : MonoBehaviour
    {
        public bool renderPostProcessing { get; set; }
    }
    public static class CameraExtensions
    {
        public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera) => throw new NotImplementedException();
    }
}
