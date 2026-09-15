using System;
using System.Collections.Generic;
using FaceMotion.Diagnostics;
using FaceMotion.Serialization;
using FaceMotion.Versioning;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.ModularAvatar
{
    /// <summary>
    /// Migrates a persisted ModularAvatarIntegrationManifest at its use boundary (Apply/Remove).
    /// Future-schema manifests and manifests whose ownership cannot be resolved are blocked
    /// before anything is mutated. State is reconstructed from the live hierarchy only when the
    /// manifest never recorded one (Unknown/legacy): the owned integration object resolves to
    /// Attached, no object means Detached (assets retained), and a foreign object that merely
    /// has the recorded integration name is ambiguous and blocking.
    /// </summary>
    public static class ModularAvatarIntegrationManifestMigration
    {
        public static MigrationResult TryMigrateOnUse(
            ModularAvatarIntegrationManifest manifest,
            VRCAvatarDescriptor operationAvatar,
            ICollection<FaceMotionDiagnostic> diagnostics = null)
        {
            var items = diagnostics ?? new List<FaceMotionDiagnostic>();
            if (manifest == null)
            {
                items.Add(Error("FM-H-MA-MANIFEST", "The Modular Avatar manifest is missing.", string.Empty, "Restore the manifest or re-integrate."));
                return new MigrationResult(false, true, items);
            }

            if (manifest.SchemaVersion > FaceMotionVersions.IntegrationManifestVersion)
            {
                items.Add(Error(FaceMotionDiagnosticCodes.FutureSchemaBlocked, $"The integration manifest uses schema {manifest.SchemaVersion}, newer than this tool's {FaceMotionVersions.IntegrationManifestVersion}.", manifest.IntegrationId, "Upgrade the tool before managing this integration."));
                return new MigrationResult(false, true, items);
            }

            bool applied = false;

            if (manifest.SchemaVersion != FaceMotionVersions.IntegrationManifestVersion)
            {
                manifest.SchemaVersion = FaceMotionVersions.IntegrationManifestVersion;
                applied = true;
            }

            if (!string.Equals(manifest.BackendId, ModularAvatarIntegrationBackend.BackendId, StringComparison.Ordinal))
            {
                manifest.BackendId = ModularAvatarIntegrationBackend.BackendId;
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
                if (!TryEvaluateState(manifest, operationAvatar, out IntegrationState state, out FaceMotionDiagnostic blocking))
                {
                    items.Add(blocking);
                    return new MigrationResult(applied, true, items);
                }

                manifest.State = state;
                applied = true;
            }

            if (applied)
            {
                items.Add(new FaceMotionDiagnostic(FaceMotionDiagnosticCodes.UpgradedSchema, FaceMotionDiagnosticSeverity.Info, "Integration manifest was upgraded to the current compatibility schema.", manifest.IntegrationId, false, string.Empty));
            }

            return new MigrationResult(applied, false, items);
        }

        /// <summary>
        /// Runtime-verified attach state for a Modular Avatar integration. Attached requires the
        /// full ownership check against the recorded hierarchy; Detached means the generated
        /// assets and manifest are retained but the integration object is gone.
        /// </summary>
        public static bool TryEvaluateState(
            ModularAvatarIntegrationManifest manifest,
            VRCAvatarDescriptor operationAvatar,
            out IntegrationState state,
            out FaceMotionDiagnostic blocking)
        {
            state = IntegrationState.Unknown;
            blocking = null;

            var owner = operationAvatar != null ? operationAvatar : ModularAvatarIntegrationBackend.ResolveAvatar(manifest);
            if (owner == null)
            {
                blocking = Error(FaceMotionDiagnosticCodes.IntegrationAmbiguous, "The integration manifest's avatar cannot be resolved.", manifest.IntegrationId, "Restore the avatar reference before continuing.");
                return false;
            }

            var integrationObject = ModularAvatarIntegrationBackend.ResolveIntegrationObject(manifest, owner);
            if (integrationObject != null)
            {
                state = IntegrationState.Attached;
                return true;
            }

            if (!string.IsNullOrEmpty(manifest.IntegrationObjectName) && owner.transform.Find(manifest.IntegrationObjectName) != null)
            {
                blocking = Error(FaceMotionDiagnosticCodes.IntegrationAmbiguous, "An object with the FaceMotion integration name exists but is not owned by the manifest.", manifest.IntegrationId, "Rename or remove that object before continuing.");
                return false;
            }

            state = IntegrationState.Detached;
            return true;
        }

        private static FaceMotionDiagnostic Error(string code, string message, string contextId, string fix)
        {
            return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Error, message, contextId, true, fix);
        }
    }
}