using System;

namespace FaceMotion.Diagnostics
{
    public enum FaceMotionDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public sealed class FaceMotionDiagnostic
    {
        public FaceMotionDiagnostic(
            string code,
            FaceMotionDiagnosticSeverity severity,
            string message,
            string contextId,
            bool blocking,
            string suggestedFix)
        {
            Code = string.IsNullOrWhiteSpace(code)
                ? throw new ArgumentException("A diagnostic code is required.", nameof(code))
                : code;
            Severity = severity;
            Message = message ?? string.Empty;
            ContextId = contextId ?? string.Empty;
            Blocking = blocking;
            SuggestedFix = suggestedFix ?? string.Empty;
        }

        public string Code { get; }

        public FaceMotionDiagnosticSeverity Severity { get; }

        public string Message { get; }

        public string ContextId { get; }

        public bool Blocking { get; }

        public string SuggestedFix { get; }
    }
}
