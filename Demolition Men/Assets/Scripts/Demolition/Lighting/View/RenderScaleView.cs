using Demolition.Meta;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Demolition.Lighting
{
    /// <summary>
    /// Hands <see cref="Quality.RenderScale"/> to URP: the world renders at that fraction of the
    /// screen and is scaled up, while UI canvases stay at full resolution. It sets the render
    /// scale on the pipeline asset in memory; in the editor the authored value comes back when
    /// play mode ends, so the asset file never changes.
    /// </summary>
    public static class RenderScaleView
    {
        private static UniversalRenderPipelineAsset _asset;
        private static float _authored;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            Quality.Changed -= Apply;
            Quality.Changed += Apply;
            Application.quitting -= Restore;
            Application.quitting += Restore;
            Apply(); // AppBootstrap may have applied the settings before this ran
        }

        private static void Apply()
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null)
                return;
            if (asset != _asset)
            {
                Restore();
                _asset = asset;
                _authored = asset.renderScale;
            }
            asset.renderScale = Quality.RenderScale;
        }

        private static void Restore()
        {
            if (_asset != null)
                _asset.renderScale = _authored;
            _asset = null;
        }
    }
}
