using System.IO;
using FaceMotion.Avatar;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Avatar
{
    /// <summary>
    /// Editor persistence for AvatarMappingProfile assets. Asset round trips must go through
    /// a copied reload because loading the original path returns the same in-memory instance,
    /// and destroying assets is not permitted in test batches.
    /// </summary>
    public static class MappingProfilePersistence
    {
        public static AvatarMappingProfile CreateAsset(AvatarMappingProfile profile, string assetPath)
        {
            if (profile == null)
            {
                throw new System.ArgumentNullException(nameof(profile));
            }

            if (string.IsNullOrEmpty(assetPath))
            {
                throw new System.ArgumentException("An asset path is required.", nameof(assetPath));
            }

            if (!assetPath.StartsWith("Assets/", System.StringComparison.Ordinal)
                && !assetPath.StartsWith("Packages/", System.StringComparison.Ordinal))
            {
                throw new System.ArgumentException("The asset path must be project-relative.", nameof(assetPath));
            }

            string directory = System.IO.Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                Directory.CreateDirectory(ToAbsolutePath(directory));
                AssetDatabase.Refresh();
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.CreateAsset(profile, assetPath);
            AssetDatabase.SaveAssets();
            return profile;
        }

        public static AvatarMappingProfile ReloadCopy(AvatarMappingProfile profile, string copyAssetPath)
        {
            if (profile == null)
            {
                throw new System.ArgumentNullException(nameof(profile));
            }

            if (!AssetDatabase.IsValidFolder("Assets/__FaceMotionTests"))
            {
                AssetDatabase.CreateFolder("Assets", "__FaceMotionTests");
            }

            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(profile), copyAssetPath))
            {
                throw new System.InvalidOperationException("Could not copy profile for round-trip reload.");
            }

            AssetDatabase.SaveAssets();
            var loaded = AssetDatabase.LoadAssetAtPath<AvatarMappingProfile>(copyAssetPath);
            if (loaded == null)
            {
                throw new System.InvalidOperationException("Copied profile failed to load: " + copyAssetPath);
            }

            return loaded;
        }

        private static string ToAbsolutePath(string projectRelative)
        {
            return System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Application.dataPath),
                projectRelative);
        }
    }
}