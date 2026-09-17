using System;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.VRChat.Integration
{
    /// <summary>
    /// Persists per-animation export destinations (path + owning GUID) across sessions so
    /// the one-click flow can re-use the same clip, keep its GUID stable, and refuse to
    /// overwrite a foreign asset that occupies the registered path.
    /// </summary>
    public static class ExportedClipRegistry
    {
        private const string PathSuffix = "ExportedClip.Path";
        private const string GuidSuffix = "ExportedClip.Guid";

        public static bool TryGetPath(string animationId, out string path)
        {
            path = string.Empty;
            if (string.IsNullOrEmpty(animationId)) return false;
            string stored = EditorPrefs.GetString(GetKey(animationId, PathSuffix), string.Empty);
            if (string.IsNullOrEmpty(stored)) return false;
            path = stored;
            return true;
        }

        public static bool TryGetGuid(string animationId, out string assetGuid)
        {
            assetGuid = string.Empty;
            if (string.IsNullOrEmpty(animationId)) return false;
            string stored = EditorPrefs.GetString(GetKey(animationId, GuidSuffix), string.Empty);
            if (string.IsNullOrEmpty(stored)) return false;
            assetGuid = stored;
            return true;
        }

        /// <summary>
        /// True when an actual asset occupies <paramref name="path"/> and its GUID matches
        /// the GUID recorded for <paramref name="animationId"/> (i.e. FaceMotion owns it).
        /// </summary>
        public static bool IsOwned(string animationId, string path)
        {
            if (string.IsNullOrEmpty(animationId) || string.IsNullOrEmpty(path)) return false;
            if (!TryGetPath(animationId, out string registered)) return false;
            if (!string.Equals(registered, path, StringComparison.OrdinalIgnoreCase)) return false;
            string clipGuid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(clipGuid)) return false;
            if (!TryGetGuid(animationId, out string expectedGuid) || string.IsNullOrEmpty(expectedGuid)) return false;
            return string.Equals(clipGuid, expectedGuid, StringComparison.OrdinalIgnoreCase);
        }

        public static void Record(string animationId, string path)
        {
            if (string.IsNullOrEmpty(animationId) || string.IsNullOrEmpty(path)) return;
            EditorPrefs.SetString(GetKey(animationId, PathSuffix), path);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                EditorPrefs.DeleteKey(GetKey(animationId, GuidSuffix));
            }
            else
            {
                EditorPrefs.SetString(GetKey(animationId, GuidSuffix), guid);
            }
        }

        public static void Remove(string animationId)
        {
            if (string.IsNullOrEmpty(animationId)) return;
            EditorPrefs.DeleteKey(GetKey(animationId, PathSuffix));
            EditorPrefs.DeleteKey(GetKey(animationId, GuidSuffix));
        }

        private static string GetKey(string animationId, string suffix)
        {
            return "FaceMotion.Window.v2." + Hash128.Compute(Application.dataPath).ToString() + "." + suffix + "." + animationId;
        }
    }
}