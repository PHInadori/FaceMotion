using System;
using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Generation;
using FaceMotion.Timeline;
using FaceMotion.Versioning;
using UnityEngine;

namespace FaceMotion.Serialization
{
    /// <summary>
    /// Migrates a legacy (schema 0 = "no version field") project to the current initial
    /// public schema. The migration is deterministic and idempotent: valid stable IDs are
    /// never changed, the first occurrence of a duplicate keeps its ID, recoverable numeric
    /// corruption is clamped to safe defaults, provenance is preserved, and unsafe data
    /// (invalid track kinds / unsupported rotation modes) is left untouched and reported
    /// as a blocking FM-MIG-MALFORMED diagnostic so nothing is silently destroyed.
    /// </summary>
    public sealed class ProjectLegacyMigrator : IProjectMigrator<FaceMotionProject>
    {
        public int FromVersion => FaceMotionVersions.LegacySchemaVersion;

        public int ToVersion => FaceMotionVersions.ProjectSchemaVersion;

        public bool CanMigrate(int version)
        {
            return version == FaceMotionVersions.LegacySchemaVersion;
        }

        public bool TryMigrate(
            FaceMotionProject sourceCopy,
            out FaceMotionProject migratedCopy,
            ICollection<FaceMotionDiagnostic> diagnostics)
        {
            migratedCopy = sourceCopy;
            if (sourceCopy == null || !CanMigrate(sourceCopy.SchemaVersion))
            {
                return false;
            }

            RepairProjectId(sourceCopy, diagnostics);
            RepairAnimations(sourceCopy, diagnostics);
            RepairGenerationRecords(sourceCopy, diagnostics);
            return true;
        }

        private static void RepairProjectId(FaceMotionProject project, ICollection<FaceMotionDiagnostic> diagnostics)
        {
            if (MigrationIds.NeedsRepair(project.ProjectId))
            {
                project.SetProjectIdForMigration(StableId.New());
                diagnostics.Add(Warning(FaceMotionDiagnosticCodes.IdRepaired, "The project had no valid stable ID; a new one was generated.", project.ProjectId, "Keep the generated ID."));
            }
        }

        private static void RepairAnimations(FaceMotionProject project, ICollection<FaceMotionDiagnostic> diagnostics)
        {
            var seenAnimationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var animation in project.Animations)
            {
                if (animation == null)
                {
                    diagnostics.Add(Warning(FaceMotionDiagnosticCodes.PartialRecovery, "A null animation entry was removed during migration.", project.ProjectId, "Recreate the animation if it was unexpected."));
                    continue;
                }

                animation.SetAnimationIdForMigration(MigrationIds.RepairUnique(
                    animation.AnimationId, seenAnimationIds, out bool idRepaired, out bool idDuplicated));
                if (idRepaired)
                {
                    diagnostics.Add(Warning(FaceMotionDiagnosticCodes.IdRepaired, "An animation had no valid stable ID; a new one was generated.", animation.AnimationId, "Keep the generated ID."));
                }
                else if (idDuplicated)
                {
                    diagnostics.Add(Warning(FaceMotionDiagnosticCodes.DuplicateId, "A duplicate animation ID was replaced with a new stable ID.", animation.AnimationId, "Keep the generated ID."));
                }

                if (animation.Timeline == null)
                {
                    diagnostics.Add(Warning(FaceMotionDiagnosticCodes.PartialRecovery, "An animation had no timeline; a default timeline was created.", animation.AnimationId, "Adjust the timeline."));
                    continue;
                }

                RepairTimeline(animation, diagnostics);
            }
        }

        private static void RepairTimeline(FaceMotionAnimationData animation, ICollection<FaceMotionDiagnostic> diagnostics)
        {
            var timeline = animation.Timeline;
            bool durationFixed = !IsFinite(timeline.Duration) || timeline.Duration <= 0f;
            if (durationFixed)
            {
                timeline.Duration = 1f;
                diagnostics.Add(Warning(FaceMotionDiagnosticCodes.PartialRecovery, "Timeline duration was non-finite or non-positive; reset to 1 second.", animation.AnimationId, "Adjust the duration."));
            }

            bool frameRateFixed = !IsFinite(timeline.FrameRate) || timeline.FrameRate <= 0f;
            if (frameRateFixed)
            {
                timeline.FrameRate = 60f;
                diagnostics.Add(Warning(FaceMotionDiagnosticCodes.PartialRecovery, "Timeline frame rate was non-finite or non-positive; reset to 60 fps.", animation.AnimationId, "Adjust the frame rate."));
            }

            var seenTrackIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var track in timeline.Tracks)
            {
                if (track == null)
                {
                    diagnostics.Add(Warning(FaceMotionDiagnosticCodes.PartialRecovery, "A null track entry was removed during migration.", animation.AnimationId, "Recreate the track if it was unexpected."));
                    continue;
                }

                if (!IsSupportedKind(track.Kind))
                {
                    diagnostics.Add(Blocking(FaceMotionDiagnosticCodes.MalformedData, $"Track kind {track.Kind} is not supported; the track was not touched.", track.TrackId, "Remove or repair the track."));
                    continue;
                }

                track.SetTrackIdForMigration(MigrationIds.RepairUnique(
                    track.TrackId, seenTrackIds, out bool idRepaired, out bool idDuplicated));
                if (idRepaired)
                {
                    diagnostics.Add(Warning(FaceMotionDiagnosticCodes.IdRepaired, "A track had no valid stable ID; a new one was generated.", track.TrackId, "Keep the generated ID."));
                }
                else if (idDuplicated)
                {
                    diagnostics.Add(Warning(FaceMotionDiagnosticCodes.DuplicateId, "A duplicate track ID was replaced with a new stable ID.", track.TrackId, "Keep the generated ID."));
                }

                if (track.Kind == TrackKind.BlendShape)
                {
                    if (track.BlendShape == null)
                    {
                        diagnostics.Add(Warning(FaceMotionDiagnosticCodes.PartialRecovery, "A BlendShape track had no payload; an empty payload was created.", track.TrackId, "Bind the track to a blend shape."));
                        continue;
                    }

                    RepairFloatKeys(track, track.BlendShape, diagnostics);
                }
                else
                {
                    if (track.Transform == null)
                    {
                        diagnostics.Add(Warning(FaceMotionDiagnosticCodes.PartialRecovery, "A transform track had no payload; an empty payload was created.", track.TrackId, "Bind the track to a transform."));
                        continue;
                    }

                    if (track.Kind == TrackKind.TransformRotation
                        && track.Transform.RotationMode != MotionRotationMode.ShortestQuaternion)
                    {
                        diagnostics.Add(Blocking(FaceMotionDiagnosticCodes.MalformedData, $"Rotation mode {track.Transform.RotationMode} is not supported; the track was not touched.", track.TrackId, "Use ShortestQuaternion."));
                        continue;
                    }

                    RepairVector3Keys(track, track.Transform, diagnostics);
                }
            }
        }

        private static void RepairFloatKeys(
            FaceTrackData track,
            BlendShapeTrackPayload payload,
            ICollection<FaceMotionDiagnostic> diagnostics)
        {
            var seenKeyIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in payload.Keys)
            {
                if (key == null)
                {
                    diagnostics.Add(Warning(FaceMotionDiagnosticCodes.PartialRecovery, "A null key entry was removed during migration.", track.TrackId, "Recreate the key if it was unexpected."));
                    continue;
                }

                RepairKeyId(track, key.KeyId, key.SetKeyIdForMigration, seenKeyIds, diagnostics);

                if (!IsFinite(key.Time) || key.Time < 0f)
                {
                    key.Time = 0f;
                    diagnostics.Add(Warning(FaceMotionDiagnosticCodes.PartialRecovery, "A key time was non-finite or negative; reset to zero.", track.TrackId, "Move the key onto the timeline."));
                }

                if (!IsFinite(key.Value))
                {
                    key.Value = 0f;
                    diagnostics.Add(Warning(FaceMotionDiagnosticCodes.PartialRecovery, "A key value was NaN or Infinity; reset to zero.", track.TrackId, "Set a finite value."));
                }
            }
        }

        private static void RepairVector3Keys(
            FaceTrackData track,
            TransformTrackPayload payload,
            ICollection<FaceMotionDiagnostic> diagnostics)
        {
            var seenKeyIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in payload.Keys)
            {
                if (key == null)
                {
                    diagnostics.Add(Warning(FaceMotionDiagnosticCodes.PartialRecovery, "A null key entry was removed during migration.", track.TrackId, "Recreate the key if it was unexpected."));
                    continue;
                }

                RepairKeyId(track, key.KeyId, key.SetKeyIdForMigration, seenKeyIds, diagnostics);

                if (!IsFinite(key.Time) || key.Time < 0f)
                {
                    key.Time = 0f;
                    diagnostics.Add(Warning(FaceMotionDiagnosticCodes.PartialRecovery, "A key time was non-finite or negative; reset to zero.", track.TrackId, "Move the key onto the timeline."));
                }

                if (!IsFinite(key.Value))
                {
                    key.Value = UnityEngine.Vector3.zero;
                    diagnostics.Add(Warning(FaceMotionDiagnosticCodes.PartialRecovery, "A key value contained NaN or Infinity; reset to zero.", track.TrackId, "Set a finite value."));
                }
            }
        }

        /// <summary>Runs the shared key-ID repair, applies it to the key, and reports it.</summary>
        private static void RepairKeyId(FaceTrackData track, string keyId, Action<string> applyId, HashSet<string> seenKeyIds, ICollection<FaceMotionDiagnostic> diagnostics)
        {
            string repaired = MigrationIds.RepairUnique(keyId, seenKeyIds, out bool idRepaired, out bool idDuplicated);
            if (idRepaired)
            {
                diagnostics.Add(Warning(FaceMotionDiagnosticCodes.IdRepaired, "A key had no valid stable ID; a new one was generated.", track.TrackId, "Keep the generated ID."));
            }
            else if (idDuplicated)
            {
                diagnostics.Add(Warning(FaceMotionDiagnosticCodes.DuplicateId, "A duplicate key ID was replaced with a new stable ID.", track.TrackId, "Keep the generated ID."));
            }

            applyId(repaired);
        }

        private static void RepairGenerationRecords(FaceMotionProject project, ICollection<FaceMotionDiagnostic> diagnostics)
        {
            var seenGenerationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in project.Generations)
            {
                if (record == null)
                {
                    diagnostics.Add(Warning(FaceMotionDiagnosticCodes.PartialRecovery, "A null generation record was removed during migration.", project.ProjectId, "Recreate the record if it was unexpected."));
                    continue;
                }

                record.SetGenerationIdForMigration(MigrationIds.RepairUnique(
                    record.GenerationId, seenGenerationIds, out bool idRepaired, out bool idDuplicated));
                if (idRepaired)
                {
                    diagnostics.Add(Warning(FaceMotionDiagnosticCodes.IdRepaired, "A generation record had no valid stable ID; a new one was generated.", record.GenerationId, "Keep the generated ID."));
                }
                else if (idDuplicated)
                {
                    diagnostics.Add(Warning(FaceMotionDiagnosticCodes.DuplicateId, "A duplicate generation record ID was replaced with a new stable ID.", record.GenerationId, "Keep the generated ID."));
                }

                if (record.AlgorithmVersion == FaceMotionVersions.LegacyGeneratorAlgorithmVersion)
                {
                    diagnostics.Add(Warning(FaceMotionDiagnosticCodes.PartialRecovery, "A generation record predates algorithm versions; its pass cannot be regenerated with a guaranteed algorithm version.", record.GenerationId, "Regenerate if you need fresh output."));
                }

                if (string.IsNullOrEmpty(record.SettingsSnapshot))
                {
                    diagnostics.Add(Warning(FaceMotionDiagnosticCodes.PartialRecovery, "A generation record has no settings snapshot; it cannot be reproduced from provenance.", record.GenerationId, "Regenerate if you need fresh output."));
                    continue;
                }

                var snapshot = JsonUtility.FromJson<GenerationSettingsSnapshot>(record.SettingsSnapshot);
                if (snapshot == null || snapshot.FormatVersion != 1)
                {
                    diagnostics.Add(Warning(FaceMotionDiagnosticCodes.PartialRecovery, "A generation record's settings snapshot uses an unknown format; it cannot be reproduced from provenance.", record.GenerationId, "Regenerate if you need fresh output."));
                }
            }
        }

        private static bool IsSupportedKind(TrackKind kind)
        {
            return (kind == TrackKind.BlendShape || TrackKinds.IsTransform(kind))
                && Enum.IsDefined(typeof(TrackKind), kind);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(UnityEngine.Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static FaceMotionDiagnostic Warning(string code, string message, string contextId, string fix)
        {
            return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Warning, message, contextId, false, fix);
        }

        private static FaceMotionDiagnostic Blocking(string code, string message, string contextId, string fix)
        {
            return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Error, message, contextId, true, fix);
        }
    }
}