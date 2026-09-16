using FaceMotion.Diagnostics;
using FaceMotion.Editor.Avatar;
using UnityEngine;

namespace FaceMotion.Editor.UI.Diagnostics
{
    /// <summary>
    /// Resolves the object in the Hierarchy a diagnostic should point the user to. Ambiguous
    /// identities are never picked arbitrarily: an ambiguous path resolves to its unique
    /// parent, then to the avatar root, and finally to no selection when no safe anchor
    /// exists. This class never mutates the scene or assets.
    /// </summary>
    public static class FaceMotionDiagnosticSelectionResolver
    {
        public static Transform Resolve(
            FaceMotionDiagnosticPresentation presentation,
            GameObject avatarRoot,
            UnityAvatarObjectCache cache)
        {
            if (presentation == null)
            {
                return null;
            }

            switch (presentation.SelectionKind)
            {
                case FaceMotionDiagnosticSelectionKind.AvatarRoot:
                    return avatarRoot == null ? null : avatarRoot.transform;

                case FaceMotionDiagnosticSelectionKind.RelativeTransformPath:
                    return ResolveTransformPath(presentation.ContextId, cache);

                case FaceMotionDiagnosticSelectionKind.RelativeRendererPath:
                    return ResolveRenderer(presentation.ContextId, cache);

                case FaceMotionDiagnosticSelectionKind.RendererParentPath:
                    return ResolveRenderer(
                        RelativePathUtility.GetParentPath(presentation.ContextId),
                        cache);

                case FaceMotionDiagnosticSelectionKind.ParentOfAmbiguousPath:
                    return ResolveParentOfAmbiguousPath(presentation.ContextId, avatarRoot, cache);

                default:
                    return null;
            }
        }

        /// <summary>Whether a safe object can be derived without picking an arbitrary match.</summary>
        public static bool CanResolve(
            FaceMotionDiagnosticPresentation presentation,
            GameObject avatarRoot,
            UnityAvatarObjectCache cache)
        {
            return Resolve(presentation, avatarRoot, cache) != null;
        }

        private static Transform ResolveTransformPath(string relativePath, UnityAvatarObjectCache cache)
        {
            if (cache != null && cache.TryGetTransform(relativePath, out Transform transform))
            {
                return transform;
            }

            return null;
        }

        private static Transform ResolveRenderer(string rendererPath, UnityAvatarObjectCache cache)
        {
            if (cache != null
                && !string.IsNullOrEmpty(rendererPath)
                && cache.TryGetRenderer(rendererPath, out SkinnedMeshRenderer renderer))
            {
                return renderer.transform;
            }

            return null;
        }

        private static Transform ResolveParentOfAmbiguousPath(
            string contextId,
            GameObject avatarRoot,
            UnityAvatarObjectCache cache)
        {
            if (avatarRoot == null)
            {
                return null;
            }

            string parentPath = RelativePathUtility.GetParentPath(contextId);
            if (!string.IsNullOrEmpty(parentPath)
                && cache != null
                && cache.TryGetTransform(parentPath, out Transform parent))
            {
                return parent;
            }

            return avatarRoot.transform;
        }
    }
}