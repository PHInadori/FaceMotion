using System;

namespace FaceMotion.Diagnostics
{
    /// <summary>
    /// Central, per-code diagnostic metadata that is non-UI and identity-related. Codes stay
    /// the stable developer-facing identifier; every other field keeps the meaning of a
    /// diagnostic discoverable by a user who does not know the code. Localized display text
    /// lives in the UI localization catalog, never here.
    /// </summary>
    public sealed class DiagnosticDefinition
    {
        public DiagnosticDefinition(
            string code,
            FaceMotionDiagnosticActionLevel nonBlockingActionLevel,
            string category,
            bool canAutoFix = false,
            FaceMotionDiagnosticSelectionKind selectionKind = FaceMotionDiagnosticSelectionKind.None,
            string helpTopicId = null)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("A diagnostic code is required.", nameof(code));
            }

            if (nonBlockingActionLevel == FaceMotionDiagnosticActionLevel.Required)
            {
                throw new ArgumentException(
                    "NonBlockingActionLevel must be Recommended or Info. " +
                    "Required is derived from the diagnostic instance's Blocking flag.",
                    nameof(nonBlockingActionLevel));
            }

            Code = code;
            NonBlockingActionLevel = nonBlockingActionLevel;
            Category = string.IsNullOrEmpty(category) ? "general" : category;
            CanAutoFix = canAutoFix;
            SelectionKind = selectionKind;
            HelpTopicId = string.IsNullOrEmpty(helpTopicId) ? code : helpTopicId;
        }

        public string Code { get; }

        /// <summary>
        /// Action level used only when the diagnostic instance is non-blocking. It can be
        /// Recommended or Info; it can never be Required. Required is always derived from
        /// <see cref="FaceMotionDiagnostic.Blocking"/> by the action level resolver.
        /// </summary>
        public FaceMotionDiagnosticActionLevel NonBlockingActionLevel { get; }

        public string Category { get; }

        public bool CanAutoFix { get; }

        /// <summary>How the UI may resolve an object in the Hierarchy for this code.</summary>
        public FaceMotionDiagnosticSelectionKind SelectionKind { get; }

        /// <summary>Stable topic used to link to documentation; defaults to the code.</summary>
        public string HelpTopicId { get; }
    }
}