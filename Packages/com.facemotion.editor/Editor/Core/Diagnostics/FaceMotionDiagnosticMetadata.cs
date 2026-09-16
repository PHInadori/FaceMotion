namespace FaceMotion.Diagnostics
{
    /// <summary>
    /// User-facing action level. Independently derived from the code's real behavior and
    /// never mechanically computed from <see cref="FaceMotionDiagnosticSeverity"/>. A
    /// Warning can still require action, and an Error may only inform.
    /// </summary>
    public enum FaceMotionDiagnosticActionLevel
    {
        /// <summary>The user must resolve this before FaceMotion can work reliably (blocking, identity ambiguity, or blocked export/integration).</summary>
        Required = 0,

        /// <summary>The flow can continue, but a part of the feature may degrade or a risk exists (quality/compatibility).</summary>
        Recommended = 1,

        /// <summary>Informational only. No user action is required.</summary>
        Info = 2
    }

    /// <summary>
    /// How the UI may locate an object in the Hierarchy related to a diagnostic.
    /// The resolver never picks one arbitrary match when identity is ambiguous.
    /// </summary>
    public enum FaceMotionDiagnosticSelectionKind
    {
        /// <summary>No object can be derived safely. The select button is hidden or disabled.</summary>
        None = 0,

        /// <summary>Select the avatar root that produced the diagnostic.</summary>
        AvatarRoot = 1,

        /// <summary>ContextId is a unique relative transform path under the avatar root.</summary>
        RelativeTransformPath = 2,

        /// <summary>ContextId is a unique relative renderer path under the avatar root.</summary>
        RelativeRendererPath = 3,

        /// <summary>
        /// ContextId is a composite value ("renderer path / blend shape name"). The renderer
        /// path (parent segment) is selected because the exact shape cannot be unique.
        /// </summary>
        RendererParentPath = 4,

        /// <summary>
        /// ContextId is the ambiguous relative path itself (e.g. FM-AVT-0007). The resolver
        /// selects the unique parent, or the avatar root when even the parent is ambiguous.
        /// It never selects one of the duplicates.
        /// </summary>
        ParentOfAmbiguousPath = 5
    }
}