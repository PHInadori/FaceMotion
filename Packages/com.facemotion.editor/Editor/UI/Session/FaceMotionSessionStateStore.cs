using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.UI.Session
{
    /// <summary>
    /// Stores window state outside FaceMotionProject assets. SessionState handles immediate
    /// window recreation; project-scoped EditorPrefs survives a domain reload or restart.
    /// </summary>
    public static class FaceMotionSessionStateStore
    {
        private const string Prefix = "FaceMotion.Window.v2.";
        private const string ProjectPath = "ProjectPath";
        private const string AnimationId = "AnimationId";
        private const string CurrentTime = "CurrentTime";
        private const string Zoom = "Zoom";
        private const string AvatarGlobalObjectId = "AvatarGlobalObjectId";
        private const string AvatarScenePath = "AvatarScenePath";

        public static void Save(
            string projectPath,
            string animationId,
            float currentTime,
            float zoom,
            string avatarGlobalObjectId,
            string avatarScenePath)
        {
            string projectKey = ProjectScope();
            Write(projectKey + ProjectPath, projectPath);

            string stateKey = StateScope(projectPath);
            Write(stateKey + AnimationId, animationId);
            Write(stateKey + CurrentTime, currentTime.ToString("R", CultureInfo.InvariantCulture));
            Write(stateKey + Zoom, zoom.ToString("R", CultureInfo.InvariantCulture));
            Write(stateKey + AvatarGlobalObjectId, avatarGlobalObjectId);
            Write(stateKey + AvatarScenePath, avatarScenePath);
        }

        public static void Load(
            out string projectPath,
            out string animationId,
            out float currentTime,
            out float zoom,
            out string avatarGlobalObjectId,
            out string avatarScenePath)
        {
            projectPath = Read(ProjectScope() + ProjectPath);
            string stateKey = StateScope(projectPath);
            animationId = Read(stateKey + AnimationId);
            currentTime = ReadFloat(stateKey + CurrentTime, 0f);
            zoom = ReadFloat(stateKey + Zoom, 1f);
            avatarGlobalObjectId = Read(stateKey + AvatarGlobalObjectId);
            avatarScenePath = Read(stateKey + AvatarScenePath);
        }

        private static string ProjectScope()
        {
            return Prefix + Hash128.Compute(Application.dataPath).ToString() + ".";
        }

        private static string StateScope(string projectPath)
        {
            return ProjectScope() + Hash128.Compute(projectPath ?? string.Empty).ToString() + ".";
        }

        private static void Write(string key, string value)
        {
            string normalized = value ?? string.Empty;
            SessionState.SetString(key, normalized);
            EditorPrefs.SetString(key, normalized);
        }

        private static string Read(string key)
        {
            string sessionValue = SessionState.GetString(key, string.Empty);
            return string.IsNullOrEmpty(sessionValue) ? EditorPrefs.GetString(key, string.Empty) : sessionValue;
        }

        private static float ReadFloat(string key, float fallback)
        {
            return float.TryParse(Read(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                && !float.IsNaN(value)
                && !float.IsInfinity(value)
                ? value
                : fallback;
        }
    }
}
