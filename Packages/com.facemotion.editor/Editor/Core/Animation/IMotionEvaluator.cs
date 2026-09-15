using System.Collections.Generic;
using FaceMotion.Timeline;
using UnityEngine;

namespace FaceMotion.Animation
{
    /// <summary>
    /// Stateless canonical evaluator shared by preview, presets, random motion, and export.
    /// Implementations must not access scenes, assets, preview state, or VRCSDK types.
    /// </summary>
    public interface IMotionEvaluator
    {
        /// <summary>Evaluates a blend shape curve at time. False when nothing can be evaluated.</summary>
        bool TryEvaluateFloat(IReadOnlyList<FloatKeyframeData> keys, float time, out float value);

        /// <summary>Evaluates a position curve at time via eased component-wise lerp.</summary>
        bool TryEvaluatePosition(IReadOnlyList<Vector3KeyframeData> keys, float time, out Vector3 value);

        /// <summary>Evaluates a scale curve at time via eased component-wise lerp.</summary>
        bool TryEvaluateScale(IReadOnlyList<Vector3KeyframeData> keys, float time, out Vector3 value);

        /// <summary>
        /// Evaluates a rotation curve at time via eased shortest-path quaternion slerp.
        /// 360-degree spin is not supported by the first evaluator.
        /// </summary>
        bool TryEvaluateRotation(IReadOnlyList<Vector3KeyframeData> keys, float time, out Quaternion value);
    }
}