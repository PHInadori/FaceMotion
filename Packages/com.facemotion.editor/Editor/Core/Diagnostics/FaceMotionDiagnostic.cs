using System;
using System.Collections.Generic;

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
            string suggestedFix,
            IReadOnlyDictionary<string, string> details = null)
        {
            Code = string.IsNullOrWhiteSpace(code)
                ? throw new ArgumentException("A diagnostic code is required.", nameof(code))
                : code;
            Severity = severity;
            Message = message ?? string.Empty;
            ContextId = contextId ?? string.Empty;
            Blocking = blocking;
            SuggestedFix = suggestedFix ?? string.Empty;
            Details = details == null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(details);
        }

        public string Code { get; }

        public FaceMotionDiagnosticSeverity Severity { get; }

        public string Message { get; }

        public string ContextId { get; }

        public bool Blocking { get; }

        public string SuggestedFix { get; }

        /// <summary>Stable, display-only details for this diagnostic. They never retain Unity object references.</summary>
        public IReadOnlyDictionary<string, string> Details { get; }
    }

    public static class FaceMotionDiagnosticDetailKeys
    {
        public const string Reason = "reason";
        public const string ConflictObjectName = "conflict-object-name";
        public const string ConflictObjectPath = "conflict-object-path";
        public const string ConflictComponent = "conflict-component";
        public const string ConflictController = "conflict-controller";
        public const string ConflictClip = "conflict-clip";
        public const string BindingPath = "binding-path";
        public const string BindingProperty = "binding-property";
        public const string BindingType = "binding-type";
        public const string Binding = "binding";
        public const string ReasonAmbiguousRelativePath = "ambiguous-relative-path";
        public const string ReasonMergeAnimatorBinding = "merge-animator-binding";
    }
}
