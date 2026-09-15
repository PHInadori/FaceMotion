using System.Collections.Generic;
using UnityEngine;

namespace FaceMotion.Animation
{
    /// <summary>
    /// Produces the frame-aligned sampling grid used by export. Samples start at zero,
    /// advance by 1/frameRate, and always include the exact end of the duration even when
    /// the duration is not on a frame boundary.
    /// </summary>
    public static class MotionSampler
    {
        public static float[] CreateSampleTimes(float duration, float frameRate)
        {
            if (!IsFinite(duration) || duration <= 0f)
            {
                return new[] { 0f };
            }

            if (!IsFinite(frameRate) || frameRate <= 0f)
            {
                return new[] { duration };
            }

            int frames = Mathf.Max(0, Mathf.RoundToInt(duration * frameRate));
            var times = new List<float>(frames + 1);
            for (int i = 0; i <= frames; i++)
            {
                float t = (float)((double)i / frameRate);
                if (t > duration + FrameSnapper.Epsilon)
                {
                    break;
                }

                times.Add(t);
            }

            if (times.Count == 0)
            {
                times.Add(0f);
            }

            float last = times[times.Count - 1];
            if (last < duration - FrameSnapper.Epsilon)
            {
                times.Add(duration);
            }
            else
            {
                times[times.Count - 1] = duration;
            }

            return times.ToArray();
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}