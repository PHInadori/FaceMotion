using System;
using System.Collections.Generic;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.Avatar;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.VRChat
{
    /// <summary>Whether the avatar is a scene object, a prefab asset, or a prefab instance.</summary>
    public enum AvatarObjectContext
    {
        SceneInstance = 0,
        PrefabAsset = 1,
        PrefabInstance = 2,
        Unknown = 3
    }

    /// <summary>Read-only result of descriptor validation.</summary>
    public sealed class DescriptorValidation
    {
        public DescriptorValidation(
            VRCAvatarDescriptor descriptor,
            GameObject avatarRoot,
            AvatarObjectContext objectContext,
            IReadOnlyList<FaceMotionDiagnostic> diagnostics,
            bool hasBlocking)
        {
            Descriptor = descriptor;
            AvatarRoot = avatarRoot;
            ObjectContext = objectContext;
            Diagnostics = diagnostics ?? new List<FaceMotionDiagnostic>();
            HasBlocking = hasBlocking;
        }

        public VRCAvatarDescriptor Descriptor { get; }

        public GameObject AvatarRoot { get; }

        public AvatarObjectContext ObjectContext { get; }

        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }

        public bool HasBlocking { get; }
    }

    /// <summary>
    /// VRChat-side entry point. Retrieves a VRCAvatarDescriptor, resolves the avatar root
    /// (descriptor.gameObject), determines the object context (prefab asset vs scene
    /// instance), and emits validation diagnostics. Validation reports problems, never
    /// throws; a null descriptor is a diagnostic rather than an exception.
    /// </summary>
    public static class VRCAvatarDescriptorAdapter
    {
        public static AvatarObjectContext DetectObjectContext(GameObject avatarRoot)
        {
            if (avatarRoot == null)
            {
                return AvatarObjectContext.Unknown;
            }

            PrefabAssetType assetType = PrefabUtility.GetPrefabAssetType(avatarRoot);
            if (assetType == PrefabAssetType.NotAPrefab)
            {
                if (avatarRoot.scene.IsValid())
                {
                    return AvatarObjectContext.SceneInstance;
                }

                return AvatarObjectContext.Unknown;
            }

            PrefabInstanceStatus instanceStatus = PrefabUtility.GetPrefabInstanceStatus(avatarRoot);
            if (instanceStatus != PrefabInstanceStatus.NotAPrefab
                && instanceStatus != PrefabInstanceStatus.MissingAsset)
            {
                return AvatarObjectContext.PrefabInstance;
            }

            if (assetType == PrefabAssetType.Regular || assetType == PrefabAssetType.Model)
            {
                return AvatarObjectContext.PrefabAsset;
            }

            return AvatarObjectContext.Unknown;
        }

        /// <summary>
        /// Walks up the hierarchy (scene objects and prefab assets both included) to locate
        /// the nearest VRCAvatarDescriptor, or null when none exists.
        /// </summary>
        public static VRCAvatarDescriptor FindAvatarRoot(GameObject gameObject)
        {
            Transform current = gameObject == null ? null : gameObject.transform;
            while (current != null)
            {
                var descriptor = current.GetComponent<VRCAvatarDescriptor>();
                if (descriptor != null)
                {
                    return descriptor;
                }

                current = current.parent;
            }

            return null;
        }

        /// <summary>Convenience: validates the descriptor and scans its root hierarchy.</summary>
        public static AvatarScanReport ScanAvatar(VRCAvatarDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return new AvatarScanReport(null, new List<FaceMotionDiagnostic>
                {
                    Blocking(
                        FaceMotionDiagnosticCodes.DescriptorMissing,
                        "The VRCAvatarDescriptor is null; nothing can be scanned.",
                        string.Empty,
                        "Provide the descriptor of the avatar to scan.")
                }, true);
            }

            return UnityAvatarScanner.Scan(descriptor.gameObject);
        }

        public static DescriptorValidation Validate(VRCAvatarDescriptor descriptor)
        {
            var diagnostics = new List<FaceMotionDiagnostic>();
            bool hasBlocking = false;

            if (descriptor == null)
            {
                diagnostics.Add(Blocking(
                    FaceMotionDiagnosticCodes.DescriptorMissing,
                    "The VRCAvatarDescriptor is null.",
                    string.Empty,
                    "Provide the avatar descriptor."));
                return new DescriptorValidation(null, null, AvatarObjectContext.Unknown, diagnostics, true);
            }

            GameObject root = descriptor.gameObject;
            AvatarObjectContext context = DetectObjectContext(root);

            Animator animator = root.GetComponent<Animator>();
            bool activeRoot = root.activeSelf;

            if (!activeRoot)
            {
                diagnostics.Add(new FaceMotionDiagnostic(
                    FaceMotionDiagnosticCodes.InactiveAvatarRoot,
                    FaceMotionDiagnosticSeverity.Warning,
                    "The avatar root GameObject is inactive.",
                    root.name,
                    false,
                    "Activate the avatar root, or treat the scan as provisional."));
            }

            if (animator == null)
            {
                diagnostics.Add(new FaceMotionDiagnostic(
                    FaceMotionDiagnosticCodes.MissingAnimator,
                    FaceMotionDiagnosticSeverity.Warning,
                    "The avatar root has no Animator component.",
                    root.name,
                    false,
                    "Add an Animator to drive expression animations."));
            }
            else if (animator.avatar == null)
            {
                diagnostics.Add(new FaceMotionDiagnostic(
                    FaceMotionDiagnosticCodes.AnimatorAvatarNull,
                    FaceMotionDiagnosticSeverity.Warning,
                    "The avatar's Animator has no avatar assigned.",
                    root.name,
                    false,
                    "Assign the Avatar to the Animator."));
            }
            else if (!animator.isHuman)
            {
                diagnostics.Add(new FaceMotionDiagnostic(
                    FaceMotionDiagnosticCodes.NonHumanoidAvatar,
                    FaceMotionDiagnosticSeverity.Warning,
                    "The avatar is not Humanoid; generic avatars are not blocked but facial binding may be limited.",
                    root.name,
                    false,
                    "Verify the avatar has the intended blend shapes."));
            }

            return new DescriptorValidation(descriptor, root, context, diagnostics, hasBlocking);
        }

        private static FaceMotionDiagnostic Blocking(string code, string message, string contextId, string fix)
        {
            return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Error, message, contextId, true, fix);
        }
    }
}