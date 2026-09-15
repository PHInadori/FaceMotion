using System;
using System.Collections.Generic;
using FaceMotion.Avatar;
using FaceMotion.Data;
using UnityEngine;

namespace FaceMotion.Editor.Avatar
{
    /// <summary>
    /// Unity-side runtime cache mapping avatar identities back to alive objects. The cache is
    /// never serialized; a fresh Rebuild increments the revision and forgets the previous
    /// objects. Ambiguous identities are excluded from the unique maps so lookups report
    /// false instead of returning an arbitrary match.
    /// </summary>
    public sealed class UnityAvatarObjectCache
    {
        private readonly Dictionary<string, Transform> _uniqueTransforms =
            new Dictionary<string, Transform>(StringComparer.Ordinal);

        private readonly HashSet<string> _ambiguousTransforms = new HashSet<string>(StringComparer.Ordinal);

        private readonly Dictionary<string, SkinnedMeshRenderer> _uniqueRenderers =
            new Dictionary<string, SkinnedMeshRenderer>(StringComparer.Ordinal);

        private readonly HashSet<string> _ambiguousRenderers = new HashSet<string>(StringComparer.Ordinal);

        private readonly Dictionary<BlendShapeBinding, int> _blendShapeIndices =
            new Dictionary<BlendShapeBinding, int>();

        private readonly HashSet<BlendShapeBinding> _ambiguousBlendShapes =
            new HashSet<BlendShapeBinding>();

        private AvatarIndex _index;

        public int Revision { get; private set; }

        public AvatarIndex Index => _index;

        public bool TryGetTransform(string relativePath, out Transform transform)
        {
            if (relativePath != null && !_ambiguousTransforms.Contains(relativePath))
            {
                return _uniqueTransforms.TryGetValue(relativePath, out transform);
            }

            transform = null;
            return false;
        }

        public bool TryGetRenderer(string relativePath, out SkinnedMeshRenderer renderer)
        {
            if (relativePath != null && !_ambiguousRenderers.Contains(relativePath))
            {
                return _uniqueRenderers.TryGetValue(relativePath, out renderer);
            }

            renderer = null;
            return false;
        }

        public bool TryGetBlendShapeIndex(BlendShapeBinding identity, out int index)
        {
            if (!_ambiguousBlendShapes.Contains(identity))
            {
                return _blendShapeIndices.TryGetValue(identity, out index);
            }

            index = -1;
            return false;
        }

        public AvatarScanReport Rebuild(GameObject root)
        {
            AvatarScanReport report = UnityAvatarScanner.Scan(root);
            Revision++;
            Clear();
            _index = report.Index;
            if (_index != null)
            {
                BuildFromObjects(root.transform, _index);
            }

            return report;
        }

        public void Clear()
        {
            _uniqueTransforms.Clear();
            _ambiguousTransforms.Clear();
            _uniqueRenderers.Clear();
            _ambiguousRenderers.Clear();
            _blendShapeIndices.Clear();
            _ambiguousBlendShapes.Clear();
            _index = null;
        }

        private void BuildFromObjects(Transform root, AvatarIndex index)
        {
            var allTransforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform element = allTransforms[i];
                string path = RelativePathUtility.GetRelativePath(root, element);
                if (path == null)
                {
                    continue;
                }

                NameResolutionStatus status = index.ResolveTransform(path).Status;
                if (status == NameResolutionStatus.Unique)
                {
                    _uniqueTransforms[path] = element;
                }
                else if (status == NameResolutionStatus.Ambiguous)
                {
                    _ambiguousTransforms.Add(path);
                }
            }

            var allRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                SkinnedMeshRenderer renderer = allRenderers[i];
                string rendererPath = RelativePathUtility.GetRelativePath(root, renderer.transform);
                if (rendererPath == null)
                {
                    continue;
                }

                if (index.TryFindRenderer(rendererPath, out _))
                {
                    _uniqueRenderers[rendererPath] = renderer;
                }
                else
                {
                    _ambiguousRenderers.Add(rendererPath);
                }

                Mesh mesh = renderer.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                for (int blendIndex = 0; blendIndex < mesh.blendShapeCount; blendIndex++)
                {
                    var identity = new BlendShapeBinding(
                        rendererPath,
                        mesh.GetBlendShapeName(blendIndex));
                    BlendShapeResolution resolution = index.ResolveBlendShape(identity);
                    if (resolution.Status == NameResolutionStatus.Unique)
                    {
                        _blendShapeIndices[identity] = blendIndex;
                    }
                    else if (resolution.Status == NameResolutionStatus.Ambiguous)
                    {
                        _ambiguousBlendShapes.Add(identity);
                    }
                }
            }
        }
    }
}