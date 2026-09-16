using System.Collections.Generic;

namespace FaceMotion.Diagnostics
{
    public static partial class FaceMotionDiagnosticDefinitionRegistry
    {
        static partial void RegisterMappingCodes(Dictionary<string, DiagnosticDefinition> defs)
        {
            Add(defs, FaceMotionDiagnosticCodes.InvalidProfileId, FaceMotionDiagnosticActionLevel.Recommended, "mapping");
            Add(defs, FaceMotionDiagnosticCodes.NullMappingProfile, FaceMotionDiagnosticActionLevel.Recommended, "mapping");
            Add(defs, FaceMotionDiagnosticCodes.FutureProfileSchema, FaceMotionDiagnosticActionLevel.Recommended, "mapping");
            Add(defs, FaceMotionDiagnosticCodes.UninitializedProfileSchema, FaceMotionDiagnosticActionLevel.Recommended, "mapping");
            Add(defs, FaceMotionDiagnosticCodes.InvalidEntryId, FaceMotionDiagnosticActionLevel.Recommended, "mapping");
            Add(defs, FaceMotionDiagnosticCodes.DuplicateEntryId, FaceMotionDiagnosticActionLevel.Recommended, "mapping");
            Add(defs, FaceMotionDiagnosticCodes.MissingLogicalTargetId, FaceMotionDiagnosticActionLevel.Recommended, "mapping");
            Add(defs, FaceMotionDiagnosticCodes.InvalidLogicalTargetKind, FaceMotionDiagnosticActionLevel.Recommended, "mapping");
            Add(defs, FaceMotionDiagnosticCodes.MissingBinding, FaceMotionDiagnosticActionLevel.Recommended, "mapping");
            Add(defs, FaceMotionDiagnosticCodes.InvalidFingerprint, FaceMotionDiagnosticActionLevel.Recommended, "mapping");
            Add(defs, FaceMotionDiagnosticCodes.DuplicateLogicalTarget, FaceMotionDiagnosticActionLevel.Recommended, "mapping");
            Add(defs, FaceMotionDiagnosticCodes.MissingRenderer, FaceMotionDiagnosticActionLevel.Recommended, "mapping");
            Add(defs, FaceMotionDiagnosticCodes.MissingBlendShape, FaceMotionDiagnosticActionLevel.Recommended, "mapping");
            Add(defs, FaceMotionDiagnosticCodes.MissingTransform, FaceMotionDiagnosticActionLevel.Recommended, "mapping");
            Add(defs, FaceMotionDiagnosticCodes.AmbiguousBinding, FaceMotionDiagnosticActionLevel.Recommended, "mapping");
            Add(defs, FaceMotionDiagnosticCodes.InvalidBinding, FaceMotionDiagnosticActionLevel.Recommended, "mapping");
            Add(defs, FaceMotionDiagnosticCodes.ProfileFingerprintMismatch, FaceMotionDiagnosticActionLevel.Recommended, "mapping");
            Add(defs, FaceMotionDiagnosticCodes.StaleMappingButValid, FaceMotionDiagnosticActionLevel.Recommended, "mapping");
        }
    }
}