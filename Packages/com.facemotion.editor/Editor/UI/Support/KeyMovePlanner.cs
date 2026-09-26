using System;
using System.Collections.Generic;
using FaceMotion.Animation;

namespace FaceMotion.Editor.UI.Support
{
    /// <summary>
    /// Pure planner for horizontal key moves. Applies the same delta to a group of keys,
    /// clamps the whole group into (0..duration), snaps to the frame grid when enabled, and
    /// resolves duplicates against occupied times (existing keys and keys moving together)
    /// by pushing to the nearest free frame. Never deletes or merges keys.
    /// </summary>
    public static class KeyMovePlanner
    {
        public const float TimeTolerance = 1e-4f;

        /// <summary>
        /// Plans final times for the keys of one track. <paramref name="occupied"/> must
        /// contain every other key time in that track (excluding the keys being moved).
        /// </summary>
        public static float[] PlanTrack(
            IReadOnlyList<float> currentTimes,
            IReadOnlyList<float> occupied,
            float deltaTime,
            float duration,
            float frameRate,
            bool snapEnabled)
        {
            float[] result = new float[currentTimes.Count];
            if (duration <= 0f || !IsFinite(duration))
            {
                for (int i = 0; i < currentTimes.Count; i++)
                {
                    result[i] = currentTimes[i];
                }

                return result;
            }

            var used = new List<float>(occupied == null ? currentTimes.Count + 4 : occupied.Count + currentTimes.Count + 4);
            if (occupied != null)
            {
                used.AddRange(occupied);
            }

            deltaTime = TimelineTimeDomain.ConstrainGroupDelta(currentTimes, deltaTime, duration);

            for (int i = 0; i < currentTimes.Count; i++)
            {
                float candidate = currentTimes[i] + deltaTime;
                if (!IsFinite(candidate))
                {
                    candidate = currentTimes[i];
                }

                candidate = TimelineTimeDomain.Clamp(candidate, duration);
                if (snapEnabled)
                {
                    candidate = FrameSnapper.Snap(candidate, frameRate, duration);
                }

                candidate = ResolveCollision(candidate, used, duration, frameRate);
                result[i] = candidate;
                used.Add(candidate);
            }

            return result;
        }

        private static float ResolveCollision(float candidate, List<float> used, float duration, float frameRate)
        {
            if (!Contains(used, candidate))
            {
                return candidate;
            }

            float step = IsFinite(frameRate) && frameRate > 0f ? 1f / frameRate : 1f / 60f;
            for (int frame = 1; frame <= 2048; frame++)
            {
                float later = Math.Min(duration, candidate + step * frame);
                if (later >= 0f && later <= duration && !Contains(used, later))
                {
                    return later;
                }

                float earlier = Math.Max(0f, candidate - step * frame);
                if (earlier >= 0f && earlier <= duration && !Contains(used, earlier))
                {
                    return earlier;
                }
            }

            return candidate;
        }

        private static bool Contains(List<float> times, float target)
        {
            for (int i = 0; i < times.Count; i++)
            {
                if (Math.Abs(times[i] - target) <= TimeTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
