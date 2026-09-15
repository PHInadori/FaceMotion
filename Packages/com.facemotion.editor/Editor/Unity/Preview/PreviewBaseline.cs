using System.Collections.Generic;
using UnityEngine;

namespace FaceMotion.Editor.Preview
{
    /// <summary>Values captured before preview evaluation and restored before each new frame.</summary>
    public sealed class PreviewBaseline
    {
        private readonly Dictionary<Transform, TransformValues> _transforms = new Dictionary<Transform, TransformValues>();
        private readonly Dictionary<BlendShapeKey, float> _blendShapes = new Dictionary<BlendShapeKey, float>();

        public void Capture(Transform transform)
        {
            if (transform != null && !_transforms.ContainsKey(transform))
            {
                _transforms.Add(transform, new TransformValues(transform));
            }
        }

        public void Capture(SkinnedMeshRenderer renderer, int index)
        {
            var key = new BlendShapeKey(renderer, index);
            if (renderer != null && index >= 0 && !_blendShapes.ContainsKey(key))
            {
                _blendShapes.Add(key, renderer.GetBlendShapeWeight(index));
            }
        }

        public void Restore()
        {
            foreach (var pair in _transforms)
            {
                if (pair.Key != null)
                {
                    pair.Value.Apply(pair.Key);
                }
            }

            foreach (var pair in _blendShapes)
            {
                if (pair.Key.Renderer != null)
                {
                    pair.Key.Renderer.SetBlendShapeWeight(pair.Key.Index, pair.Value);
                }
            }
        }

        private readonly struct TransformValues
        {
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;
            private readonly Vector3 _scale;

            public TransformValues(Transform transform)
            {
                _position = transform.localPosition;
                _rotation = transform.localRotation;
                _scale = transform.localScale;
            }

            public void Apply(Transform transform)
            {
                transform.localPosition = _position;
                transform.localRotation = _rotation;
                transform.localScale = _scale;
            }
        }

        private readonly struct BlendShapeKey : System.IEquatable<BlendShapeKey>
        {
            public BlendShapeKey(SkinnedMeshRenderer renderer, int index)
            {
                Renderer = renderer;
                Index = index;
            }

            public SkinnedMeshRenderer Renderer { get; }
            public int Index { get; }

            public bool Equals(BlendShapeKey other)
            {
                return Renderer == other.Renderer && Index == other.Index;
            }

            public override bool Equals(object obj)
            {
                return obj is BlendShapeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (Renderer == null ? 0 : Renderer.GetInstanceID()) ^ Index;
            }
        }
    }
}
