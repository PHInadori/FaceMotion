using System;
using UnityEditor;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// Creates a unique root folder under Assets/__FaceMotionTests for temp assets and
    /// deletes it on dispose. Tests must never persist assets inside the package itself.
    /// </summary>
    internal sealed class TempFaceMotionAsset : IDisposable
    {
        private const string Root = "Assets/__FaceMotionTests";

        public string Folder { get; }

        public TempFaceMotionAsset()
        {
            if (!AssetDatabase.IsValidFolder(Root))
            {
                AssetDatabase.CreateFolder("Assets", "__FaceMotionTests");
            }

            Folder = Root + "/" + Guid.NewGuid().ToString("N");
            EnsureFolder(Folder);
            AssetDatabase.SaveAssets();
        }

        public string AssetPath(string fileName)
        {
            return Folder + "/" + fileName + ".asset";
        }

        public void Dispose()
        {
            if (Folder != null
                && Folder.StartsWith(Root, StringComparison.Ordinal)
                && AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
                AssetDatabase.SaveAssets();
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int separator = path.LastIndexOf('/');
            if (separator <= 0)
            {
                return;
            }

            string parent = path.Substring(0, separator);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}