using FaceMotion.Diagnostics;

namespace FaceMotion.Editor.UI.Controllers
{
    /// <summary>
    /// Diagnostic factory for controller-level outcomes that are not data validation. Codes
    /// reuse the FM-CMD / FM-UI contracts defined in Core.
    /// </summary>
    public static class ControllerDiagnostics
    {
        public static FaceMotionDiagnostic NoProject()
        {
            return new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.CommandInvalidProject,
                FaceMotionDiagnosticSeverity.Warning,
                "No FaceMotion project is open.",
                string.Empty,
                false,
                "Create or select a project first.");
        }

        public static FaceMotionDiagnostic NoSelection(string target)
        {
            return new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.UISelectionInvalid,
                FaceMotionDiagnosticSeverity.Warning,
                $"No {target} is selected.",
                string.Empty,
                false,
                "Select a " + target + " first.");
        }

        public static FaceMotionDiagnostic InvalidValue(string detail)
        {
            return new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.CommandInvalidArgument,
                FaceMotionDiagnosticSeverity.Error,
                detail,
                string.Empty,
                true,
                "Enter a valid value.");
        }

        public static FaceMotionDiagnostic DuplicateTrack(string bindingDescription, string contextId)
        {
            return new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.DuplicateTrackBinding,
                FaceMotionDiagnosticSeverity.Warning,
                $"A track already binds \"{bindingDescription}\".",
                contextId,
                false,
                "Select the existing track instead of adding a duplicate.");
        }

        public static FaceMotionDiagnostic AssetCreateFailed(string path)
        {
            return new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.UIAssetCreateFailed,
                FaceMotionDiagnosticSeverity.Error,
                $"Could not create the project asset at \"{path}\".",
                path,
                true,
                "Choose a writable path inside Assets.");
        }

        public static FaceMotionDiagnostic PasteTrackMissing(string trackId)
        {
            return new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.UIPasteTrackMissing,
                FaceMotionDiagnosticSeverity.Error,
                $"The paste target track \"{trackId}\" no longer exists.",
                trackId,
                true,
                "Re-copy the keys from the source track.");
        }

        public static FaceMotionDiagnostic PasteCollision(int skipped)
        {
            return new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.UIPasteCollision,
                FaceMotionDiagnosticSeverity.Warning,
                $"{skipped} pasted key(s) were skipped because the target time is occupied.",
                string.Empty,
                false,
                "Move the keys around the occupied times, then paste again.");
        }

        public static FaceMotionDiagnostic Info(string message)
        {
            return new FaceMotionDiagnostic(
                "FM-UI-INFO",
                FaceMotionDiagnosticSeverity.Info,
                message,
                string.Empty,
                false,
                string.Empty);
        }
    }
}