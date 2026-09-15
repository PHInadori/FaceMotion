using FaceMotion.Diagnostics;

namespace FaceMotion.Editor
{
    internal static class CommandDiagnostics
    {
        public static FaceMotionDiagnostic InvalidProject(string contextId)
        {
            return new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.CommandInvalidProject,
                FaceMotionDiagnosticSeverity.Error,
                "The project is not in a usable state.",
                contextId,
                true,
                "Load a validated project before running commands.");
        }

        public static FaceMotionDiagnostic TargetNotFound(string target, string contextId)
        {
            return new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.CommandTargetNotFound,
                FaceMotionDiagnosticSeverity.Error,
                $"The {target} was not found.",
                contextId,
                true,
                "Select an existing target before running the command.");
        }

        public static FaceMotionDiagnostic InvalidArgument(string detail, string contextId)
        {
            return new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.CommandInvalidArgument,
                FaceMotionDiagnosticSeverity.Error,
                detail,
                contextId,
                true,
                "Fix the command arguments.");
        }

        public static FaceMotionDiagnostic KindMismatch(string detail, string contextId)
        {
            return new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.CommandKindMismatch,
                FaceMotionDiagnosticSeverity.Error,
                detail,
                contextId,
                true,
                "Use the command that matches the track kind.");
        }

        public static FaceMotionDiagnostic DuplicateTrackBinding(string bindingDescription, string contextId)
        {
            return new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.DuplicateTrackBinding,
                FaceMotionDiagnosticSeverity.Warning,
                $"A track already binds \"{bindingDescription}\".",
                contextId,
                false,
                "Select the existing track instead of adding a duplicate.");
        }
    }
}