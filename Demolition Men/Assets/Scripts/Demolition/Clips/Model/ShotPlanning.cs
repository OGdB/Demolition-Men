using System;
using Demolition.Generation;

namespace Demolition.Clips
{
    /// <summary>Where to hit and how to frame it: the pure parts of a shot.</summary>
    public static class ShotPlanning
    {
        /// <summary>
        /// The ground-level block whose loss brings down the most of the building, replayed the
        /// way <see cref="BuildingValidator"/> judges weak points (detach, then overload crumble).
        /// Null when nothing at ground level can be hit (no blocks, only glass).
        /// </summary>
        public static SpecBlock WeakPoint(BuildingSpec spec, out float cascade)
        {
            cascade = 0f;
            SpecBlock best = null;
            var baseline = BuildingValidator.Stress(spec.ToStructure());
            foreach (SpecBlock block in spec.Blocks)
            {
                if (block.Rect.yMin > 1 || block.Material == BlockMaterial.Glass)
                    continue;
                float lost = BuildingValidator.Cascade(spec, baseline, block);
                if (best == null || lost > cascade)
                {
                    best = block;
                    cascade = lost;
                }
            }
            return best;
        }

        /// <summary>Share of the frame's short side the subject must fit in. A square master is
        /// cut to 9:16 and 16:9 afterwards, and both keep only its middle 9/16.</summary>
        public static float SafeShare(int width, int height) => width == height ? 9f / 16f : 1f;

        /// <summary>
        /// An orthographic camera that shows [x0, x1] × [y0, y1] inside the frame's safe region
        /// (<see cref="SafeShare"/>), centred across and standing on the region's bottom edge, so
        /// the ground stays at the bottom of every crop and spare room goes to the sky.
        /// </summary>
        public static void Frame(float x0, float x1, float y0, float y1, int width, int height, float minSize,
            out float centerX, out float centerY, out float size)
        {
            float share = SafeShare(width, height);
            float aspect = width == height ? 1f : width / (float)height; // the safe region's
            size = Math.Max(minSize, Math.Max((y1 - y0) * 0.5f, (x1 - x0) * 0.5f / aspect) / share);
            centerX = (x0 + x1) * 0.5f;
            centerY = y0 + size * share;
        }

        /// <summary>
        /// The <c>frame</c> camera of a shot about a building whose bottom-left corner is at
        /// (<paramref name="left"/>, <paramref name="ground"/>): the building, every player's
        /// start and a strip of street, plus the shot's padding.
        /// </summary>
        public static void FrameBuilding(ShotSpec shot, float left, float ground, int width, int height, float levelLeft,
            out float centerX, out float centerY, out float size)
        {
            float x0 = left, x1 = left + width;
            if (shot.building >= 0) // player 2 starts 1.5 behind player 1
                x0 = Math.Min(x0, left + shot.offset - 1.5f * (shot.players - 1) - 1f);
            float pad = shot.camera.pad;
            Frame(x0 - pad, x1 + pad, ground - 1f, ground + height + 1f + pad, shot.width, shot.height, 5f,
                out centerX, out centerY, out size);
            centerX = ClampLeft(centerX, size, shot, levelLeft);
        }

        /// <summary>Keep the camera's left edge on the level: there is no street before it.</summary>
        public static float ClampLeft(float centerX, float size, ShotSpec shot, float levelLeft) =>
            Math.Max(centerX, levelLeft + size * shot.width / (float)shot.height);
    }
}
