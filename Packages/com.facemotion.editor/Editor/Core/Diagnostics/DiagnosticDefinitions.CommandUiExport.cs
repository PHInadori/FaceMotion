using System.Collections.Generic;

namespace FaceMotion.Diagnostics
{
    public static partial class FaceMotionDiagnosticDefinitionRegistry
    {
        static partial void RegisterCommandUiExportCodes(Dictionary<string, DiagnosticDefinition> defs)
        {
            Add(defs, FaceMotionDiagnosticCodes.CommandInvalidProject, FaceMotionDiagnosticActionLevel.Recommended, "command");
            Add(defs, FaceMotionDiagnosticCodes.CommandTargetNotFound, FaceMotionDiagnosticActionLevel.Recommended, "command");
            Add(defs, FaceMotionDiagnosticCodes.CommandInvalidArgument, FaceMotionDiagnosticActionLevel.Recommended, "command");
            Add(defs, FaceMotionDiagnosticCodes.CommandKindMismatch, FaceMotionDiagnosticActionLevel.Recommended, "command");
            Add(defs, FaceMotionDiagnosticCodes.UIAssetCreateFailed, FaceMotionDiagnosticActionLevel.Recommended, "ui");
            Add(defs, FaceMotionDiagnosticCodes.UIProjectLoadFailed, FaceMotionDiagnosticActionLevel.Recommended, "ui");
            Add(defs, FaceMotionDiagnosticCodes.UISelectionInvalid, FaceMotionDiagnosticActionLevel.Recommended, "ui");
            Add(defs, FaceMotionDiagnosticCodes.UIPasteTrackMissing, FaceMotionDiagnosticActionLevel.Recommended, "ui");
            Add(defs, FaceMotionDiagnosticCodes.UIPasteCollision, FaceMotionDiagnosticActionLevel.Recommended, "ui");
            Add(defs, FaceMotionDiagnosticCodes.UiInfo, FaceMotionDiagnosticActionLevel.Info, "ui");
            Add(defs, FaceMotionDiagnosticCodes.ExportCreateParentFailed, FaceMotionDiagnosticActionLevel.Recommended, "export");
            Add(defs, FaceMotionDiagnosticCodes.ExportPathOccupied, FaceMotionDiagnosticActionLevel.Recommended, "export");
            Add(defs, FaceMotionDiagnosticCodes.ExportSucceeded, FaceMotionDiagnosticActionLevel.Info, "export");
            Add(defs, FaceMotionDiagnosticCodes.ExportWriteFailed, FaceMotionDiagnosticActionLevel.Recommended, "export");
            Add(defs, FaceMotionDiagnosticCodes.ExportNoTimeline, FaceMotionDiagnosticActionLevel.Recommended, "export");
            Add(defs, FaceMotionDiagnosticCodes.ExportInvalidDuration, FaceMotionDiagnosticActionLevel.Recommended, "export");
            Add(defs, FaceMotionDiagnosticCodes.ExportInvalidFrameRate, FaceMotionDiagnosticActionLevel.Recommended, "export");
            Add(defs, FaceMotionDiagnosticCodes.ExportInvalidPath, FaceMotionDiagnosticActionLevel.Recommended, "export");
            Add(defs, FaceMotionDiagnosticCodes.ExportNullTrack, FaceMotionDiagnosticActionLevel.Recommended, "export");
            Add(defs, FaceMotionDiagnosticCodes.ExportInvalidBlendShape, FaceMotionDiagnosticActionLevel.Recommended, "export");
            Add(defs, FaceMotionDiagnosticCodes.ExportInvalidTransform, FaceMotionDiagnosticActionLevel.Recommended, "export");
            Add(defs, FaceMotionDiagnosticCodes.ExportUnsupportedRotation, FaceMotionDiagnosticActionLevel.Recommended, "export");
            Add(defs, FaceMotionDiagnosticCodes.ExportInvalidKey, FaceMotionDiagnosticActionLevel.Recommended, "export");
        }
    }
}