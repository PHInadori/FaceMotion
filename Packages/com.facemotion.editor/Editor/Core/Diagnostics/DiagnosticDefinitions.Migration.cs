using System.Collections.Generic;

namespace FaceMotion.Diagnostics
{
    public static partial class FaceMotionDiagnosticDefinitionRegistry
    {
        static partial void RegisterMigrationCodes(Dictionary<string, DiagnosticDefinition> defs)
        {
            Add(defs, FaceMotionDiagnosticCodes.FutureSchema, FaceMotionDiagnosticActionLevel.Recommended, "migration");
            Add(defs, FaceMotionDiagnosticCodes.UninitializedSchema, FaceMotionDiagnosticActionLevel.Recommended, "migration");
            Add(defs, FaceMotionDiagnosticCodes.MissingMigrationStep, FaceMotionDiagnosticActionLevel.Recommended, "migration");
            Add(defs, FaceMotionDiagnosticCodes.MigrationFailed, FaceMotionDiagnosticActionLevel.Recommended, "migration");
            Add(defs, FaceMotionDiagnosticCodes.MigrationValidationFailed, FaceMotionDiagnosticActionLevel.Recommended, "migration");
            Add(defs, FaceMotionDiagnosticCodes.UpgradedSchema, FaceMotionDiagnosticActionLevel.Info, "migration");
            Add(defs, FaceMotionDiagnosticCodes.FutureSchemaBlocked, FaceMotionDiagnosticActionLevel.Recommended, "migration");
            Add(defs, FaceMotionDiagnosticCodes.MalformedData, FaceMotionDiagnosticActionLevel.Recommended, "migration");
            Add(defs, FaceMotionDiagnosticCodes.IdRepaired, FaceMotionDiagnosticActionLevel.Info, "migration");
            Add(defs, FaceMotionDiagnosticCodes.DuplicateId, FaceMotionDiagnosticActionLevel.Recommended, "migration");
            Add(defs, FaceMotionDiagnosticCodes.PartialRecovery, FaceMotionDiagnosticActionLevel.Recommended, "migration");
            Add(defs, FaceMotionDiagnosticCodes.IntegrationAmbiguous, FaceMotionDiagnosticActionLevel.Recommended, "migration");
        }
    }
}