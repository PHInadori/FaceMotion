using System.Collections.Generic;
using UnityEngine;

namespace FaceMotion.Editor.Avatar
{
    /// <summary>
    /// Single source of truth for relative paths between the avatar root and a descendant.
    /// The root itself is "" and the separator is "/". This utility is shared by scanning,
    /// binding, and future export and preview so all of them agree on identity.
    /// </summary>
    public static class RelativePathUtility
    {
        public const string Separator = "/";

        /// <summary>
        /// Returns "" when <paramref name="descendant"/> is the root, the "/"-joined relative
        /// path otherwise, and null when <paramref name="descendant"/> is not under
        /// <paramref name="root"/>.
        /// </summary>
        public static string GetRelativePath(Transform root, Transform descendant)
        {
            if (root == null || descendant == null)
            {
                return null;
            }

            if (ReferenceEquals(root, descendant))
            {
                return string.Empty;
            }

            var names = new List<string>(16);
            Transform current = descendant;
            while (current != null && !ReferenceEquals(current, root))
            {
                names.Add(current.name);
                current = current.parent;
            }

            if (current == null)
            {
                return null;
            }

            names.Reverse();
            return string.Join(Separator, names);
        }

        /// <summary>Depth below the root: 0 for the root, path segment count otherwise.</summary>
        public static int GetDepth(Transform root, Transform descendant)
        {
            if (root == null || descendant == null || ReferenceEquals(root, descendant))
            {
                return 0;
            }

            int depth = 0;
            Transform current = descendant;
            while (current != null && !ReferenceEquals(current, root))
            {
                depth++;
                current = current.parent;
            }

            return current == null ? -1 : depth;
        }

        /// <summary>Parent path of a relative path, or "" for a top-level path.</summary>
        public static string GetParentPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return string.Empty;
            }

            int lastSeparator = relativePath.LastIndexOf(Separator, System.StringComparison.Ordinal);
            return lastSeparator < 0 ? string.Empty : relativePath.Substring(0, lastSeparator);
        }
    }
}