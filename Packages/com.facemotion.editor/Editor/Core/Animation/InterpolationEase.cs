using FaceMotion.Timeline;
using UnityEngine;

namespace FaceMotion.Animation
{
    /// <summary>
    /// The single canonical easing function shared by every consumer. Input is a normalized
    /// segment offset in (0..1) and output is the eased offset in (0..1). Float, position,
    /// scale, and rotation evaluation all call this exactly once per segment so every
    /// interpolation mode produces identical timing on every value kind. No UI or
    /// UnityEditor dependency.
    /// </summary>
    public static class InterpolationEase
    {
        public static float Evaluate(InterpolationType type, float t)
        {
            t = Mathf.Clamp01(t);
            switch (type)
            {
                case InterpolationType.Hold:
                    // Hold keeps the left value across the entire segment. The switch to the
                    // right value happens only at the exact right key time, which the
                    // evaluator endpoint handling provides.
                    return 0f;
                case InterpolationType.Linear:
                    return t;
                case InterpolationType.EaseIn:
                    return t * t;
                case InterpolationType.EaseOut:
                    return 1f - (1f - t) * (1f - t);
                case InterpolationType.EaseInOut:
                    return t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);
                case InterpolationType.Smooth:
                    return t * t * (3f - 2f * t);
                default:
                    return t;
            }
        }
    }
}