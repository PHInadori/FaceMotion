using System.Collections.Generic;

namespace FaceMotion.Diagnostics
{
    public static partial class FaceMotionDiagnosticDefinitionRegistry
    {
        static partial void RegisterModularAvatarCodes(Dictionary<string, DiagnosticDefinition> defs)
        {
            Add(defs, FaceMotionDiagnosticCodes.ModularAvatarMetadataUnavailable, FaceMotionDiagnosticActionLevel.Recommended, "integration-modular-avatar");
            Add(defs, FaceMotionDiagnosticCodes.ModularAvatarNotInstalled, FaceMotionDiagnosticActionLevel.Recommended, "integration-modular-avatar");
            Add(defs, FaceMotionDiagnosticCodes.ModularAvatarVersionUnavailable, FaceMotionDiagnosticActionLevel.Recommended, "integration-modular-avatar");
            Add(defs, FaceMotionDiagnosticCodes.ModularAvatarNotImplemented, FaceMotionDiagnosticActionLevel.Recommended, "integration-modular-avatar");
            Add(defs, FaceMotionDiagnosticCodes.ModularAvatarManifest, FaceMotionDiagnosticActionLevel.Recommended, "integration-modular-avatar");
            Add(defs, FaceMotionDiagnosticCodes.ModularAvatarAvatar, FaceMotionDiagnosticActionLevel.Recommended, "integration-modular-avatar");
            Add(defs, FaceMotionDiagnosticCodes.ModularAvatarPrefabAsset, FaceMotionDiagnosticActionLevel.Recommended, "integration-modular-avatar");
            Add(defs, FaceMotionDiagnosticCodes.ModularAvatarClip, FaceMotionDiagnosticActionLevel.Recommended, "integration-modular-avatar");
            Add(defs, FaceMotionDiagnosticCodes.ModularAvatarPath, FaceMotionDiagnosticActionLevel.Recommended, "integration-modular-avatar");
            Add(defs, FaceMotionDiagnosticCodes.ModularAvatarParameterName, FaceMotionDiagnosticActionLevel.Recommended, "integration-modular-avatar");
            Add(defs, FaceMotionDiagnosticCodes.ModularAvatarManifestConflict, FaceMotionDiagnosticActionLevel.Recommended, "integration-modular-avatar");
            Add(defs, FaceMotionDiagnosticCodes.ModularAvatarPlan, FaceMotionDiagnosticActionLevel.Recommended, "integration-modular-avatar");
            Add(defs, FaceMotionDiagnosticCodes.ModularAvatarDetached, FaceMotionDiagnosticActionLevel.Info, "integration-modular-avatar");
            Add(defs, FaceMotionDiagnosticCodes.ModularAvatarApplied, FaceMotionDiagnosticActionLevel.Info, "integration-modular-avatar");
            Add(defs, FaceMotionDiagnosticCodes.ModularAvatarApply, FaceMotionDiagnosticActionLevel.Recommended, "integration-modular-avatar");
            Add(defs, FaceMotionDiagnosticCodes.ModularAvatarOwnership, FaceMotionDiagnosticActionLevel.Recommended, "integration-modular-avatar");
            Add(defs, FaceMotionDiagnosticCodes.ModularAvatarRemoved, FaceMotionDiagnosticActionLevel.Info, "integration-modular-avatar");
            Add(defs, FaceMotionDiagnosticCodes.ModularAvatarParameterConflict, FaceMotionDiagnosticActionLevel.Recommended, "integration-modular-avatar");
            Add(defs, FaceMotionDiagnosticCodes.ModularAvatarBindingConflict, FaceMotionDiagnosticActionLevel.Recommended, "integration-modular-avatar");
            Add(defs, FaceMotionDiagnosticCodes.ModularAvatarCrossBindingConflict, FaceMotionDiagnosticActionLevel.Recommended, "integration-modular-avatar");
        }
    }
}