using System;
using System.Collections.Generic;
using FaceMotion.Editor.UI.Localization;
using FaceMotion.Avatar;
using FaceMotion.Data;

namespace FaceMotion.Editor.UI.Support
{
    /// <summary>
    /// Immutable UI candidate list built once per avatar scan. Searching filters this
    /// snapshot; it is never rebuilt per OnGUI pass.
    /// </summary>
    public sealed class AvatarCandidateSnapshot
    {
        public sealed class BlendShapeCandidate
        {
            public BlendShapeCandidate(string rendererPath, string blendShapeName)
            {
                RendererPath = rendererPath ?? string.Empty;
                BlendShapeName = blendShapeName ?? string.Empty;
            }

            public string RendererPath { get; }

            public string BlendShapeName { get; }

            public string DisplayLabel => RendererPath + " / " + BlendShapeName;

            public BlendShapeBinding ToBinding()
            {
                return new BlendShapeBinding(RendererPath, BlendShapeName);
            }
        }

        public sealed class TransformCandidate
        {
            public TransformCandidate(string relativePath)
            {
                RelativePath = relativePath ?? string.Empty;
            }

            public string RelativePath { get; }

            public string DisplayLabel => string.IsNullOrEmpty(RelativePath) ? FaceMotionUiText.Get("avatarRoot") : RelativePath;
        }

        private readonly List<BlendShapeCandidate> _blendShapes = new List<BlendShapeCandidate>();
        private readonly List<TransformCandidate> _transforms = new List<TransformCandidate>();

        public int BlendShapeCount => _blendShapes.Count;

        public int TransformCount => _transforms.Count;

        public static AvatarCandidateSnapshot Build(AvatarIndex index)
        {
            var snapshot = new AvatarCandidateSnapshot();
            if (index == null)
            {
                return snapshot;
            }

            var blendShapes = new List<BlendShapeCandidate>(index.BlendShapeCount);
            for (int i = 0; i < index.BlendShapes.Count; i++)
            {
                var entry = index.BlendShapes[i];
                if (entry != null)
                {
                    blendShapes.Add(new BlendShapeCandidate(entry.RendererPath, entry.BlendShapeName));
                }
            }

            blendShapes.Sort(
                (a, b) =>
                {
                    int byPath = string.CompareOrdinal(a.RendererPath, b.RendererPath);
                    return byPath != 0 ? byPath : string.CompareOrdinal(a.BlendShapeName, b.BlendShapeName);
                });
            snapshot._blendShapes.AddRange(blendShapes);

            var transforms = new List<TransformCandidate>(index.TransformCount);
            for (int i = 0; i < index.Transforms.Count; i++)
            {
                var entry = index.Transforms[i];
                if (entry != null)
                {
                    transforms.Add(new TransformCandidate(entry.RelativePath));
                }
            }

            transforms.Sort((a, b) => string.CompareOrdinal(a.RelativePath, b.RelativePath));
            snapshot._transforms.AddRange(transforms);
            return snapshot;
        }

        public IReadOnlyList<BlendShapeCandidate> FilterBlendShapes(string query)
        {
            return Filter(
                _blendShapes,
                candidate => Contains(candidate.BlendShapeName, query) || Contains(candidate.RendererPath, query));
        }

        public IReadOnlyList<TransformCandidate> FilterTransforms(string query)
        {
            return Filter(
                _transforms,
                candidate => Contains(candidate.DisplayLabel, query));
        }

        private static bool Contains(string text, string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }

            return !string.IsNullOrEmpty(text)
                && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<T> Filter<T>(IReadOnlyList<T> source, Func<T, bool> predicate)
        {
            var result = new List<T>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                if (predicate(source[i]))
                {
                    result.Add(source[i]);
                }
            }

            return result;
        }
    }
}
