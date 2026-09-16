using System.Collections.Generic;

namespace FaceMotion.Diagnostics
{
    public static partial class FaceMotionDiagnosticDefinitionRegistry
    {
        static partial void RegisterAvatarCodes(Dictionary<string, DiagnosticDefinition> defs)
        {
            Add(defs, FaceMotionDiagnosticCodes.AvatarRootMissing, FaceMotionDiagnosticActionLevel.Recommended, "avatar");
            Add(defs, FaceMotionDiagnosticCodes.DescriptorMissing, FaceMotionDiagnosticActionLevel.Recommended, "avatar", FaceMotionDiagnosticSelectionKind.AvatarRoot);
            Add(defs, FaceMotionDiagnosticCodes.InactiveAvatarRoot, FaceMotionDiagnosticActionLevel.Recommended, "avatar", FaceMotionDiagnosticSelectionKind.AvatarRoot);
            Add(defs, FaceMotionDiagnosticCodes.MissingAnimator, FaceMotionDiagnosticActionLevel.Recommended, "avatar", FaceMotionDiagnosticSelectionKind.AvatarRoot);
            Add(defs, FaceMotionDiagnosticCodes.AnimatorAvatarNull, FaceMotionDiagnosticActionLevel.Recommended, "avatar", FaceMotionDiagnosticSelectionKind.AvatarRoot);
            Add(defs, FaceMotionDiagnosticCodes.NonHumanoidAvatar, FaceMotionDiagnosticActionLevel.Recommended, "avatar", FaceMotionDiagnosticSelectionKind.AvatarRoot);
            Add(defs, FaceMotionDiagnosticCodes.DuplicateTransformPath, FaceMotionDiagnosticActionLevel.Recommended, "avatar", FaceMotionDiagnosticSelectionKind.ParentOfAmbiguousPath);
            Add(defs, FaceMotionDiagnosticCodes.RendererSharedMeshMissing, FaceMotionDiagnosticActionLevel.Recommended, "avatar", FaceMotionDiagnosticSelectionKind.RelativeRendererPath);
            Add(defs, FaceMotionDiagnosticCodes.DuplicateBlendShapeNameInMesh, FaceMotionDiagnosticActionLevel.Recommended, "avatar", FaceMotionDiagnosticSelectionKind.RendererParentPath);
            Add(defs, FaceMotionDiagnosticCodes.FingerprintFailure, FaceMotionDiagnosticActionLevel.Recommended, "avatar", FaceMotionDiagnosticSelectionKind.AvatarRoot);
        }
    }
}