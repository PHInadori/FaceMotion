using FaceMotion.Data;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    /// <summary>Small editor-backend helpers shared only by the round-trip tests.</summary>
    internal static class EditorExtensions
    {
        public static void SetDirtyAndSave(ScriptableObject asset, string assetPath)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Copies the saved asset and reloads the copy so the returned instance is deserialized
        /// fresh from disk. Destroying an asset is not permitted in test batches, and loading the
        /// original path returns the same in-memory instance, so a copy is the only reliable
        /// round trip through the Unity serializer.
        /// </summary>
        public static FaceMotionProject ReloadCopy(string sourceAssetPath, string copyAssetPath)
        {
            if (!AssetDatabase.CopyAsset(sourceAssetPath, copyAssetPath))
            {
                throw new System.InvalidOperationException("Could not copy asset for round-trip reload: " + sourceAssetPath);
            }

            AssetDatabase.SaveAssets();
            var loaded = AssetDatabase.LoadAssetAtPath<FaceMotionProject>(copyAssetPath);
            if (loaded == null)
            {
                throw new System.InvalidOperationException("Copied asset failed to load: " + copyAssetPath);
            }

            return loaded;
        }
    }
}