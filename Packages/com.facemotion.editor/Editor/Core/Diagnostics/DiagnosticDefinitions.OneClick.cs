using System.Collections.Generic;

namespace FaceMotion.Diagnostics
{
    public static partial class FaceMotionDiagnosticDefinitionRegistry
    {
        static partial void RegisterOneClickCodes(Dictionary<string, DiagnosticDefinition> defs)
        {
            Add(defs, FaceMotionDiagnosticCodes.OneClickNoCurrentAnimation, FaceMotionDiagnosticActionLevel.Recommended, "integration-one-click");
            Add(defs, FaceMotionDiagnosticCodes.OneClickNoAvatar, FaceMotionDiagnosticActionLevel.Recommended, "integration-one-click");
            Add(defs, FaceMotionDiagnosticCodes.OneClickForeignClip, FaceMotionDiagnosticActionLevel.Recommended, "integration-one-click");
            Add(defs, FaceMotionDiagnosticCodes.OneClickBackendUnavailable, FaceMotionDiagnosticActionLevel.Recommended, "integration-one-click");
            Add(defs, FaceMotionDiagnosticCodes.OneClickCrossBackend, FaceMotionDiagnosticActionLevel.Info, "integration-one-click");
            Add(defs, FaceMotionDiagnosticCodes.OneClickExported, FaceMotionDiagnosticActionLevel.Info, "integration-one-click");
            Add(defs, FaceMotionDiagnosticCodes.OneClickReapplied, FaceMotionDiagnosticActionLevel.Info, "integration-one-click");
            Add(defs, FaceMotionDiagnosticCodes.OneClickSucceeded, FaceMotionDiagnosticActionLevel.Info, "integration-one-click");
            Add(defs, FaceMotionDiagnosticCodes.OneClickApplyStopped, FaceMotionDiagnosticActionLevel.Recommended, "integration-one-click");
            Add(defs, FaceMotionDiagnosticCodes.OneClickNoPartialState, FaceMotionDiagnosticActionLevel.Info, "integration-one-click");
            Add(defs, FaceMotionDiagnosticCodes.BatchNoSelection, FaceMotionDiagnosticActionLevel.Recommended, "integration-batch");
            Add(defs, FaceMotionDiagnosticCodes.BatchDuplicateExportPath, FaceMotionDiagnosticActionLevel.Recommended, "integration-batch");
            Add(defs, FaceMotionDiagnosticCodes.BatchPreflightFailed, FaceMotionDiagnosticActionLevel.Recommended, "integration-batch");
            Add(defs, FaceMotionDiagnosticCodes.BatchRollback, FaceMotionDiagnosticActionLevel.Recommended, "integration-batch");
            Add(defs, FaceMotionDiagnosticCodes.BatchPartialApply, FaceMotionDiagnosticActionLevel.Recommended, "integration-batch");
        }
    }
}
