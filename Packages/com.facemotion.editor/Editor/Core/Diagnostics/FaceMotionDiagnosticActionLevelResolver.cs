namespace FaceMotion.Diagnostics
{
    /// <summary>
    /// Pure resolver for the final user-facing action level. Severity alone never decides
    /// the action level, and the definition alone never produces Required.
    /// </summary>
    public static class FaceMotionDiagnosticActionLevelResolver
    {
        /// <summary>
        /// Resolves the user-facing action level for a diagnostic instance.
        /// </summary>
        public static FaceMotionDiagnosticActionLevel Resolve(
            FaceMotionDiagnostic diagnostic,
            DiagnosticDefinition definition)
        {
            if (diagnostic == null)
            {
                return FaceMotionDiagnosticActionLevel.Info;
            }

            if (diagnostic.Blocking)
            {
                return FaceMotionDiagnosticActionLevel.Required;
            }

            if (definition != null)
            {
                return definition.NonBlockingActionLevel;
            }

            if (diagnostic.Severity == FaceMotionDiagnosticSeverity.Info)
            {
                return FaceMotionDiagnosticActionLevel.Info;
            }

            return FaceMotionDiagnosticActionLevel.Recommended;
        }
    }
}