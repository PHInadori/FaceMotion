using System;
using System.Collections.Generic;
using FaceMotion.Diagnostics;
using FaceMotion.Serialization;
using FaceMotion.Versioning;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.VRChat.Integration
{
    /// <summary>
    /// Migrates a persisted DirectIntegrationManifest at its use boundary (Apply/Rollback).
    /// Future-schema manifests and manifests whose avatar cannot be resolved are blocked
    /// before anything is mutated. State is reconstructed from live avatar references only
    /// when the manifest never recorded one (Unknown/legacy); a recorded state is left as-is
    /// because runtime ownership resolution governs each operation.
    /// </summary>
    public static class DirectIntegrationManifestMigration
    {
        public static MigrationResult TryMigrateOnUse(DirectIntegrationManifest manifest, ICollection<FaceMotionDiagnostic> diagnostics = null)
        {
            var items = diagnostics ?? new List<FaceMotionDiagnostic>();
            if (manifest == null)
            {
                items.Add(Error("FM-G-MANIFEST", "The Direct integration manifest is missing.", string.Empty, "Restore the manifest or re-integrate."));
                return new MigrationResult(false, true, items);
            }

            if (manifest.SchemaVersion > FaceMotionVersions.IntegrationManifestVersion)
            {
                items.Add(Error(FaceMotionDiagnosticCodes.FutureSchemaBlocked, $"The integration manifest uses schema {manifest.SchemaVersion}, newer than this tool's {FaceMotionVersions.IntegrationManifestVersion}.", manifest.IntegrationId, "Upgrade the tool before managing this integration."));
                return new MigrationResult(false, true, items);
            }

            if (ResolveAvatar(manifest) == null)
            {
                items.Add(Error(FaceMotionDiagnosticCodes.IntegrationAmbiguous, "The integration manifest's avatar reference cannot be resolved.", manifest.IntegrationId, "Restore the avatar reference before continuing."));
                return new MigrationResult(false, true, items);
            }

            bool applied = false;

            if (manifest.SchemaVersion != FaceMotionVersions.IntegrationManifestVersion)
            {
                manifest.SchemaVersion = FaceMotionVersions.IntegrationManifestVersion;
                applied = true;
            }

            if (!string.Equals(manifest.BackendId, DirectVRChatIntegration.BackendId, StringComparison.Ordinal))
            {
                manifest.BackendId = DirectVRChatIntegration.BackendId;
                applied = true;
                items.Add(new FaceMotionDiagnostic(FaceMotionDiagnosticCodes.PartialRecovery, FaceMotionDiagnosticSeverity.Warning, "The manifest's backend was recorded from its asset type.", manifest.IntegrationId, false, string.Empty));
            }

            if (MigrationIds.NeedsRepair(manifest.IntegrationId))
            {
                manifest.IntegrationId = MigrationIds.Repair(manifest.IntegrationId);
                applied = true;
                items.Add(new FaceMotionDiagnostic(FaceMotionDiagnosticCodes.IdRepaired, FaceMotionDiagnosticSeverity.Warning, "The integration had no valid ID; a new one was generated.", manifest.IntegrationId, false, "It is preserved on the next apply."));
            }

            if (manifest.State == IntegrationState.Unknown)
            {
                manifest.State = EvaluateState(manifest);
                applied = true;
            }

            if (applied)
            {
                items.Add(new FaceMotionDiagnostic(FaceMotionDiagnosticCodes.UpgradedSchema, FaceMotionDiagnosticSeverity.Info, "Integration manifest was upgraded to the current compatibility schema.", manifest.IntegrationId, false, string.Empty));
            }

            return new MigrationResult(applied, false, items);
        }

        /// <summary>Runtime-verified attach state for a Direct integration.</summary>
        public static IntegrationState EvaluateState(DirectIntegrationManifest manifest)
        {
            var avatar = ResolveAvatar(manifest);
            if (manifest == null || avatar == null)
            {
                return IntegrationState.Unknown;
            }

            bool attached = GetFx(avatar) == manifest.GeneratedFx
                && avatar.expressionParameters == manifest.GeneratedParameters
                && avatar.expressionsMenu == manifest.GeneratedMenu;
            return attached ? IntegrationState.Attached : IntegrationState.Detached;
        }

        private static RuntimeAnimatorController GetFx(VRCAvatarDescriptor avatar)
        {
            if (avatar == null || avatar.baseAnimationLayers == null)
            {
                return null;
            }

            foreach (var layer in avatar.baseAnimationLayers)
            {
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX)
                {
                    return layer.animatorController;
                }
            }

            return null;
        }

        internal static VRCAvatarDescriptor ResolveAvatar(DirectIntegrationManifest manifest)
        {
            if (manifest == null) return null;
            if (manifest.Avatar != null) return manifest.Avatar;
            if (string.IsNullOrEmpty(manifest.AvatarGlobalId)) return null;
            return GlobalObjectId.TryParse(manifest.AvatarGlobalId, out var id)
                ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as VRCAvatarDescriptor
                : null;
        }

        private static FaceMotionDiagnostic Error(string code, string message, string contextId, string fix)
        {
            return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Error, message, contextId, true, fix);
        }
    }
}
