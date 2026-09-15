using System;
using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Timeline;

namespace FaceMotion.Avatar
{
    /// <summary>Result of validating one track's binding against an avatar.</summary>
    public sealed class TrackBindingValidation
    {
        public TrackBindingValidation(
            string animationId,
            string trackId,
            TrackKind kind,
            MappingResolutionStatus status,
            FaceMotionDiagnostic diagnostic)
        {
            AnimationId = animationId ?? string.Empty;
            TrackId = trackId ?? string.Empty;
            Kind = kind;
            Status = status;
            Diagnostic = diagnostic;
        }

        public string AnimationId { get; }

        public string TrackId { get; }

        public TrackKind Kind { get; }

        public MappingResolutionStatus Status { get; }

        /// <summary>Null when the binding resolves uniquely.</summary>
        public FaceMotionDiagnostic Diagnostic { get; }
    }

    /// <summary>
    /// Validates the bindings embedded in a project's tracks against one avatar index.
    /// Read-only: validation never mutates data, and it is independent of AvatarMappingProfile.
    /// </summary>
    public static class AvatarTrackBindingValidator
    {
        public static IReadOnlyList<TrackBindingValidation> ValidateTimeline(
            FaceMotionProject project,
            AvatarIndex index)
        {
            var results = new List<TrackBindingValidation>();
            if (project == null || index == null || project.Animations == null)
            {
                return results;
            }

            foreach (var animation in project.Animations)
            {
                if (animation == null || animation.Timeline == null)
                {
                    continue;
                }

                foreach (var track in animation.Timeline.Tracks)
                {
                    if (track == null)
                    {
                        continue;
                    }

                    results.Add(ValidateTrack(animation.AnimationId, track, index));
                }
            }

            return results;
        }

        private static TrackBindingValidation ValidateTrack(
            string animationId,
            FaceTrackData track,
            AvatarIndex index)
        {
            if (track.Kind == TrackKind.BlendShape)
            {
                if (track.BlendShape == null)
                {
                    return new TrackBindingValidation(animationId, track.TrackId, track.Kind, MappingResolutionStatus.Invalid, Blocking(
                        FaceMotionDiagnosticCodes.InvalidBinding,
                        "A blend shape track has no active payload.",
                        track.TrackId,
                        "Repair the track."));
                }

                string rendererPath = track.BlendShape.RendererPath;
                string blendShapeName = track.BlendShape.BlendShapeName;
                if (string.IsNullOrEmpty(rendererPath))
                {
                    return new TrackBindingValidation(animationId, track.TrackId, track.Kind, MappingResolutionStatus.Invalid, Blocking(
                        FaceMotionDiagnosticCodes.InvalidBinding,
                        "A blend shape track has no renderer path.",
                        track.TrackId,
                        "Bind the track to a renderer."));
                }

                if (!index.HasRenderer(rendererPath))
                {
                    return new TrackBindingValidation(animationId, track.TrackId, track.Kind, MappingResolutionStatus.MissingRenderer, Warning(
                        FaceMotionDiagnosticCodes.MissingRenderer,
                        $"Renderer \"{rendererPath}\" is absent from the avatar.",
                        track.TrackId,
                        "Point the track at an existing renderer."));
                }

                if (string.IsNullOrEmpty(blendShapeName))
                {
                    return new TrackBindingValidation(animationId, track.TrackId, track.Kind, MappingResolutionStatus.Invalid, Blocking(
                        FaceMotionDiagnosticCodes.InvalidBinding,
                        "A blend shape track has no blend shape name.",
                        track.TrackId,
                        "Bind the track to a blend shape."));
                }

                var identity = new BlendShapeBinding(rendererPath, blendShapeName);
                BlendShapeResolution resolution = index.ResolveBlendShape(identity);
                switch (resolution.Status)
                {
                    case NameResolutionStatus.Unique:
                        return new TrackBindingValidation(animationId, track.TrackId, track.Kind, MappingResolutionStatus.Resolved, null);
                    case NameResolutionStatus.Ambiguous:
                        return new TrackBindingValidation(animationId, track.TrackId, track.Kind, MappingResolutionStatus.Ambiguous, Warning(
                            FaceMotionDiagnosticCodes.AmbiguousBinding,
                            $"Blend shape \"{blendShapeName}\" on \"{rendererPath}\" is ambiguous ({resolution.Count} matches).",
                            track.TrackId,
                            "Rename the duplicate blend shape, or rebind."));
                    default:
                        return new TrackBindingValidation(animationId, track.TrackId, track.Kind, MappingResolutionStatus.MissingBlendShape, Warning(
                            FaceMotionDiagnosticCodes.MissingBlendShape,
                            $"Blend shape \"{blendShapeName}\" is absent from renderer \"{rendererPath}\".",
                            track.TrackId,
                            "Point the track at an existing blend shape."));
                }
            }

            if (TrackKinds.IsTransform(track.Kind))
            {
                if (track.Transform == null)
                {
                    return new TrackBindingValidation(animationId, track.TrackId, track.Kind, MappingResolutionStatus.Invalid, Blocking(
                        FaceMotionDiagnosticCodes.InvalidBinding,
                        "A transform track has no active payload.",
                        track.TrackId,
                        "Repair the track."));
                }

                string transformPath = track.Transform.TransformPath;
                if (string.IsNullOrEmpty(transformPath))
                {
                    return new TrackBindingValidation(animationId, track.TrackId, track.Kind, MappingResolutionStatus.Invalid, Blocking(
                        FaceMotionDiagnosticCodes.InvalidBinding,
                        "A transform track has no transform path.",
                        track.TrackId,
                        "Bind the track to a transform."));
                }

                TransformResolution resolution = index.ResolveTransform(transformPath);
                switch (resolution.Status)
                {
                    case NameResolutionStatus.Unique:
                        return new TrackBindingValidation(animationId, track.TrackId, track.Kind, MappingResolutionStatus.Resolved, null);
                    case NameResolutionStatus.Ambiguous:
                        return new TrackBindingValidation(animationId, track.TrackId, track.Kind, MappingResolutionStatus.Ambiguous, Warning(
                            FaceMotionDiagnosticCodes.AmbiguousBinding,
                            $"Transform path \"{transformPath}\" is ambiguous ({resolution.Count} matches).",
                            track.TrackId,
                            "Rename the sibling transform, or rebind."));
                    default:
                        return new TrackBindingValidation(animationId, track.TrackId, track.Kind, MappingResolutionStatus.MissingTransform, Warning(
                            FaceMotionDiagnosticCodes.MissingTransform,
                            $"Transform \"{transformPath}\" is absent from the avatar.",
                            track.TrackId,
                            "Point the track at an existing transform."));
                }
            }

            return new TrackBindingValidation(animationId, track.TrackId, track.Kind, MappingResolutionStatus.Invalid, Warning(
                FaceMotionDiagnosticCodes.InvalidBinding,
                $"Track kind {track.Kind} is not bindable.",
                track.TrackId,
                "Use a supported TrackKind."));
        }

        private static FaceMotionDiagnostic Blocking(string code, string message, string contextId, string fix)
        {
            return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Error, message, contextId, true, fix);
        }

        private static FaceMotionDiagnostic Warning(string code, string message, string contextId, string fix)
        {
            return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Warning, message, contextId, false, fix);
        }
    }
}