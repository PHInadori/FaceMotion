using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Serialization;
using UnityEditor;

namespace FaceMotion.Editor.Serialization
{
    /// <summary>
    /// Runs the project migration pipeline at an explicit boundary and commits a successful
    /// migration to the persisted ScriptableObject asset transactionally. Future schemas
    /// and unsafe states are never written; a migrated asset is rewritten with the
    /// deterministic migrated clone only after the pipeline's own validation passed.
    /// </summary>
    public static class ProjectMigrationService
    {
        public static ProjectMigrationResult TryMigrateOnLoad(FaceMotionProject asset)
        {
            if (asset == null)
            {
                return new ProjectMigrationResult(ProjectMigrationStatus.MigrationFailed, null,
                    new[] { new FaceMotionDiagnostic(FaceMotionDiagnosticCodes.NullProject, FaceMotionDiagnosticSeverity.Error, "Cannot migrate a null project.", string.Empty, true, "Load a project first.") });
            }

            var pipeline = new ProjectMigrationPipeline(new IProjectMigrator<FaceMotionProject>[]
            {
                new ProjectLegacyMigrator(),
                new ProjectV1ToV2Migrator()
            });
            var result = pipeline.Migrate(asset);
            if (result.Status != ProjectMigrationStatus.Migrated || result.Project == null)
            {
                return result;
            }

            Undo.RegisterCompleteObjectUndo(asset, "Migrate FaceMotion Project Schema");
            // The migrated clone is detached Unity object data: its m_Name is empty, and the
            // JSON overwrite would clear the asset's filename-derived Unity object name.
            // The name is outside the FaceMotion schema, so preserve it exactly as-is.
            string originalAssetName = asset.name;
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(result.Project), asset);
            asset.name = originalAssetName;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return result;
        }
    }
}
