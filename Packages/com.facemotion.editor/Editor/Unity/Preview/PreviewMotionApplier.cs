using System;
using FaceMotion.Animation;
using FaceMotion.Data;
using FaceMotion.Timeline;
using UnityEngine;

namespace FaceMotion.Editor.Preview
{
    /// <summary>Applies authored values using the canonical evaluator and no alternate curve math.</summary>
    public static class PreviewMotionApplier
    {
        public static void Apply(
            FaceMotionAnimationData animation,
            PreviewObjectCache cache,
            PreviewBaseline baseline,
            float time)
        {
            if (animation == null || animation.Timeline == null || cache == null || baseline == null)
            {
                return;
            }

            baseline.Restore();
            foreach (var track in animation.Timeline.Tracks)
            {
                if (track == null || !track.Enabled)
                {
                    continue;
                }

                ApplyTrack(track, cache, baseline, time);
            }
        }

        private static bool ApplyTrack(
            FaceTrackData track,
            PreviewObjectCache cache,
            PreviewBaseline baseline,
            float time)
        {
            if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
            {
                var binding = new BlendShapeBinding(track.BlendShape.RendererPath, track.BlendShape.BlendShapeName);
                if (!cache.TryGetBlendShape(binding, out var renderer, out int index))
                {
                    return false;
                }

                if (!CanonicalMotionEvaluator.Instance.TryEvaluateFloat(track.BlendShape.Keys, time, out float value))
                {
                    return false;
                }

                baseline.Capture(renderer, index);
                renderer.SetBlendShapeWeight(index, value);
                return true;
            }

            if (!TrackKinds.IsTransform(track.Kind) || track.Transform == null)
            {
                return false;
            }

            if (!cache.TryGetTransform(track.Transform.TransformPath, out var transform))
            {
                return false;
            }

            baseline.Capture(transform);
            if (track.Kind == TrackKind.TransformPosition
                && CanonicalMotionEvaluator.Instance.TryEvaluatePosition(track.Transform.Keys, time, out Vector3 position))
            {
                transform.localPosition = position;
                return true;
            }
            else if (track.Kind == TrackKind.TransformRotation
                && CanonicalMotionEvaluator.Instance.TryEvaluateRotation(track.Transform.Keys, time, out Quaternion rotation))
            {
                transform.localRotation = rotation;
                return true;
            }
            else if (track.Kind == TrackKind.TransformScale
                && CanonicalMotionEvaluator.Instance.TryEvaluateScale(track.Transform.Keys, time, out Vector3 scale))
            {
                transform.localScale = scale;
                return true;
            }

            return false;
        }
    }
}
