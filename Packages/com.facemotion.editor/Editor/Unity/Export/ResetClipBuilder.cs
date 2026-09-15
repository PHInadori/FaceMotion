using System;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Export
{
    /// <summary>Creates a static clip that restores only the source clip's supported bindings to the avatar baseline.</summary>
    public static class ResetClipBuilder
    {
        public static AnimationClip Create(VRCAvatarDescriptor avatar, AnimationClip source)
        {
            var reset = new AnimationClip { frameRate = source.frameRate };
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                var target = string.IsNullOrEmpty(binding.path) ? avatar.transform : avatar.transform.Find(binding.path);
                if (target == null) throw new InvalidOperationException("Could not resolve animation binding path '" + binding.path + "' on the avatar.");
                float baseline = GetBaselineValue(target, binding);
                reset.SetCurve(binding.path, binding.type, binding.propertyName, new AnimationCurve(new Keyframe(0f, baseline), new Keyframe(1f / reset.frameRate, baseline)));
            }
            return reset;
        }

        private static float GetBaselineValue(Transform target, EditorCurveBinding binding)
        {
            if (binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal))
            {
                var renderer = target.GetComponent<SkinnedMeshRenderer>();
                var blendShapeName = binding.propertyName.Substring("blendShape.".Length);
                if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.GetBlendShapeIndex(blendShapeName) < 0) throw new InvalidOperationException("Could not resolve blend shape binding '" + binding.path + "/" + blendShapeName + "' on the avatar.");
                return renderer.GetBlendShapeWeight(renderer.sharedMesh.GetBlendShapeIndex(blendShapeName));
            }
            if (binding.type == typeof(Transform))
            {
                if (binding.propertyName == "m_LocalPosition.x") return target.localPosition.x;
                if (binding.propertyName == "m_LocalPosition.y") return target.localPosition.y;
                if (binding.propertyName == "m_LocalPosition.z") return target.localPosition.z;
                if (binding.propertyName == "m_LocalRotation.x") return target.localRotation.x;
                if (binding.propertyName == "m_LocalRotation.y") return target.localRotation.y;
                if (binding.propertyName == "m_LocalRotation.z") return target.localRotation.z;
                if (binding.propertyName == "m_LocalRotation.w") return target.localRotation.w;
                if (binding.propertyName == "m_LocalScale.x") return target.localScale.x;
                if (binding.propertyName == "m_LocalScale.y") return target.localScale.y;
                if (binding.propertyName == "m_LocalScale.z") return target.localScale.z;
            }
            throw new InvalidOperationException("Unsupported animation binding '" + binding.path + "/" + binding.propertyName + "'.");
        }
    }
}
