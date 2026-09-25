using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Avatar
{
    /// <summary>Canonical identity for one Unity float animation binding.</summary>
    public readonly struct AnimationBindingKey : IEquatable<AnimationBindingKey>
    {
        private AnimationBindingKey(string path, Type componentType, string propertyName)
        {
            Path = path ?? string.Empty;
            ComponentType = componentType;
            PropertyName = propertyName ?? string.Empty;
        }

        public string Path { get; }
        public Type ComponentType { get; }
        public string PropertyName { get; }

        public static AnimationBindingKey From(EditorCurveBinding binding)
        {
            return new AnimationBindingKey(binding.path, binding.type, binding.propertyName);
        }

        public static AnimationBindingKey ForBlendShape(string rendererPath, string blendShapeName)
        {
            return new AnimationBindingKey(rendererPath, typeof(SkinnedMeshRenderer), "blendShape." + (blendShapeName ?? string.Empty));
        }

        public static IEnumerable<EditorCurveBinding> SharedFloatBindings(AnimationClip first, AnimationClip second)
        {
            var bindings = new HashSet<AnimationBindingKey>();
            foreach (var binding in AnimationUtility.GetCurveBindings(first))
            {
                bindings.Add(From(binding));
            }

            foreach (var binding in AnimationUtility.GetCurveBindings(second))
            {
                if (bindings.Contains(From(binding)))
                {
                    yield return binding;
                }
            }
        }

        public bool Equals(AnimationBindingKey other)
        {
            return ComponentType == other.ComponentType
                && string.Equals(Path, other.Path, StringComparison.Ordinal)
                && string.Equals(PropertyName, other.PropertyName, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is AnimationBindingKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ComponentType == null ? 0 : ComponentType.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Path);
                return (hash * 397) ^ StringComparer.Ordinal.GetHashCode(PropertyName);
            }
        }

        public string ToCanonicalString()
        {
            return Path + "\n"
                + (ComponentType == null ? string.Empty : ComponentType.FullName) + "\n"
                + PropertyName;
        }
    }
}
