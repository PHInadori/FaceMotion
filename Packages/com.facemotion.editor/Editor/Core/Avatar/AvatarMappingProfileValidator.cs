using System;
using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Versioning;

namespace FaceMotion.Avatar
{
    /// <summary>
    /// Structural, schema, and identity validation of a mapping profile. Blocking matches
    /// the project validator philosophy; nothing here mutates the profile.
    /// </summary>
    public static class AvatarMappingProfileValidator
    {
        public static ValidationReport Validate(AvatarMappingProfile profile)
        {
            var diagnostics = new List<FaceMotionDiagnostic>();
            if (profile == null)
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.NullMappingProfile,
                    "The mapping profile is null.",
                    string.Empty,
                    "Load a valid mapping profile."));
                return new ValidationReport(diagnostics);
            }

            ValidateProfile(profile, diagnostics);
            return new ValidationReport(diagnostics);
        }

        private static void ValidateProfile(AvatarMappingProfile profile, List<FaceMotionDiagnostic> diagnostics)
        {
            if (!StableId.IsValid(profile.ProfileId))
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.InvalidProfileId,
                    "Mapping profile ID is not a valid stable ID.",
                    profile.ProfileId,
                    "Regenerate the profile ID with StableId.New()."));
            }

            if (profile.SchemaVersion > FaceMotionVersions.MappingProfileSchemaVersion)
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.FutureProfileSchema,
                    $"Mapping profile uses schema {profile.SchemaVersion}, newer than this tool's {FaceMotionVersions.MappingProfileSchemaVersion}.",
                    profile.ProfileId,
                    "Upgrade this tool to a version that reads the reported schema."));
            }
            else if (profile.SchemaVersion <= 0)
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.UninitializedProfileSchema,
                    "Mapping profile schema version is uninitialized.",
                    profile.ProfileId,
                    "Initialize the profile through AvatarMappingProfile.CreateNew()."));
            }

            if (profile.AvatarFingerprint != null && !profile.AvatarFingerprint.IsValid)
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.InvalidFingerprint,
                    "The stored avatar fingerprint is malformed.",
                    profile.ProfileId,
                    "Rebind the profile to a scanned avatar to refresh the fingerprint."));
            }

            var seenEntryIds = new HashSet<string>(StringComparer.Ordinal);
            var seenLogicalTargets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in profile.Mappings)
            {
                if (entry == null)
                {
                    diagnostics.Add(Blocking(
                        FaceMotionDiagnosticCodes.InvalidEntryId,
                        "A mapping entry is null.",
                        profile.ProfileId,
                        "Remove the null entry."));
                    continue;
                }

                if (!StableId.IsValid(entry.EntryId))
                {
                    diagnostics.Add(Blocking(
                        FaceMotionDiagnosticCodes.InvalidEntryId,
                        "A mapping entry ID is not a valid stable ID.",
                        entry.EntryId,
                        "Repair or regenerate the entry ID."));
                }
                else if (!seenEntryIds.Add(entry.EntryId))
                {
                    diagnostics.Add(Blocking(
                        FaceMotionDiagnosticCodes.DuplicateEntryId,
                        "Mapping entry ID appears more than once.",
                        entry.EntryId,
                        "Assign a unique entry ID."));
                }

                if (string.IsNullOrWhiteSpace(entry.LogicalTargetId))
                {
                    diagnostics.Add(Blocking(
                        FaceMotionDiagnosticCodes.MissingLogicalTargetId,
                        "A mapping entry has no logical target ID.",
                        entry.EntryId,
                        "Assign a logical target ID such as \"mouth.smile\"."));
                }
                else if (!seenLogicalTargets.Add(entry.LogicalTargetId))
                {
                    diagnostics.Add(Blocking(
                        FaceMotionDiagnosticCodes.DuplicateLogicalTarget,
                        "The same logical target ID is bound more than once.",
                        entry.LogicalTargetId,
                        "Assign a unique logical target ID per binding."));
                }

                ValidateEntry(entry, diagnostics);
            }
        }

        private static void ValidateEntry(BindingMappingEntry entry, List<FaceMotionDiagnostic> diagnostics)
        {
            if (!Enum.IsDefined(typeof(LogicalTargetKind), entry.TargetKind))
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.InvalidLogicalTargetKind,
                    $"Logical target kind {(int)entry.TargetKind} is not supported.",
                    entry.EntryId,
                    "Use a supported LogicalTargetKind."));
                return;
            }

            bool hasValidBinding = entry.TargetKind == LogicalTargetKind.BlendShape
                ? entry.BlendShape.HasValue
                : entry.Transform.HasValue;

            if (!hasValidBinding)
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.MissingBinding,
                    "The mapping entry has no populated binding for its target kind.",
                    entry.EntryId,
                    "Bind the logical target to a concrete avatar element."));
            }
        }

        private static FaceMotionDiagnostic Blocking(string code, string message, string contextId, string fix)
        {
            return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Error, message, contextId, true, fix);
        }
    }
}