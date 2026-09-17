namespace FaceMotion.Editor.UI.Guidance
{
    /// <summary>
    /// Coarse first-run workflow stage derived from the editor session. The numeric values are
    /// the user-facing step numbers; they intentionally match the five-step onboarding flow.
    /// </summary>
    public enum FaceMotionUxState
    {
        SelectAvatar = 1,
        CreateAnimation = 2,
        AddTrack = 3,
        AddKey = 4,
        PreviewAndIntegrate = 5
    }
}
