namespace FaceMotion.Editor.UI.Guidance
{
    /// <summary>
    /// Coarse next-action stages derived from the current authoring state.
    /// </summary>
    public enum FaceMotionUxState
    {
        CreateProject = 1,
        SelectAvatar = 2,
        CreateAnimation = 3,
        AddTrack = 4,
        AddKey = 5,
        StartPreview = 6,
        SelectForVrchat = 7,
        PreviewAndIntegrate = 8
    }
}
