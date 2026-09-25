namespace FaceMotion.Editor.UI.Guidance
{
    /// <summary>
    /// Coarse first-run workflow stage derived from the editor session. The numeric values are
    /// the user-facing step numbers; they intentionally match the six-step onboarding flow.
    /// </summary>
    public enum FaceMotionUxState
    {
        CreateProject = 1,
        SelectAvatar = 2,
        CreateAnimation = 3,
        StartPreview = 4,
        AddTrack = 5,
        PreviewAndIntegrate = 6
    }
}
