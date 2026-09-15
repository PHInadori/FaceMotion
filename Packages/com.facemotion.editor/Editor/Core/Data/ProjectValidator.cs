using System;
using System.Collections.Generic;
using FaceMotion.Diagnostics;
using FaceMotion.Timeline;
using FaceMotion.Versioning;
using UnityEngine;

namespace FaceMotion.Data
{
    /// <summary>A read-only snapshot of validation results.</summary>
    public sealed class ValidationReport
    {
        private readonly IReadOnlyList<FaceMotionDiagnostic> _diagnostics;

        public ValidationReport(IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        {
            _diagnostics = diagnostics ?? new List<FaceMotionDiagnostic>();
        }

        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics => _diagnostics;

        public bool HasBlocking
        {
            get
            {
                for (int i = 0; i < _diagnostics.Count; i++)
                {
                    if (_diagnostics[i].Blocking)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>True when no diagnostic is blocking.</summary>
        public bool IsValid => !HasBlocking;
    }

    /// <summary>
    /// Structural, provenance, and schema validation for a project. Blocking is independent
    /// of severity so future export and integration steps can reason about safety.
    /// </summary>
    public static class ProjectValidator
    {
        public const float TimeTolerance = 1e-4f;

        public static ValidationReport Validate(FaceMotionProject project)
        {
            var diagnostics = new List<FaceMotionDiagnostic>();
            if (project == null)
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.NullProject,
                    "The project is null.",
                    string.Empty,
                    "Load a valid project."));
                return new ValidationReport(diagnostics);
            }

            ValidateProject(project, diagnostics);
            return new ValidationReport(diagnostics);
        }

        private static void ValidateProject(FaceMotionProject project, List<FaceMotionDiagnostic> diagnostics)
        {
            if (!StableId.IsValid(project.ProjectId))
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.InvalidProjectId,
                    "Project ID is not a valid stable ID.",
                    project.ProjectId,
                    "Regenerate the project ID with StableId.New()."));
            }

            if (project.SchemaVersion > FaceMotionVersions.ProjectSchemaVersion)
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.FutureSchema,
                    $"Project uses schema {project.SchemaVersion}, newer than this tool's {FaceMotionVersions.ProjectSchemaVersion}.",
                    project.ProjectId,
                    "Upgrade this tool to a version that reads the reported schema."));
            }
            else if (project.SchemaVersion <= 0)
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.UninitializedSchema,
                    "Project schema version is uninitialized.",
                    project.ProjectId,
                    "Initialize the project through FaceMotionProject.CreateNew()."));
            }

            var seenAnimationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var animation in project.Animations)
            {
                if (animation == null)
                {
                    diagnostics.Add(Blocking(
                        FaceMotionDiagnosticCodes.NullAnimation,
                        "An animation entry is null.",
                        project.ProjectId,
                        "Remove the null entry."));
                    continue;
                }

                ValidateAnimation(project, animation, seenAnimationIds, diagnostics);
            }

            ValidateGenerationRegistry(project, diagnostics);
        }

        private static void ValidateAnimation(
            FaceMotionProject project,
            FaceMotionAnimationData animation,
            HashSet<string> seenIds,
            List<FaceMotionDiagnostic> diagnostics)
        {
            string animationId = animation.AnimationId;
            if (!StableId.IsValid(animationId))
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.InvalidAnimationId,
                    "Animation ID is not a valid stable ID.",
                    animationId,
                    "Repair or regenerate the animation ID."));
            }
            else if (!seenIds.Add(animationId))
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.DuplicateAnimationId,
                    "Animation ID appears more than once.",
                    animationId,
                    "Assign a unique animation ID."));
            }

            if (animation.Timeline == null)
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.NullTimeline,
                    "Animation has no timeline.",
                    animationId,
                    "Assign a timeline."));
                return;
            }

            ValidateTimeline(project, animation.Timeline, animationId, diagnostics);
        }

        private static void ValidateTimeline(
            FaceMotionProject project,
            FaceTimelineData timeline,
            string animationId,
            List<FaceMotionDiagnostic> diagnostics)
        {
            float duration = timeline.Duration;
            float frameRate = timeline.FrameRate;

            if (!IsFinite(duration) || duration <= 0f)
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.InvalidDuration,
                    "Timeline duration must be a finite value greater than zero.",
                    animationId,
                    "Set a positive duration."));
            }

            if (!IsFinite(frameRate) || frameRate <= 0f)
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.InvalidFrameRate,
                    "Timeline frame rate must be a finite value greater than zero.",
                    animationId,
                    "Set a positive frame rate."));
            }

            var seenTrackIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var track in timeline.Tracks)
            {
                if (track == null)
                {
                    diagnostics.Add(Blocking(
                        FaceMotionDiagnosticCodes.NullTrack,
                        "A track entry is null.",
                        animationId,
                        "Remove the null entry."));
                    continue;
                }

                ValidateTrack(project, track, duration, seenTrackIds, diagnostics);
            }
        }

        private static void ValidateTrack(
            FaceMotionProject project,
            FaceTrackData track,
            float duration,
            HashSet<string> seenIds,
            List<FaceMotionDiagnostic> diagnostics)
        {
            string trackId = track.TrackId;
            if (!StableId.IsValid(trackId))
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.InvalidTrackId,
                    "Track ID is not a valid stable ID.",
                    trackId,
                    "Repair or regenerate the track ID."));
            }
            else if (!seenIds.Add(trackId))
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.DuplicateTrackId,
                    "Track ID appears more than once.",
                    trackId,
                    "Assign a unique track ID."));
            }

            // Unity's serializer materializes null class fields with a default instance,
            // so the inactive payload field is not a reliable signal after a round trip.
            // TrackKind is the single source of truth for which payload is active.
            if (track.Kind == TrackKind.BlendShape)
            {
                if (track.BlendShape == null)
                {
                    diagnostics.Add(Blocking(
                        FaceMotionDiagnosticCodes.PayloadMismatch,
                        "A BlendShape track requires an active BlendShape payload.",
                        trackId,
                        "Run ProjectNormalizer or recreate the track."));
                }
                else
                {
                    ValidateFloatKeys(project, track, track.BlendShape, duration, diagnostics);
                }
            }
            else if (TrackKinds.IsTransform(track.Kind))
            {
                if (track.Transform == null)
                {
                    diagnostics.Add(Blocking(
                        FaceMotionDiagnosticCodes.PayloadMismatch,
                        "A transform track requires an active Transform payload.",
                        trackId,
                        "Run ProjectNormalizer or recreate the track."));
                }
                else
                {
                    if (track.Kind == TrackKind.TransformRotation
                        && track.Transform.RotationMode != MotionRotationMode.ShortestQuaternion)
                    {
                        diagnostics.Add(Blocking(
                            FaceMotionDiagnosticCodes.UnsupportedRotationMode,
                            $"Rotation mode {track.Transform.RotationMode} is not supported by the first evaluator.",
                            trackId,
                            "Use ShortestQuaternion."));
                    }

                    ValidateVector3Keys(project, track, track.Transform, duration, diagnostics);
                }
            }
            else
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.PayloadMismatch,
                    $"Track kind {track.Kind} is not supported.",
                    trackId,
                    "Use a supported TrackKind."));
            }
        }

        private static void ValidateFloatKeys(
            FaceMotionProject project,
            FaceTrackData track,
            BlendShapeTrackPayload payload,
            float duration,
            List<FaceMotionDiagnostic> diagnostics)
        {
            var keys = payload.Keys;
            if (keys == null)
            {
                return;
            }

            var seenKeyIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (key == null)
                {
                    diagnostics.Add(Blocking(
                        FaceMotionDiagnosticCodes.NullKey,
                        "A key entry is null.",
                        track.TrackId,
                        "Remove the null entry."));
                    continue;
                }

                ValidateKeyBase(project, key.KeyId, key.Time, key.Origin, duration, track.TrackId, seenKeyIds, diagnostics);

                if (!IsFinite(key.Value))
                {
                    diagnostics.Add(Blocking(
                        FaceMotionDiagnosticCodes.NonFiniteKeyValue,
                        "Key value is NaN or Infinity.",
                        track.TrackId,
                        "Set a finite key value."));
                }
            }

            ValidateSegmentOrdering(keys, k => k.Time, track.TrackId, diagnostics);
        }

        private static void ValidateVector3Keys(
            FaceMotionProject project,
            FaceTrackData track,
            TransformTrackPayload payload,
            float duration,
            List<FaceMotionDiagnostic> diagnostics)
        {
            var keys = payload.Keys;
            if (keys == null)
            {
                return;
            }

            var seenKeyIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (key == null)
                {
                    diagnostics.Add(Blocking(
                        FaceMotionDiagnosticCodes.NullKey,
                        "A key entry is null.",
                        track.TrackId,
                        "Remove the null entry."));
                    continue;
                }

                ValidateKeyBase(project, key.KeyId, key.Time, key.Origin, duration, track.TrackId, seenKeyIds, diagnostics);

                if (!IsFinite(key.Value))
                {
                    diagnostics.Add(Blocking(
                        FaceMotionDiagnosticCodes.NonFiniteKeyValue,
                        "Key value contains NaN or Infinity.",
                        track.TrackId,
                        "Set a finite key value."));
                }
            }

            ValidateSegmentOrdering(keys, k => k.Time, track.TrackId, diagnostics);
        }

        private static void ValidateKeyBase(
            FaceMotionProject project,
            string keyId,
            float time,
            KeyOrigin origin,
            float duration,
            string contextId,
            HashSet<string> seenIds,
            List<FaceMotionDiagnostic> diagnostics)
        {
            if (!StableId.IsValid(keyId))
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.InvalidKeyId,
                    "Key ID is not a valid stable ID.",
                    keyId,
                    "Repair or regenerate the key ID."));
            }
            else if (!seenIds.Add(keyId))
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.DuplicateKeyId,
                    "Key ID appears more than once.",
                    keyId,
                    "Assign a unique key ID."));
            }

            if (!IsFinite(time))
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.NonFiniteKeyTime,
                    "Key time is NaN or Infinity.",
                    contextId,
                    "Set a finite key time."));
                return;
            }

            if (time < 0f)
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.NegativeKeyTime,
                    "Key time is before zero.",
                    contextId,
                    "Move the key into the timeline domain."));
            }

            if (IsFinite(duration) && duration > 0f && time > duration + TimeTolerance)
            {
                diagnostics.Add(Warning(
                    FaceMotionDiagnosticCodes.KeyBeyondDuration,
                    "Key time is beyond the timeline duration.",
                    contextId,
                    "Move the key, or extend the duration."));
            }

            if (origin.Kind == OriginKind.Manual)
            {
                if (!string.IsNullOrEmpty(origin.GenerationId))
                {
                    diagnostics.Add(Warning(
                        FaceMotionDiagnosticCodes.ManualKeyWithGenerationId,
                        "A manual key carries an unexpected generation ID.",
                        contextId,
                        "Clear the generation ID for a manual key."));
                }
            }
            else
            {
                if (string.IsNullOrEmpty(origin.GenerationId))
                {
                    diagnostics.Add(Warning(
                        FaceMotionDiagnosticCodes.OrphanedGenerationKey,
                        "A generated key has no generation ID.",
                        contextId,
                        "Assign the key to the generation that produced it."));
                }
                else if (!project.HasGeneration(origin.GenerationId))
                {
                    diagnostics.Add(Warning(
                        FaceMotionDiagnosticCodes.OrphanedGenerationKey,
                        "A generated key references a missing generation record.",
                        contextId,
                        "Restore the generation record, or mark the key manual."));
                }
            }
        }

        private static void ValidateSegmentOrdering<T>(
            IReadOnlyList<T> keys,
            Func<T, float> timeOf,
            string contextId,
            List<FaceMotionDiagnostic> diagnostics)
        {
            if (keys == null)
            {
                return;
            }

            for (int i = 1; i < keys.Count; i++)
            {
                float previous = timeOf(keys[i - 1]);
                float current = timeOf(keys[i]);
                if (current < previous - TimeTolerance)
                {
                    diagnostics.Add(Blocking(
                        FaceMotionDiagnosticCodes.UnsortedKeys,
                        "Keys are not sorted by ascending time; evaluation requires sorted segments.",
                        contextId,
                        "Run ProjectNormalizer."));
                    break;
                }

                if (Mathf.Abs(current - previous) <= TimeTolerance)
                {
                    diagnostics.Add(Warning(
                        FaceMotionDiagnosticCodes.DuplicateKeyTime,
                        "Two keys share the same time.",
                        contextId,
                        "Separate or deduplicate the overlapping keys."));
                }
            }
        }

        private static void ValidateGenerationRegistry(
            FaceMotionProject project,
            List<FaceMotionDiagnostic> diagnostics)
        {
            var seenGenerationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in project.Generations)
            {
                if (record == null)
                {
                    diagnostics.Add(Blocking(
                        FaceMotionDiagnosticCodes.NullGenerationRecord,
                        "A generation record entry is null.",
                        project.ProjectId,
                        "Remove the null entry."));
                    continue;
                }

                if (!string.IsNullOrEmpty(record.GenerationId) && !seenGenerationIds.Add(record.GenerationId))
                {
                    diagnostics.Add(Warning(
                        FaceMotionDiagnosticCodes.DuplicateGenerationId,
                        "Generation ID appears more than once.",
                        record.GenerationId,
                        "Assign a unique generation ID."));
                }
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
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