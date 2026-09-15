using System;
using System.Collections.Generic;
using FaceMotion.Data;

namespace FaceMotion.Avatar
{
    public enum NameResolutionStatus
    {
        NotFound = 0,
        Unique = 1,
        Ambiguous = 2
    }

    /// <summary>Result of resolving one transform path against an avatar index.</summary>
    public sealed class TransformResolution
    {
        public TransformResolution(NameResolutionStatus status, TransformIndexEntry entry, int count)
        {
            Status = status;
            Entry = entry;
            Count = count;
        }

        public NameResolutionStatus Status { get; }

        public TransformIndexEntry Entry { get; }

        public int Count { get; }
    }

    /// <summary>Result of resolving one (renderer path, blend shape name) identity.</summary>
    public sealed class BlendShapeResolution
    {
        public BlendShapeResolution(NameResolutionStatus status, BlendShapeIndexEntry entry, int count)
        {
            Status = status;
            Entry = entry;
            Count = count;
        }

        public NameResolutionStatus Status { get; }

        public BlendShapeIndexEntry Entry { get; }

        public int Count { get; }
    }

    /// <summary>
    /// Immutable SDK-neutral snapshot of one avatar structure. An AvatarIndex is a runtime
    /// editor cache rebuilt when the avatar changes; it is never persisted into a FaceMotion
    /// project. Lookups use ordinal case-sensitive dictionaries built once at construction;
    /// repeated linear scans are avoided.
    /// </summary>
    public sealed class AvatarIndex
    {
        private readonly Dictionary<string, List<int>> _transformIndex;
        private readonly Dictionary<string, List<int>> _rendererIndex;
        private readonly Dictionary<BlendShapeBinding, List<int>> _blendShapeIndex;
        private readonly IReadOnlyList<TransformIndexEntry> _transforms;
        private readonly IReadOnlyList<RendererIndexEntry> _renderers;
        private readonly IReadOnlyList<BlendShapeIndexEntry> _blendShapes;

        private AvatarIndex(
            AvatarFingerprint fingerprint,
            IReadOnlyList<TransformIndexEntry> transforms,
            IReadOnlyList<RendererIndexEntry> renderers,
            IReadOnlyList<BlendShapeIndexEntry> blendShapes)
        {
            Fingerprint = fingerprint;
            _transforms = transforms;
            _renderers = renderers;
            _blendShapes = blendShapes;

            _transformIndex = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (int i = 0; i < transforms.Count; i++)
            {
                Add(_transformIndex, transforms[i].RelativePath, i);
            }

            _rendererIndex = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (int i = 0; i < renderers.Count; i++)
            {
                Add(_rendererIndex, renderers[i].RelativePath, i);
            }

            _blendShapeIndex = new Dictionary<BlendShapeBinding, List<int>>();
            for (int i = 0; i < blendShapes.Count; i++)
            {
                var identity = new BlendShapeBinding(
                    blendShapes[i].RendererPath,
                    blendShapes[i].BlendShapeName);
                Add(_blendShapeIndex, identity, i);
            }
        }

        public AvatarFingerprint Fingerprint { get; }

        public IReadOnlyList<TransformIndexEntry> Transforms => _transforms;

        public IReadOnlyList<RendererIndexEntry> Renderers => _renderers;

        public IReadOnlyList<BlendShapeIndexEntry> BlendShapes => _blendShapes;

        public int TransformCount => _transforms.Count;

        public int RendererCount => _renderers.Count;

        public int BlendShapeCount => _blendShapes.Count;

        public static AvatarIndex Create(
            AvatarFingerprint fingerprint,
            IReadOnlyList<TransformIndexEntry> transforms,
            IReadOnlyList<RendererIndexEntry> renderers,
            IReadOnlyList<BlendShapeIndexEntry> blendShapes)
        {
            if (fingerprint == null)
            {
                throw new ArgumentNullException(nameof(fingerprint));
            }

            transforms = transforms ?? new List<TransformIndexEntry>();
            renderers = renderers ?? new List<RendererIndexEntry>();
            blendShapes = blendShapes ?? new List<BlendShapeIndexEntry>();
            return new AvatarIndex(fingerprint, transforms, renderers, blendShapes);
        }

        public bool HasTransform(string relativePath)
        {
            return relativePath != null && _transformIndex.ContainsKey(relativePath);
        }

        public TransformResolution ResolveTransform(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return new TransformResolution(NameResolutionStatus.NotFound, null, 0);
            }

            if (_transformIndex.TryGetValue(relativePath, out var matches))
            {
                return new TransformResolution(
                    matches.Count == 1 ? NameResolutionStatus.Unique : NameResolutionStatus.Ambiguous,
                    matches.Count == 1 ? _transforms[matches[0]] : null,
                    matches.Count);
            }

            return new TransformResolution(NameResolutionStatus.NotFound, null, 0);
        }

        public bool HasRenderer(string relativePath)
        {
            return relativePath != null && _rendererIndex.ContainsKey(relativePath);
        }

        public bool TryFindRenderer(string relativePath, out RendererIndexEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(relativePath)
                || !_rendererIndex.TryGetValue(relativePath, out var matches)
                || matches.Count != 1)
            {
                return false;
            }

            entry = _renderers[matches[0]];
            return true;
        }

        public BlendShapeResolution ResolveBlendShape(BlendShapeBinding identity)
        {
            if (!identity.IsValid)
            {
                return new BlendShapeResolution(NameResolutionStatus.NotFound, null, 0);
            }

            if (_blendShapeIndex.TryGetValue(identity, out var matches))
            {
                return new BlendShapeResolution(
                    matches.Count == 1 ? NameResolutionStatus.Unique : NameResolutionStatus.Ambiguous,
                    matches.Count == 1 ? _blendShapes[matches[0]] : null,
                    matches.Count);
            }

            return new BlendShapeResolution(NameResolutionStatus.NotFound, null, 0);
        }

        private static void Add<T>(Dictionary<T, List<int>> dictionary, T key, int index) where T : notnull
        {
            if (dictionary.TryGetValue(key, out var matches))
            {
                matches.Add(index);
                return;
            }

            dictionary.Add(key, new List<int> { index });
        }
    }
}