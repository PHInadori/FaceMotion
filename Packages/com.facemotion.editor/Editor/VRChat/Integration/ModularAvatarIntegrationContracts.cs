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
        public ModularAvatarIntegrationRequest(VRCAvatarDescriptor avatar, AnimationClip clip, string outputFolder, string displayName)
        { Avatar = avatar; Clip = clip; OutputFolder = outputFolder; DisplayName = displayName; }
        public VRCAvatarDescriptor Avatar { get; }
        public AnimationClip Clip { get; }
        public string OutputFolder { get; }
        public string DisplayName { get; }
    }

    public sealed class ModularAvatarIntegrationPlan
    {
        public ModularAvatarIntegrationPlan(ModularAvatarIntegrationRequest request, string parameterName, string objectName, IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        { Request = request; ParameterName = parameterName; ObjectName = objectName; Diagnostics = diagnostics ?? Array.Empty<FaceMotionDiagnostic>(); }
        public ModularAvatarIntegrationRequest Request { get; }
        public string ParameterName { get; }
        public string ObjectName { get; }
        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }
        public bool IsValid { get { for (var i = 0; i < Diagnostics.Count; i++) if (Diagnostics[i].Blocking) return false; return true; } }
    }

    public sealed class ModularAvatarIntegrationResult
    {
        public ModularAvatarIntegrationResult(bool succeeded, UnityEngine.Object manifest, IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        { Succeeded = succeeded; Manifest = manifest; Diagnostics = diagnostics ?? Array.Empty<FaceMotionDiagnostic>(); }
        public bool Succeeded { get; }
        public UnityEngine.Object Manifest { get; }
        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }
    }

    public interface IModularAvatarIntegrationBackend
    {
        bool HasExistingIntegration(VRCAvatarDescriptor avatar);
        ModularAvatarIntegrationPlan Plan(ModularAvatarIntegrationRequest request);
        ModularAvatarIntegrationResult Apply(ModularAvatarIntegrationPlan plan);
        ModularAvatarIntegrationResult Remove(VRCAvatarDescriptor avatar);
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
