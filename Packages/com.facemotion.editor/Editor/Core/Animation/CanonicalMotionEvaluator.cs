using System.Collections.Generic;
using FaceMotion.Timeline;
using UnityEngine;

namespace FaceMotion.Animation
{
    /// <summary>
    /// The single canonical evaluator implementation. Stateless, allocation-free, and
    /// safe to call from any thread. Interpolation is defined by the outgoing key.
    /// </summary>
    public sealed class CanonicalMotionEvaluator : IMotionEvaluator
    {
        public static readonly IMotionEvaluator Instance = new CanonicalMotionEvaluator();

        private CanonicalMotionEvaluator()
        {
        }

        public bool TryEvaluateFloat(IReadOnlyList<FloatKeyframeData> keys, float time, out float value)
        {
            value = 0f;
            if (keys == null || keys.Count == 0 || !IsFinite(time))
            {
                return false;
            }

            if (time <= keys[0].Time)
            {
                value = keys[0].Value;
                return true;
            }

            if (time >= keys[keys.Count - 1].Time)
            {
                value = keys[keys.Count - 1].Value;
                return true;
            }

            if (SegmentSearch.TryFindFloatSegment(keys, time, out var left, out var right, out float alpha))
            {
                value = Mathf.LerpUnclamped(left.Value, right.Value, InterpolationEase.Evaluate(left.Interpolation, alpha));
                return true;
            }

            return false;
        }

        public bool TryEvaluatePosition(IReadOnlyList<Vector3KeyframeData> keys, float time, out Vector3 value)
        {
            return TryEvaluateVector3(keys, time, out value);
        }

        public bool TryEvaluateScale(IReadOnlyList<Vector3KeyframeData> keys, float time, out Vector3 value)
        {
            return TryEvaluateVector3(keys, time, out value);
        }

        public bool TryEvaluateRotation(IReadOnlyList<Vector3KeyframeData> keys, float time, out Quaternion value)
        {
            value = Quaternion.identity;
            if (keys == null || keys.Count == 0 || !IsFinite(time))
            {
                return false;
            }

            if (time <= keys[0].Time)
            {
                value = Quaternion.Euler(keys[0].Value);
                return true;
            }

            if (time >= keys[keys.Count - 1].Time)
            {
                value = Quaternion.Euler(keys[keys.Count - 1].Value);
                return true;
            }

            if (SegmentSearch.TryFindVector3Segment(keys, time, out var left, out var right, out float alpha))
            {
                Quaternion leftQ = Quaternion.Euler(left.Value);
                Quaternion rightQ = Quaternion.Euler(right.Value);
                if (Quaternion.Dot(leftQ, rightQ) < 0f)
                {
                    rightQ = new Quaternion(-rightQ.x, -rightQ.y, -rightQ.z, -rightQ.w);
                }

                value = Quaternion.Slerp(leftQ, rightQ, InterpolationEase.Evaluate(left.Interpolation, alpha));
                return true;
            }

            return false;
        }

        private bool TryEvaluateVector3(IReadOnlyList<Vector3KeyframeData> keys, float time, out Vector3 value)
        {
            value = default;
            if (keys == null || keys.Count == 0 || !IsFinite(time))
            {
                return false;
            }

            if (time <= keys[0].Time)
            {
                value = keys[0].Value;
                return true;
            }

            if (time >= keys[keys.Count - 1].Time)
            {
                value = keys[keys.Count - 1].Value;
                return true;
            }

            if (SegmentSearch.TryFindVector3Segment(keys, time, out var left, out var right, out float alpha))
            {
                value = Vector3.LerpUnclamped(left.Value, right.Value, InterpolationEase.Evaluate(left.Interpolation, alpha));
                return true;
            }

            return false;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}