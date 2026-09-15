using System;
using FaceMotion.Data;
using FaceMotion.Editor.Avatar;
using UnityEngine;

namespace FaceMotion.Editor.Preview
{
    /// <summary>Live-object lookup cache for one preview clone or explicit scene apply target.</summary>
    public sealed class PreviewObjectCache
    {
        private readonly UnityAvatarObjectCache _objects = new UnityAvatarObjectCache();

        public PreviewObjectCache(GameObject root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            _objects.Rebuild(root);
        }

        public bool TryGetTransform(string path, out Transform transform)
        {
            return _objects.TryGetTransform(path, out transform);
        }

        public bool TryGetBlendShape(BlendShapeBinding binding, out SkinnedMeshRenderer renderer, out int index)
        {
            index = -1;
            return _objects.TryGetRenderer(binding.RendererPath, out renderer)
                && _objects.TryGetBlendShapeIndex(binding, out index);
        }
    }
}
