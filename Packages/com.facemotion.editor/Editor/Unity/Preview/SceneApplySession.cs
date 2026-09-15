using System;
using FaceMotion.Data;
using UnityEngine;

namespace FaceMotion.Editor.Preview
{
    /// <summary>Explicit, reversible application of the current preview frame to a scene avatar.</summary>
    public sealed class SceneApplySession : IDisposable
    {
        private PreviewObjectCache _cache;
        private readonly PreviewBaseline _baseline = new PreviewBaseline();
        private GameObject _root;

        public bool IsActive => _cache != null;

        public GameObject Root => _root;

        public void Start(GameObject root)
        {
            if (ReferenceEquals(root, _root) && IsActive) return;
            Dispose();
            if (root != null)
            {
                _root = root;
                _cache = new PreviewObjectCache(root);
            }
        }

        public void Apply(FaceMotionAnimationData animation, float time)
        {
            if (_cache != null)
            {
                PreviewMotionApplier.Apply(animation, _cache, _baseline, time);
            }
        }

        public void Dispose()
        {
            _baseline.Restore();
            _cache = null;
            _root = null;
        }
    }
}
