using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Export
{
    /// <summary>
    /// Shared ownership and safe-deletion utilities for generated integration assets.
    /// Only assets listed in the manifest's OwnedAssetPaths may be deleted.
    /// </summary>
    public static class GeneratedAssetOwnership
    {
        private static readonly HashSet<string> CanonicalFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "FX.controller",
            "Parameters.asset",
            "Menu.asset",
            "FaceMotion.menu.asset",
            "Reset.anim",
            "Manifest.asset"
        };

        /// <summary>
        /// Returns true if <paramref name="path"/> is a valid Assets-relative path
        /// with no traversal segments that sits strictly inside <paramref name="root"/>.
        /// </summary>
        public static bool IsSafeOwnedAssetPath(string path, string root)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root)) return false;
            path = path.Replace('\\', '/');
            root = root.Replace('\\', '/');
            if (path == "Assets") return false;
            if (!path.StartsWith("Assets/", StringComparison.Ordinal)) return false;
            if (!path.StartsWith(root + "/", StringComparison.Ordinal)) return false;
            foreach (var part in path.Split('/'))
            {
                if (part.Length == 0 || part == "." || part == "..") return false;
            }
            return true;
        }

        /// <summary>Returns true if the filename belongs to the known set of FaceMotion-generated assets.</summary>
        public static bool IsCanonicalGeneratedFileName(string fileName)
        {
            return !string.IsNullOrEmpty(fileName) && CanonicalFileNames.Contains(fileName);
        }

        /// <summary>
        /// Scans <paramref name="folderPath"/> for foreign content that must prevent
        /// folder deletion: any subfolder, or any non-meta file whose full asset path
        /// is not in <paramref name="ownedAssetPaths"/>.
        /// Returns true when foreign content is found.
        /// </summary>
        public static bool FolderHasForeignContent(string folderPath, IReadOnlyCollection<string> ownedAssetPaths, out string foreignPath)
        {
            foreignPath = null;
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath)) return true;

            var full = AssetPathToFullPath(folderPath);
            if (string.IsNullOrEmpty(full) || !Directory.Exists(full)) return true;

            var owned = new HashSet<string>(ownedAssetPaths ?? Array.Empty<string>(), StringComparer.Ordinal);

            foreach (var dir in Directory.GetDirectories(full))
            {
                foreignPath = FullPathToAssetPath(dir);
                return true;
            }

            foreach (var file in Directory.GetFiles(full))
            {
                var name = Path.GetFileName(file);
                if (name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;

                var rel = FullPathToAssetPath(file);
                if (rel != null && owned.Contains(rel)) continue;

                foreignPath = rel ?? Path.GetFileName(file);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Deletes <paramref name="folderPath"/> only when it is a valid, safe Assets
        /// folder that contains no foreign content and no subfolders.
        /// Returns true if the folder was deleted.
        /// </summary>
        public static bool TryDeleteEmptyOwnedFolder(string folderPath, IReadOnlyCollection<string> ownedAssetPaths)
        {
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath)) return false;
            if (FolderHasForeignContent(folderPath, ownedAssetPaths, out _)) return false;
            return AssetDatabase.DeleteAsset(folderPath);
        }

        internal static string AssetPathToFullPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            assetPath = assetPath.Replace('\\', '/');
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal) && assetPath != "Assets") return null;
            var relative = assetPath.Length > "Assets".Length ? assetPath.Substring("Assets/".Length) : string.Empty;
            return string.IsNullOrEmpty(relative) ? Application.dataPath : Path.Combine(Application.dataPath, relative);
        }

        internal static string FullPathToAssetPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return null;
            fullPath = fullPath.Replace('\\', '/');
            var dataPath = Application.dataPath.Replace('\\', '/');
            if (!fullPath.StartsWith(dataPath, StringComparison.Ordinal)) return null;
            var rel = fullPath.Substring(dataPath.Length);
            if (rel.StartsWith("/", StringComparison.Ordinal)) rel = rel.Substring(1);
            return string.IsNullOrEmpty(rel) ? "Assets" : "Assets/" + rel;
        }
    }
}
