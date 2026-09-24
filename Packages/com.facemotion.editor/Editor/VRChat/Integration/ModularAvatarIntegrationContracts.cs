using System;
using System.Collections.Generic;
using FaceMotion.Diagnostics;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.VRChat.Integration
{
    /// <summary>MA-free public contract. The implementation lives in the package-gated MA assembly.</summary>
    public sealed class ModularAvatarIntegrationRequest
    {
        public ModularAvatarIntegrationRequest(VRCAvatarDescriptor avatar, AnimationClip clip, string outputFolder, string displayName, string animationId = null)
        { Avatar = avatar; Clip = clip; OutputFolder = outputFolder; DisplayName = displayName; AnimationId = animationId ?? string.Empty; }
        public VRCAvatarDescriptor Avatar { get; }
        public AnimationClip Clip { get; }
        public string OutputFolder { get; }
        public string DisplayName { get; }
        /// <summary>Stable project identity used by desired-state reconciliation; never inferred from a display name.</summary>
        public string AnimationId { get; }
    }

    public sealed class ModularAvatarIntegrationPlan
    {
        public ModularAvatarIntegrationPlan(ModularAvatarIntegrationRequest request, string parameterName, string objectName, IReadOnlyList<FaceMotionDiagnostic> diagnostics)
            : this(request, parameterName, objectName, "FaceMotionMA_" + (objectName ?? string.Empty).Replace("FaceMotion MA ", string.Empty), diagnostics) { }
        public ModularAvatarIntegrationPlan(ModularAvatarIntegrationRequest request, string parameterName, string objectName, string rootName, IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        { Request = request; ParameterName = parameterName; ObjectName = objectName; RootName = rootName; Diagnostics = diagnostics ?? Array.Empty<FaceMotionDiagnostic>(); }
        public ModularAvatarIntegrationRequest Request { get; }
        public string ParameterName { get; }
        public string ObjectName { get; }
        public string RootName { get; }
        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }
        public bool IsValid { get { for (var i = 0; i < Diagnostics.Count; i++) if (Diagnostics[i].Blocking) return false; return true; } }
    }

    /// <summary>Pure, all-or-nothing planning input for multiple MA integrations on one avatar.</summary>
    public sealed class ModularAvatarIntegrationBatchPlan
    {
        public ModularAvatarIntegrationBatchPlan(IReadOnlyList<ModularAvatarIntegrationPlan> items)
        { Items = items ?? Array.Empty<ModularAvatarIntegrationPlan>(); }
        public IReadOnlyList<ModularAvatarIntegrationPlan> Items { get; }
        public bool IsValid { get { if (Items.Count == 0) return false; for (var i = 0; i < Items.Count; i++) if (Items[i] == null || !Items[i].IsValid) return false; return true; } }
    }

    public sealed class ModularAvatarIntegrationBatchResult
    {
        public ModularAvatarIntegrationBatchResult(bool succeeded, IReadOnlyList<ModularAvatarIntegrationResult> items, IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        { Succeeded = succeeded; Items = items ?? Array.Empty<ModularAvatarIntegrationResult>(); Diagnostics = diagnostics ?? Array.Empty<FaceMotionDiagnostic>(); }
        public bool Succeeded { get; }
        public IReadOnlyList<ModularAvatarIntegrationResult> Items { get; }
        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }
    }

    public sealed class ModularAvatarIntegrationResult
    {
        public ModularAvatarIntegrationResult(bool succeeded, UnityEngine.Object manifest, IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        { Succeeded = succeeded; Manifest = manifest; Diagnostics = diagnostics ?? Array.Empty<FaceMotionDiagnostic>(); }
        public bool Succeeded { get; }
        public UnityEngine.Object Manifest { get; }
        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }
    }

    /// <summary>Verified ownership metadata used to reconcile MA integrations without display-name matching.</summary>
    public sealed class ModularAvatarManagedState
    {
        public ModularAvatarManagedState(string animationId, string parameterName)
        { AnimationId = animationId ?? string.Empty; ParameterName = parameterName ?? string.Empty; }
        public string AnimationId { get; }
        public string ParameterName { get; }
        public bool HasAnimationId => !string.IsNullOrEmpty(AnimationId);
    }

    public sealed class ModularAvatarManagedStateSnapshot
    {
        public ModularAvatarManagedStateSnapshot(IReadOnlyList<ModularAvatarManagedState> items, IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        { Items = items ?? Array.Empty<ModularAvatarManagedState>(); Diagnostics = diagnostics ?? Array.Empty<FaceMotionDiagnostic>(); }
        public IReadOnlyList<ModularAvatarManagedState> Items { get; }
        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }
        public bool IsValid { get { for (var i = 0; i < Diagnostics.Count; i++) if (Diagnostics[i].Blocking) return false; return true; } }
    }

    /// <summary>Optional transaction boundary for reconciliation backends that can restore a real pre-apply snapshot.</summary>
    public interface IModularAvatarIntegrationRollbackBackend
    {
        object CaptureRollbackSnapshot(VRCAvatarDescriptor avatar);
        ModularAvatarIntegrationBatchResult RestoreRollbackSnapshot(object snapshot);
    }

    /// <summary>Optional final-state planner. Implementations may ignore proven-owned entries scheduled for removal.</summary>
    public interface IModularAvatarDesiredStateBackend
    {
        ModularAvatarIntegrationBatchPlan PlanFinalState(IReadOnlyList<ModularAvatarIntegrationRequest> requests, IReadOnlyList<string> removingParameters);
    }

    public interface IModularAvatarIntegrationBackend
    {
        bool HasExistingIntegration(VRCAvatarDescriptor avatar);
        bool HasExistingIntegration(VRCAvatarDescriptor avatar, string parameter);
        ModularAvatarIntegrationPlan Plan(ModularAvatarIntegrationRequest request);
        ModularAvatarIntegrationResult Apply(ModularAvatarIntegrationPlan plan);
        /// <summary>Removes every FaceMotion Modular Avatar integration from the avatar (including its generated assets).</summary>
        ModularAvatarIntegrationResult Remove(VRCAvatarDescriptor avatar);
        /// <summary>Removes the FaceMotion Modular Avatar integration whose generated parameter matches. Idempotent no-op when absent.</summary>
        ModularAvatarIntegrationResult RemoveAnimation(VRCAvatarDescriptor avatar, string parameter);
        /// <summary>Removes the FaceMotion Modular Avatar integrations for the given generated parameters. Idempotent per parameter.</summary>
        ModularAvatarIntegrationBatchResult RemoveAnimations(VRCAvatarDescriptor avatar, IReadOnlyList<string> parameters);
        ModularAvatarIntegrationBatchPlan PlanBatch(IReadOnlyList<ModularAvatarIntegrationRequest> requests);
        ModularAvatarIntegrationBatchResult ApplyBatch(ModularAvatarIntegrationBatchPlan plan);
        /// <summary>Lists only FaceMotion manifests whose ownership can be proven without mutation.</summary>
        ModularAvatarManagedStateSnapshot InspectManagedState(VRCAvatarDescriptor avatar);
        /// <summary>Verifies every requested removal before any hierarchy or asset mutation occurs.</summary>
        ModularAvatarManagedStateSnapshot ValidateRemovals(VRCAvatarDescriptor avatar, IReadOnlyList<string> parameters);
    }

    public static class ModularAvatarIntegrationBackendLocator
    {
        private const string Implementation = "FaceMotion.Editor.ModularAvatar.ModularAvatarIntegrationBackend, FaceMotion.Editor.ModularAvatar";
        public static IModularAvatarIntegrationBackend Create()
        {
            return Type.GetType(Implementation)?.GetConstructor(Type.EmptyTypes)?.Invoke(null) as IModularAvatarIntegrationBackend;
        }
    }
}
