using FaceMotion.Animation;
using FaceMotion.Data;
using FaceMotion.Editor.Diagnostics;
using FaceMotion.Timeline;
using UnityEngine;

namespace FaceMotion.Editor.Preview
{
    /// <summary>Applies authored values using the canonical evaluator and no alternate curve math.</summary>
    public static class PreviewMotionApplier
    {
        public static void Apply(FaceMotionAnimationData animation, PreviewObjectCache cache, PreviewBaseline baseline, float time)
        {
            if (animation == null || animation.Timeline == null || cache == null || baseline == null)
            {
                return;
            }

            baseline.Restore();
            int enabledTrackCount = 0;
            int evaluatedBindingCount = 0;
            foreach (var track in animation.Timeline.Tracks)
            {
                if (track == null || !track.Enabled)
                {
                    continue;
                }

                enabledTrackCount++;
                if (ApplyTrack(track, cache, baseline, time))
                {
                    evaluatedBindingCount++;
                }
            }

            FaceMotionPreviewTrace.Trace(
                "E.ApplySummary",
                "time={0} enabledTracks={1} evaluatedBindings={2}",
                time,
                enabledTrackCount,
                evaluatedBindingCount);
        }

        private static bool ApplyTrack(FaceTrackData track, PreviewObjectCache cache, PreviewBaseline baseline, float time)
        {
            if (track.Kind == TrackKind.BlendShape && track.BlendShape != null)
            {
                var binding = new BlendShapeBinding(track.BlendShape.RendererPath, track.BlendShape.BlendShapeName);
                if (cache.TryGetBlendShape(binding, out var renderer, out int index)
                    && CanonicalMotionEvaluator.Instance.TryEvaluateFloat(track.BlendShape.Keys, time, out float value))
                {
                    baseline.Capture(renderer, index);
                    renderer.SetBlendShapeWeight(index, value);
                    float readBack = renderer.GetBlendShapeWeight(index);
                    FaceMotionPreviewTrace.Trace(
                        "F.ApplyBlendShape",
                        "renderer={0} name={1} time={2} value={3} afterSet={4} event={5}",
                        track.BlendShape.RendererPath,
                        track.BlendShape.BlendShapeName,
                        time,
                        value,
                        readBack,
                        Event.current == null ? "<none>" : Event.current.type.ToString());
                    return true;
                }
                else if (track.BlendShape != null)
                {
                    FaceMotionPreviewTrace.Trace(
                        "F.ApplyBlendShape.Miss",
                        "renderer={0} name={1} time={2} event={3}",
                        track.BlendShape.RendererPath,
                        track.BlendShape.BlendShapeName,
                        time,
                        Event.current == null ? "<none>" : Event.current.type.ToString());
                }

                return false;
            }

            if (!TrackKinds.IsTransform(track.Kind) || track.Transform == null
                || !cache.TryGetTransform(track.Transform.TransformPath, out var transform))
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
