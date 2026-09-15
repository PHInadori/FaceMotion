using UnityEditor;

namespace FaceMotion.Editor.UI.Session
{
    /// <summary>
    /// Mirrors a few scalar editor state values to EditorPrefs so the window can restore
    /// the selected project, animation, current time, and zoom after a domain reload.
    /// Project data is never touched; UI state is never written into a FaceMotion asset.
    /// </summary>
    public static class FaceMotionSessionStateStore
    {
        private const string ProjectPathKey = "FaceMotion.Window.v1.ProjectPath";
        private const string AnimationIdKey = "FaceMotion.Window.v1.AnimationId";
        private const string CurrentTimeKey = "FaceMotion.Window.v1.CurrentTime";
        private const string ZoomKey = "FaceMotion.Window.v1.Zoom";

        public static void Save(string projectPath, string animationId, float currentTime, float zoom)
        {
            EditorPrefs.SetString(ProjectPathKey, projectPath ?? string.Empty);
            EditorPrefs.SetString(AnimationIdKey, animationId ?? string.Empty);
            EditorPrefs.SetFloat(CurrentTimeKey, currentTime);
            EditorPrefs.SetFloat(ZoomKey, zoom);
        }

        public static void Load(out string projectPath, out string animationId, out float currentTime, out float zoom)
        {
            projectPath = EditorPrefs.GetString(ProjectPathKey, string.Empty);
            animationId = EditorPrefs.GetString(AnimationIdKey, string.Empty);
            currentTime = EditorPrefs.GetFloat(CurrentTimeKey, 0f);
            zoom = EditorPrefs.GetFloat(ZoomKey, 1f);
        }
    }
}