using System.Collections.Generic;
using FaceMotion.Avatar;
using FaceMotion.Diagnostics;
using FaceMotion.Serialization;
using UnityEditor;

namespace FaceMotion.Editor.Avatar
{
    /// <summary>
    /// Migrates a persisted AvatarMappingProfile asset at an explicit boundary. The profile
    /// is rewritten with the deterministic migrated clone only when migration made changes;
    /// future schemas are never modified in place.
    /// </summary>
    public static class MappingProfileMigrationService
    {
        public static MigrationResult TryMigrateOnLoad(AvatarMappingProfile profile)
        {
            var diagnostics = new List<FaceMotionDiagnostic>();
            var result = AvatarMappingProfileMigration.TryMigrate(profile, out var migrated, diagnostics);
            if (!result.Allowed || !result.Applied || migrated == null)
            {
                return new MigrationResult(result.Applied, result.Blocked, diagnostics);
            }

            Undo.RegisterCompleteObjectUndo(profile, "Migrate FaceMotion Mapping Profile Schema");
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(migrated), profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return new MigrationResult(true, false, diagnostics);
        }
    }
}