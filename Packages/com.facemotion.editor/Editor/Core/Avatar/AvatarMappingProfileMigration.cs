using System;
using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Serialization;
using FaceMotion.Versioning;

namespace FaceMotion.Avatar
{
    /// <summary>
    /// Pure, deterministic migration for an AvatarMappingProfile. Valid IDs, fingerprints,
    /// and binding paths are never changed. Missing/malformed profile and entry IDs are
    /// regenerated; the first occurrence of a duplicate keeps its ID. A malformed stored
    /// fingerprint is preserved and flagged as recoverable rather than silently dropped.
    /// </summary>
    public static class AvatarMappingProfileMigration
    {
        public static MigrationResult TryMigrate(
            AvatarMappingProfile source,
            out AvatarMappingProfile migrated,
            ICollection<FaceMotionDiagnostic> diagnostics = null)
        {
            var items = diagnostics ?? new List<FaceMotionDiagnostic>();

            if (source == null)
            {
                migrated = null;
                items.Add(Blocking(FaceMotionDiagnosticCodes.NullMappingProfile, "The mapping profile is null.", string.Empty, "Load a valid mapping profile."));
                return new MigrationResult(false, true, items);
            }

            if (source.SchemaVersion < FaceMotionVersions.LegacySchemaVersion)
            {
                migrated = null;
                items.Add(Blocking(FaceMotionDiagnosticCodes.UninitializedProfileSchema, "Mapping profile schema version is uninitialized.", source.ProfileId, "Initialize the profile through AvatarMappingProfile.CreateNew()."));
                return new MigrationResult(false, true, items);
            }

            if (source.SchemaVersion > FaceMotionVersions.MappingProfileSchemaVersion)
            {
                migrated = null;
                items.Add(Blocking(FaceMotionDiagnosticCodes.FutureProfileSchema, $"Mapping profile uses schema {source.SchemaVersion}, newer than this tool's {FaceMotionVersions.MappingProfileSchemaVersion}.", source.ProfileId, "Upgrade this tool to a version that reads the reported schema."));
                return new MigrationResult(false, true, items);
            }

            migrated = AvatarMappingProfile.CreateClone(source);
            if (source.SchemaVersion == FaceMotionVersions.MappingProfileSchemaVersion)
            {
                return new MigrationResult(false, false, items);
            }

            if (MigrationIds.NeedsRepair(migrated.ProfileId))
            {
                migrated.SetProfileIdForMigration(StableId.New());
                items.Add(Warning(FaceMotionDiagnosticCodes.IdRepaired, "The mapping profile had no valid stable ID; a new one was generated.", migrated.ProfileId, "Keep the generated ID."));
            }

            var seenEntryIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in migrated.Mappings)
            {
                if (entry == null)
                {
                    items.Add(Warning(FaceMotionDiagnosticCodes.PartialRecovery, "A null mapping entry was removed during migration.", migrated.ProfileId, "Recreate the entry if it was unexpected."));
                    continue;
                }

                entry.SetEntryIdForMigration(MigrationIds.RepairUnique(
                    entry.EntryId, seenEntryIds, out bool idRepaired, out bool idDuplicated));
                if (idRepaired)
                {
                    items.Add(Warning(FaceMotionDiagnosticCodes.IdRepaired, "A mapping entry had no valid stable ID; a new one was generated.", entry.EntryId, "Keep the generated ID."));
                }
                else if (idDuplicated)
                {
                    items.Add(Warning(FaceMotionDiagnosticCodes.DuplicateId, "A duplicate mapping entry ID was replaced with a new stable ID.", entry.EntryId, "Keep the generated ID."));
                }
            }

            migrated.NormalizeStructure();

            if (migrated.AvatarFingerprint != null && !migrated.AvatarFingerprint.IsValid)
            {
                items.Add(Warning(FaceMotionDiagnosticCodes.PartialRecovery, "The stored avatar fingerprint is malformed; it was preserved as-is.", migrated.ProfileId, "Rebind the profile to a scanned avatar to refresh the fingerprint."));
            }

            migrated.SetSchemaVersionForMigration(FaceMotionVersions.MappingProfileSchemaVersion);
            items.Add(Info(FaceMotionDiagnosticCodes.UpgradedSchema, "Mapping profile was upgraded to the current schema."));
            return new MigrationResult(true, false, items);
        }

        private static FaceMotionDiagnostic Warning(string code, string message, string contextId, string fix)
        {
            return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Warning, message, contextId, false, fix);
        }

        private static FaceMotionDiagnostic Info(string code, string message)
        {
            return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Info, message, string.Empty, false, string.Empty);
        }

        private static FaceMotionDiagnostic Blocking(string code, string message, string contextId, string fix)
        {
            return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Error, message, contextId, true, fix);
        }
    }
}
