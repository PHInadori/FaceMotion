using System.Collections.Generic;
using FaceMotion.Timeline;
using UnityEngine;

namespace FaceMotion.Animation
{
    /// <summary>
    /// Locates the evaluation segment for a time using binary search. Returns false when
    /// the input is invalid, empty, has a single key, or lies at/beyond either endpoint;
    /// callers clamp to the endpoint values in those cases. Keys are expected to be sorted
    /// by ascending time, which validation guarantees.
    /// </summary>
    public static class SegmentSearch
    {
        public static bool TryFindFloatSegment(
            IReadOnlyList<FloatKeyframeData> keys,
            float time,
            out FloatKeyframeData left,
            out FloatKeyframeData right,
            out float alpha)
        {
            left = null;
            right = null;
            alpha = 0f;

            if (keys == null || keys.Count < 2 || !IsFinite(time))
            {
                return false;
            }

            if (time <= keys[0].Time || time >= keys[keys.Count - 1].Time)
            {
                return false;
            }

            int rightIndex = FindRightIndex(keys, time);
            left = keys[rightIndex - 1];
            right = keys[rightIndex];
            alpha = ComputeAlpha(left.Time, right.Time, time);
            return true;
        }

        public static bool TryFindVector3Segment(
            IReadOnlyList<Vector3KeyframeData> keys,
            float time,
            out Vector3KeyframeData left,
            out Vector3KeyframeData right,
            out float alpha)
        {
            left = null;
            right = null;
            alpha = 0f;

            if (keys == null || keys.Count < 2 || !IsFinite(time))
            {
                return false;
            }

            if (time <= keys[0].Time || time >= keys[keys.Count - 1].Time)
            {
                return false;
            }

            int rightIndex = FindRightIndex(keys, time);
            left = keys[rightIndex - 1];
            right = keys[rightIndex];
            alpha = ComputeAlpha(left.Time, right.Time, time);
            return true;
        }

        /// <summary>Computes the normalized segment offset of time between the two key times.</summary>
        public static float ComputeAlpha(float leftTime, float rightTime, float time)
        {
            float span = rightTime - leftTime;
            return span > 0f ? (time - leftTime) / span : 0f;
        }

        private static int FindRightIndex(IReadOnlyList<FloatKeyframeData> keys, float time)
        {
            int low = 0;
            int high = keys.Count - 1;
            while (low < high)
            {
                int mid = (low + high) >> 1;
                if (keys[mid].Time <= time)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            return low;
        }

        private static int FindRightIndex(IReadOnlyList<Vector3KeyframeData> keys, float time)
        {
            int low = 0;
            int high = keys.Count - 1;
            while (low < high)
            {
                int mid = (low + high) >> 1;
                if (keys[mid].Time <= time)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            return low;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}