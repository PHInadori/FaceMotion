using System;
using System.Collections.Generic;
using System.Reflection;
using FaceMotion.Avatar;
using FaceMotion.Data;
using FaceMotion.Editor.Avatar;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.VRChat
{
    /// <summary>Cached descriptor and FX usage facts for one avatar scan.</summary>
    public sealed class VrcBlendShapeConflictIndex
    {
        private readonly HashSet<string> _eyelids = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _lipSync = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _fxBindings = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _maBindings = new HashSet<string>(StringComparer.Ordinal);

        public static VrcBlendShapeConflictIndex Build(VRCAvatarDescriptor descriptor)
        {
            var index = new VrcBlendShapeConflictIndex();
            if (descriptor == null)
            {
                return index;
            }

            AddEyelids(index, descriptor);
            AddLipSync(index, descriptor);
            AddFxBindings(index, descriptor);
            AddModularAvatarBindings(index, descriptor);
            return index;
        }

        public VrcBlendShapeConflict Get(BlendShapeBinding binding)
        {
            string key = Key(binding.RendererPath, binding.BlendShapeName);
            if (_eyelids.Contains(key))
            {
                return new VrcBlendShapeConflict(true, false, "VRChat Blink / Eyelids", "VRC Avatar Descriptor / Eyelids");
            }

            if (_lipSync.Contains(key))
            {
                return new VrcBlendShapeConflict(true, false, "VRChat LipSync", "VRC Avatar Descriptor / LipSync");
            }

            if (_fxBindings.Contains(key))
            {
                return new VrcBlendShapeConflict(false, true, "Existing FX Animator", "VRC Avatar Descriptor / FX Animator");
            }

            if (_maBindings.Contains(key))
            {
                return new VrcBlendShapeConflict(false, true, "Modular Avatar Merge Animator", "Modular Avatar / Merge Animator");
            }

            return VrcBlendShapeConflict.None;
        }

        public bool IsEyelid(BlendShapeBinding binding)
        {
            return _eyelids.Contains(Key(binding.RendererPath, binding.BlendShapeName));
        }

        public bool IsLipSync(BlendShapeBinding binding)
        {
            return _lipSync.Contains(Key(binding.RendererPath, binding.BlendShapeName));
        }

        private static void AddEyelids(VrcBlendShapeConflictIndex index, VRCAvatarDescriptor descriptor)
        {
            var settings = descriptor.customEyeLookSettings;
            if (settings.eyelidType != VRCAvatarDescriptor.EyelidType.Blendshapes || settings.eyelidsSkinnedMesh == null
                || settings.eyelidsSkinnedMesh.sharedMesh == null || settings.eyelidsBlendshapes == null)
            {
                return;
            }

            string path = RelativePathUtility.GetRelativePath(descriptor.transform, settings.eyelidsSkinnedMesh.transform);
            if (path == null)
            {
                return;
            }

            Mesh mesh = settings.eyelidsSkinnedMesh.sharedMesh;
            for (int i = 0; i < settings.eyelidsBlendshapes.Length; i++)
            {
                int shapeIndex = settings.eyelidsBlendshapes[i];
                if (shapeIndex >= 0 && shapeIndex < mesh.blendShapeCount)
                {
                    index._eyelids.Add(Key(path, mesh.GetBlendShapeName(shapeIndex)));
                }
            }
        }

        private static void AddLipSync(VrcBlendShapeConflictIndex index, VRCAvatarDescriptor descriptor)
        {
            if (descriptor.VisemeSkinnedMesh == null || descriptor.VisemeBlendShapes == null)
            {
                return;
            }

            string path = RelativePathUtility.GetRelativePath(descriptor.transform, descriptor.VisemeSkinnedMesh.transform);
            if (path == null)
            {
                return;
            }

            for (int i = 0; i < descriptor.VisemeBlendShapes.Length; i++)
            {
                string shape = descriptor.VisemeBlendShapes[i];
                if (!string.IsNullOrEmpty(shape))
                {
                    index._lipSync.Add(Key(path, shape));
                }
            }
        }

        private static void AddFxBindings(VrcBlendShapeConflictIndex index, VRCAvatarDescriptor descriptor)
        {
            RuntimeAnimatorController controller = GetFx(descriptor);
            var animator = controller as AnimatorController;
            if (animator == null)
            {
                return;
            }

            AddAnimatorBindings(index._fxBindings, animator);
        }

        private static void AddModularAvatarBindings(VrcBlendShapeConflictIndex index, VRCAvatarDescriptor descriptor)
        {
            Component[] components = descriptor.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component.GetType().FullName != "nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator")
                {
                    continue;
                }

                FieldInfo animatorField = component.GetType().GetField("animator", BindingFlags.Instance | BindingFlags.Public);
                var animator = animatorField == null ? null : animatorField.GetValue(component) as AnimatorController;
                if (animator != null)
                {
                    AddAnimatorBindings(index._maBindings, animator);
                }
            }
        }

        private static void AddAnimatorBindings(HashSet<string> target, AnimatorController animator)
        {
            AnimationClip[] clips = animator.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null)
                {
                    continue;
                }

                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clips[i]);
                for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
                {
                    EditorCurveBinding binding = bindings[bindingIndex];
                    const string prefix = "blendShape.";
                    if (binding.type == typeof(SkinnedMeshRenderer)
                        && binding.propertyName != null && binding.propertyName.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        target.Add(Key(binding.path, binding.propertyName.Substring(prefix.Length)));
                    }
                }
            }
        }

        private static RuntimeAnimatorController GetFx(VRCAvatarDescriptor descriptor)
        {
            if (descriptor.baseAnimationLayers == null)
            {
                return null;
            }

            for (int i = 0; i < descriptor.baseAnimationLayers.Length; i++)
            {
                if (descriptor.baseAnimationLayers[i].type == VRCAvatarDescriptor.AnimLayerType.FX)
                {
                    return descriptor.baseAnimationLayers[i].animatorController;
                }
            }

            return null;
        }

        private static string Key(string rendererPath, string blendShapeName)
        {
            return (rendererPath ?? string.Empty) + "\n" + (blendShapeName ?? string.Empty);
        }
    }

    public readonly struct VrcBlendShapeConflict
    {
        public static readonly VrcBlendShapeConflict None = new VrcBlendShapeConflict(false, false, string.Empty, string.Empty);

        public VrcBlendShapeConflict(bool conflict, bool warning, string reason, string source)
        {
            IsConflict = conflict;
            IsWarning = warning;
            Reason = reason ?? string.Empty;
            Source = source ?? string.Empty;
        }

        public bool IsConflict { get; }
        public bool IsWarning { get; }
        public string Reason { get; }
        public string Source { get; }
    }
}
