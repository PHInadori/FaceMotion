using System.Collections.Generic;

namespace FaceMotion.Diagnostics
{
    public static partial class FaceMotionDiagnosticDefinitionRegistry
    {
        static partial void RegisterIntegrationDirectCodes(Dictionary<string, DiagnosticDefinition> defs)
        {
            Add(defs, FaceMotionDiagnosticCodes.GenerationAvatar, FaceMotionDiagnosticActionLevel.Recommended, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationPrefabAsset, FaceMotionDiagnosticActionLevel.Recommended, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationClip, FaceMotionDiagnosticActionLevel.Recommended, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationPath, FaceMotionDiagnosticActionLevel.Recommended, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationParameterName, FaceMotionDiagnosticActionLevel.Recommended, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationOutputConflict, FaceMotionDiagnosticActionLevel.Recommended, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationPlan, FaceMotionDiagnosticActionLevel.Recommended, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationApplied, FaceMotionDiagnosticActionLevel.Info, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationApply, FaceMotionDiagnosticActionLevel.Recommended, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationRollback, FaceMotionDiagnosticActionLevel.Recommended, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationOwnership, FaceMotionDiagnosticActionLevel.Recommended, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationRolledBack, FaceMotionDiagnosticActionLevel.Info, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationParameterConflict, FaceMotionDiagnosticActionLevel.Recommended, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationBudget, FaceMotionDiagnosticActionLevel.Recommended, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationMenuCapacity, FaceMotionDiagnosticActionLevel.Recommended, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationFx, FaceMotionDiagnosticActionLevel.Recommended, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationAnimatorParameterConflict, FaceMotionDiagnosticActionLevel.Recommended, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationLayerConflict, FaceMotionDiagnosticActionLevel.Recommended, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationWriteDefaults, FaceMotionDiagnosticActionLevel.Recommended, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationBindingConflict, FaceMotionDiagnosticActionLevel.Recommended, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationManifest, FaceMotionDiagnosticActionLevel.Recommended, "integration-direct");
            Add(defs, FaceMotionDiagnosticCodes.GenerationPlanUnexpected, FaceMotionDiagnosticActionLevel.Recommended, "integration-direct");
        }
    }
}
