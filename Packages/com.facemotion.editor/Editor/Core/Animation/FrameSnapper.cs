using System;
using UnityEngine;

namespace FaceMotion.Animation
{
    /// <summary>
    /// Round/snap a time onto the frame grid. Invalid frame rates return the input
    /// unchanged; NaN and infinite times are returned unchanged to stay non-destructive.
    /// </summary>
    public static class FrameSnapper
    {
        public const float Epsilon = 1e-4f;

        public static float Snap(float time, float frameRate)
        {
            if (!IsUsableRate(frameRate) || !IsFinite(time))
            {
                return time;
            }

            double frame = Math.Round((double)time * frameRate, MidpointRounding.AwayFromZero);
            return (float)(frame / frameRate);
        }

        /// <summary>Snaps to the frame grid and clamps into the (0..duration) domain.</summary>
        public static float Snap(float time, float frameRate, float duration)
        {
            float snapped = Snap(time, frameRate);
            if (duration > 0f && IsFinite(duration))
            {
                if (snapped < 0f)
                {
                    snapped = 0f;
                }
                else if (Math.Abs(snapped - duration) <= Epsilon)
                {
                    snapped = duration;
                }
                else if (snapped > duration)
                {
                    snapped = duration;
                }
            }

            return snapped;
        }

        private static bool IsUsableRate(float frameRate)
        {
            return IsFinite(frameRate) && frameRate > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}