using System;
using System.Collections.Generic;
using System.IO;
using FaceMotion.Animation;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Timeline;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Export
{
    /// <summary>Exports an authored timeline as a sampled Unity AnimationClip asset.</summary>
    public static class AnimationClipExporter
    {
        public const string BlendShapePropertyPrefix = "blendShape.";

        public static AnimationClipExportResult Export(FaceMotionAnimationData animation, string assetPath)
        {
            var diagnostics = new List<FaceMotionDiagnostic>();
            Validate(animation, assetPath, diagnostics);
            if (HasBlocking(diagnostics))
            {
                return new AnimationClipExportResult(null, false, diagnostics);
            }

            var createdFolders = new List<string>();
            if (!EnsureParentFolders(assetPath, createdFolders, out string parentFailure))
            {
                diagnostics.Add(Error(FaceMotionDiagnosticCodes.ExportCreateParentFailed, parentFailure, assetPath, "Choose a writable path under Assets."));
                return new AnimationClipExportResult(null, false, diagnostics);
            }

            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (mainAsset != null && existing == null)
            {
                diagnostics.Add(Error(FaceMotionDiagnosticCodes.ExportPathOccupied, "The destination is occupied by a non-AnimationClip asset.", assetPath, "Choose an empty .anim path or an existing AnimationClip."));
                return new AnimationClipExportResult(null, false, diagnostics);
            }

            var staged = new AnimationClip { name = Path.GetFileNameWithoutExtension(assetPath) };
            bool createdAsset = false;
            try
            {
                Populate(staged, animation);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(staged, assetPath);
                    existing = staged;
                    createdAsset = true;
                }
                else
                {
                    // Copying into the existing main asset keeps its .meta GUID and references intact.
                    EditorUtility.CopySerialized(staged, existing);
                    UnityEngine.Object.DestroyImmediate(staged);
                }

                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                diagnostics.Add(new FaceMotionDiagnostic(FaceMotionDiagnosticCodes.ExportSucceeded, FaceMotionDiagnosticSeverity.Info, "AnimationClip exported successfully.", assetPath, false, string.Empty));
                return new AnimationClipExportResult(existing, true, diagnostics);
            }
            catch (Exception exception)
            {
                if (existing != staged)
                {
                    UnityEngine.Object.DestroyImmediate(staged);
                }

                if (!createdAsset)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                    CleanupCreatedFolders(createdFolders);
                }

                diagnostics.Add(Error(FaceMotionDiagnosticCodes.ExportWriteFailed, exception.Message, assetPath, "Check that the destination is writable and try again."));
                return new AnimationClipExportResult(null, false, diagnostics);
            }
        }

        public static AnimationClipExportValidation Validate(FaceMotionAnimationData animation, string assetPath)
        {
            var diagnostics = new List<FaceMotionDiagnostic>();
            Validate(animation, assetPath, diagnostics);
            return new AnimationClipExportValidation(diagnostics);
        }

        /// <summary>Builds an unsaved clip for integration planning without touching the AssetDatabase.</summary>
        public static AnimationClip CreatePreview(FaceMotionAnimationData animation)
        {
            var preview = new AnimationClip { name = animation == null ? "FaceMotion Preview" : animation.DisplayName };
            Populate(preview, animation);
            return preview;
        }

        private static void Validate(FaceMotionAnimationData animation, string assetPath, List<FaceMotionDiagnostic> diagnostics)
        {
            if (animation == null || animation.Timeline == null)
            {
                diagnostics.Add(Error(FaceMotionDiagnosticCodes.ExportNoTimeline, "Select an animation with a timeline before exporting.", string.Empty, "Select a valid FaceMotion animation."));
                return;
            }

            if (!IsFinite(animation.Timeline.Duration) || animation.Timeline.Duration <= 0f)
            {
                diagnostics.Add(Error(FaceMotionDiagnosticCodes.ExportInvalidDuration, "Timeline duration must be finite and greater than zero.", animation.AnimationId, "Set a positive duration."));
            }

            if (!IsFinite(animation.Timeline.FrameRate) || animation.Timeline.FrameRate <= 0f)
            {
                diagnostics.Add(Error(FaceMotionDiagnosticCodes.ExportInvalidFrameRate, "Timeline frame rate must be finite and greater than zero.", animation.AnimationId, "Set a positive frame rate."));
            }

            if (!IsAssetsAnimationPath(assetPath))
            {
                diagnostics.Add(Error(FaceMotionDiagnosticCodes.ExportInvalidPath, "Export destination must be a .anim asset under Assets.", assetPath, "Choose a path under Assets with the .anim extension."));
            }

            foreach (var track in animation.Timeline.Tracks)
            {
                ValidateTrack(track, diagnostics);
            }
        }

        private static void ValidateTrack(FaceTrackData track, List<FaceMotionDiagnostic> diagnostics)
        {
            if (track == null)
            {
                diagnostics.Add(Error(FaceMotionDiagnosticCodes.ExportNullTrack, "Timeline contains a null track.", string.Empty, "Remove the null track."));
                return;
            }

            if (!track.Enabled)
            {
                return;
            }

            if (track.Kind == TrackKind.BlendShape)
            {
                if (track.BlendShape == null || string.IsNullOrEmpty(track.BlendShape.BlendShapeName) || track.BlendShape.Keys.Count == 0)
                {
                    diagnostics.Add(Error(FaceMotionDiagnosticCodes.ExportInvalidBlendShape, "An enabled blend shape track needs a name and at least one key.", track.TrackId, "Set the binding and add a key, or disable the track."));
                }
                else
                {
                    ValidateFloatKeys(track.BlendShape.Keys, track.TrackId, diagnostics);
                }

                return;
            }

            if (!TrackKinds.IsTransform(track.Kind) || track.Transform == null || track.Transform.Keys.Count == 0)
            {
                diagnostics.Add(Error(FaceMotionDiagnosticCodes.ExportInvalidTransform, "An enabled transform track needs a supported payload and at least one key.", track.TrackId, "Set the transform binding and add a key, or disable the track."));
            }
            else if (track.Kind == TrackKind.TransformRotation && track.Transform.RotationMode != MotionRotationMode.ShortestQuaternion)
            {
                diagnostics.Add(Error(FaceMotionDiagnosticCodes.ExportUnsupportedRotation, "Only ShortestQuaternion rotation tracks can be exported.", track.TrackId, "Use ShortestQuaternion rotation mode."));
            }
            else
            {
                ValidateVector3Keys(track.Transform.Keys, track.TrackId, diagnostics);
            }
        }

        private static void ValidateFloatKeys(IReadOnlyList<FloatKeyframeData> keys, string trackId, List<FaceMotionDiagnostic> diagnostics)
        {
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] == null || !IsFinite(keys[i].Time) || !IsFinite(keys[i].Value))
                {
                    diagnostics.Add(Error(FaceMotionDiagnosticCodes.ExportInvalidKey, "Blend shape keys must have finite time and value.", trackId, "Remove or repair the invalid key."));
                    return;
                }
            }
        }

        private static void ValidateVector3Keys(IReadOnlyList<Vector3KeyframeData> keys, string trackId, List<FaceMotionDiagnostic> diagnostics)
        {
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] == null || !IsFinite(keys[i].Time) || !IsFinite(keys[i].Value))
                {
                    diagnostics.Add(Error(FaceMotionDiagnosticCodes.ExportInvalidKey, "Transform keys must have finite time and value.", trackId, "Remove or repair the invalid key."));
                    return;
                }
            }
        }

        private static void Populate(AnimationClip clip, FaceMotionAnimationData animation)
        {
            float[] times = MotionSampler.CreateSampleTimes(animation.Timeline.Duration, animation.Timeline.FrameRate);
            clip.frameRate = animation.Timeline.FrameRate;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = animation.Timeline.Loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            foreach (var track in animation.Timeline.Tracks)
            {
                if (track == null || !track.Enabled)
                {
                    continue;
                }

                if (track.Kind == TrackKind.BlendShape)
                {
                    SetFloatCurve(clip, track.BlendShape.RendererPath, typeof(SkinnedMeshRenderer), BlendShapePropertyPrefix + track.BlendShape.BlendShapeName, times, t => EvaluateFloat(track, t));
                }
                else if (track.Kind == TrackKind.TransformPosition)
                {
                    SetVector3Curves(clip, track.Transform.TransformPath, "m_LocalPosition", times, t => EvaluatePosition(track, t));
                }
                else if (track.Kind == TrackKind.TransformScale)
                {
                    SetVector3Curves(clip, track.Transform.TransformPath, "m_LocalScale", times, t => EvaluateScale(track, t));
                }
                else if (track.Kind == TrackKind.TransformRotation)
                {
                    SetQuaternionCurves(clip, track.Transform.TransformPath, times, track);
                }
            }
        }

        private static void SetVector3Curves(AnimationClip clip, string path, string property, float[] times, Func<float, Vector3> evaluate)
        {
            SetFloatCurve(clip, path, typeof(Transform), property + ".x", times, t => evaluate(t).x);
            SetFloatCurve(clip, path, typeof(Transform), property + ".y", times, t => evaluate(t).y);
            SetFloatCurve(clip, path, typeof(Transform), property + ".z", times, t => evaluate(t).z);
        }

        private static void SetQuaternionCurves(AnimationClip clip, string path, float[] times, FaceTrackData track)
        {
            var x = new Keyframe[times.Length]; var y = new Keyframe[times.Length]; var z = new Keyframe[times.Length]; var w = new Keyframe[times.Length];
            Quaternion previous = Quaternion.identity;
            for (int i = 0; i < times.Length; i++)
            {
                Quaternion value = EvaluateRotation(track, times[i]);
                if (i > 0 && Quaternion.Dot(previous, value) < 0f) value = new Quaternion(-value.x, -value.y, -value.z, -value.w);
                previous = value;
                x[i] = new Keyframe(times[i], value.x); y[i] = new Keyframe(times[i], value.y); z[i] = new Keyframe(times[i], value.z); w[i] = new Keyframe(times[i], value.w);
            }

            SetCurve(clip, path, "m_LocalRotation.x", x); SetCurve(clip, path, "m_LocalRotation.y", y);
            SetCurve(clip, path, "m_LocalRotation.z", z); SetCurve(clip, path, "m_LocalRotation.w", w);
        }

        private static void SetFloatCurve(AnimationClip clip, string path, Type type, string property, float[] times, Func<float, float> evaluate)
        {
            var keys = new Keyframe[times.Length];
            for (int i = 0; i < times.Length; i++) keys[i] = new Keyframe(times[i], evaluate(times[i]));
            clip.SetCurve(path ?? string.Empty, type, property, new AnimationCurve(keys));
        }

        private static void SetCurve(AnimationClip clip, string path, string property, Keyframe[] keys)
        {
            clip.SetCurve(path ?? string.Empty, typeof(Transform), property, new AnimationCurve(keys));
        }

        private static float EvaluateFloat(FaceTrackData track, float time) { CanonicalMotionEvaluator.Instance.TryEvaluateFloat(track.BlendShape.Keys, time, out float value); return value; }
        private static Vector3 EvaluatePosition(FaceTrackData track, float time) { CanonicalMotionEvaluator.Instance.TryEvaluatePosition(track.Transform.Keys, time, out Vector3 value); return value; }
        private static Vector3 EvaluateScale(FaceTrackData track, float time) { CanonicalMotionEvaluator.Instance.TryEvaluateScale(track.Transform.Keys, time, out Vector3 value); return value; }
        private static Quaternion EvaluateRotation(FaceTrackData track, float time) { CanonicalMotionEvaluator.Instance.TryEvaluateRotation(track.Transform.Keys, time, out Quaternion value); return value; }
        private static bool HasBlocking(IReadOnlyList<FaceMotionDiagnostic> diagnostics) { for (int i = 0; i < diagnostics.Count; i++) if (diagnostics[i].Blocking) return true; return false; }
        private static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool IsFinite(Vector3 value) { return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z); }
        private static bool IsAssetsAnimationPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string[] segments = path.Replace('\\', '/').Split('/');
            if (segments.Length < 2 || segments[0] != "Assets" || !segments[segments.Length - 1].EndsWith(".anim", StringComparison.OrdinalIgnoreCase)) return false;
            for (int i = 0; i < segments.Length; i++) if (string.IsNullOrEmpty(segments[i]) || segments[i] == "." || segments[i] == "..") return false;
            return true;
        }
        private static bool EnsureParentFolders(string assetPath, List<string> createdFolders, out string failure)
        {
            string[] segments = assetPath.Replace('\\', '/').Split('/');
            string current = "Assets";
            for (int i = 1; i < segments.Length - 1; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(current, segments[i])))
                    {
                        CleanupCreatedFolders(createdFolders);
                        failure = "Could not create the parent folder '" + next + "'.";
                        return false;
                    }
                    createdFolders.Add(next);
                }
                current = next;
            }
            failure = null;
            return true;
        }
        private static void CleanupCreatedFolders(List<string> createdFolders)
        {
            for (int i = createdFolders.Count - 1; i >= 0; i--) if (AssetDatabase.IsValidFolder(createdFolders[i])) AssetDatabase.DeleteAsset(createdFolders[i]);
        }
        private static FaceMotionDiagnostic Error(string code, string message, string context, string fix) { return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Error, message, context, true, fix); }
    }

    public class AnimationClipExportValidation
    {
        public AnimationClipExportValidation(IReadOnlyList<FaceMotionDiagnostic> diagnostics) { Diagnostics = diagnostics; }
        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }
        public bool IsValid { get { for (int i = 0; i < Diagnostics.Count; i++) if (Diagnostics[i].Blocking) return false; return true; } }
    }

    public sealed class AnimationClipExportResult : AnimationClipExportValidation
    {
        public AnimationClipExportResult(AnimationClip clip, bool succeeded, IReadOnlyList<FaceMotionDiagnostic> diagnostics) : base(diagnostics) { Clip = clip; Succeeded = succeeded; }
        public AnimationClip Clip { get; }
        public bool Succeeded { get; }
    }
}
