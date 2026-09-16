using System;
using System.Collections.Generic;
using System.IO;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.Export;
using FaceMotion.Editor.VRChat.Integration;
using FaceMotion.Serialization;
using FaceMotion.Versioning;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace FaceMotion.Editor.ModularAvatar
{
    /// <summary>Uses only MA's public 1.18.7 component APIs; MA performs the eventual avatar merge.</summary>
    public sealed class ModularAvatarIntegrationBackend : IModularAvatarIntegrationBackend
    {
        public const string BackendId = "modular-avatar";
        private const string Prefix = "FaceMotion MA ";
        private static readonly Dictionary<VRCAvatarDescriptor, ModularAvatarIntegrationManifest> SessionManifests = new Dictionary<VRCAvatarDescriptor, ModularAvatarIntegrationManifest>();

        public bool HasExistingIntegration(VRCAvatarDescriptor avatar)
        {
            return FindManifest(avatar) != null || FindRemovalCandidateManifest(avatar) != null;
        }

        public ModularAvatarIntegrationPlan Plan(ModularAvatarIntegrationRequest request)
        {
            var diagnostics = new List<FaceMotionDiagnostic>();
            if (request == null || request.Avatar == null) diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarAvatar, "Select a VRCAvatarDescriptor."));
            if (request != null && request.Avatar != null && EditorUtility.IsPersistent(request.Avatar)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarPrefabAsset, "Modular Avatar integration cannot modify a prefab asset.", "Instantiate the avatar in a scene before integrating."));
            if (request == null || request.Clip == null) diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarClip, "Select an AnimationClip before integrating."));
            if (request == null || !IsAssetFolder(request.OutputFolder)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarPath, "Output folder must be an existing folder under Assets."));
            var stem = Sanitize(request == null ? "FaceMotion" : request.DisplayName);
            var parameter = "FaceMotion_" + stem;
            if (parameter.Length > 256) diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarParameterName, "The generated parameter name exceeds VRChat's 256 character limit."));
            if (request != null && request.Avatar != null)
            {
                var existing = FindManifest(request.Avatar) ?? FindRemovalCandidateManifest(request.Avatar);
                if (existing != null && existing.ParameterName != parameter) diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarManifestConflict, "This avatar is already managed by FaceMotion Modular Avatar integration. Remove it before changing the animation name."));
                ValidateConflicts(request.Avatar, request.Clip, parameter, existing, diagnostics);
            }
            return new ModularAvatarIntegrationPlan(request, parameter, Prefix + stem, diagnostics);
        }

        public ModularAvatarIntegrationResult Apply(ModularAvatarIntegrationPlan plan)
        {
            if (plan == null || !plan.IsValid) return new ModularAvatarIntegrationResult(false, null, plan == null ? new[] { Error(FaceMotionDiagnosticCodes.ModularAvatarPlan, "No valid integration plan was supplied.") } : plan.Diagnostics);
            var avatar = plan.Request.Avatar;
            var existing = FindManifest(avatar) ?? FindRemovalCandidateManifest(avatar);
            if (existing != null)
            {
                var migration = ModularAvatarIntegrationManifestMigration.TryMigrateOnUse(existing, avatar);
                if (migration.Blocked)
                {
                    return new ModularAvatarIntegrationResult(false, null, migration.Diagnostics);
                }
            }

            string reuseRoot = null;
            var wasDetached = false;
            string carryIntegrationId = null;
            string carryAnimationId = null;
            string carryAvatarFingerprint = null;
            if (existing != null)
            {
                if (!MigrationIds.NeedsRepair(existing.IntegrationId)) carryIntegrationId = existing.IntegrationId;
                carryAnimationId = existing.AnimationId;
                carryAvatarFingerprint = existing.AvatarFingerprint;
                var manifestPath = AssetDatabase.GetAssetPath(existing);
                reuseRoot = Path.GetDirectoryName(manifestPath)?.Replace('\\', '/');
                var oldOwnedPaths = existing.OwnedAssetPaths ?? Array.Empty<string>();
                var integrationObjectName = existing.IntegrationObjectName;
                for (int i = 0; i < oldOwnedPaths.Length; i++)
                {
                    if (!GeneratedAssetOwnership.IsSafeOwnedAssetPath(oldOwnedPaths[i], reuseRoot) || !GeneratedAssetOwnership.IsCanonicalGeneratedFileName(Path.GetFileName(oldOwnedPaths[i])))
                        return new ModularAvatarIntegrationResult(false, null, new[] { Error("FM-OWNERSHIP-TAMPERED", "The integration manifest lists an asset that is not a FaceMotion-generated file inside the manifest folder.", "Inspect the manifest and restore the generated asset paths.") });
                }
                var integrationObject = ResolveIntegrationObject(existing, avatar);
                if (integrationObject != null)
                {
                    if (!RemoveInternal(existing, avatar, out var removeDiagnostics)) return new ModularAvatarIntegrationResult(false, existing, removeDiagnostics);
                    AssetDatabase.Refresh();
                }
                else
                {
                    wasDetached = true;
                    if (!string.IsNullOrEmpty(integrationObjectName) && avatar.transform.Find(integrationObjectName) != null)
                        return new ModularAvatarIntegrationResult(false, null, new[] { Error("FM-OWNERSHIP-TAMPERED", "An object with the FaceMotion integration name exists but is not owned by the manifest.", "Rename or remove that object before reapplying.") });
                    SessionManifests.Remove(avatar);
                }
                for (int i = 0; i < oldOwnedPaths.Length; i++)
                {
                    if (oldOwnedPaths[i] != manifestPath) AssetDatabase.DeleteAsset(oldOwnedPaths[i]);
                }
                AssetDatabase.DeleteAsset(manifestPath);
                AssetDatabase.SaveAssets();
            }
            var owned = new List<string>();
            string root;
            if (reuseRoot != null && AssetDatabase.IsValidFolder(reuseRoot))
            {
                root = reuseRoot;
            }
            else
            {
                var candidate = plan.Request.OutputFolder + "/FaceMotionMA_" + Sanitize(plan.Request.DisplayName);
                if (AssetDatabase.IsValidFolder(candidate)) candidate = AssetDatabase.GenerateUniqueAssetPath(candidate);
                root = candidate;
                if (!AssetDatabase.IsValidFolder(root) && string.IsNullOrEmpty(AssetDatabase.CreateFolder(plan.Request.OutputFolder, Path.GetFileName(root)))) throw new InvalidOperationException("Could not create the FaceMotion MA output folder.");
            }
            GameObject node = null;
            int undoGroup = -1;
            try
            {
                Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Apply FaceMotion Modular Avatar integration");
                if (!AssetDatabase.IsValidFolder(root))
                {
                    if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(plan.Request.OutputFolder, Path.GetFileName(root)))) throw new InvalidOperationException("Could not create the FaceMotion MA output folder.");
                }
                var controller = AnimatorController.CreateAnimatorControllerAtPath(root + "/FX.controller"); owned.Add(root + "/FX.controller");
                controller.AddParameter(plan.ParameterName, AnimatorControllerParameterType.Bool);
                var machine = new AnimatorStateMachine { name = plan.ObjectName }; AssetDatabase.AddObjectToAsset(machine, controller);
                var reset = ResetClipBuilder.Create(plan.Request.Avatar, plan.Request.Clip); reset.name = "Reset";
                AssetDatabase.CreateAsset(reset, root + "/Reset.anim"); owned.Add(root + "/Reset.anim");
                var off = machine.AddState("Off"); off.motion = reset; off.writeDefaultValues = false;
                var on = machine.AddState("On"); on.motion = plan.Request.Clip; on.writeDefaultValues = false;
                var toOn = off.AddTransition(on); toOn.hasExitTime = false; toOn.duration = 0f; toOn.AddCondition(AnimatorConditionMode.If, 0, plan.ParameterName);
                var toOff = on.AddTransition(off); toOff.hasExitTime = false; toOff.duration = 0f; toOff.AddCondition(AnimatorConditionMode.IfNot, 0, plan.ParameterName);
                controller.AddLayer(new AnimatorControllerLayer { name = plan.ObjectName, defaultWeight = 1, stateMachine = machine });
                var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>(); menu.name = plan.ObjectName;
                menu.controls = new List<VRCExpressionsMenu.Control> { new VRCExpressionsMenu.Control { name = "Play", type = VRCExpressionsMenu.Control.ControlType.Toggle, parameter = new VRCExpressionsMenu.Control.Parameter { name = plan.ParameterName }, value = 1 } };
                AssetDatabase.CreateAsset(menu, root + "/Menu.asset"); owned.Add(root + "/Menu.asset");
                node = new GameObject(plan.ObjectName); Undo.RegisterCreatedObjectUndo(node, "Apply FaceMotion Modular Avatar integration"); node.transform.SetParent(plan.Request.Avatar.transform, false);
                var merge = node.AddComponent<ModularAvatarMergeAnimator>(); merge.animator = controller; merge.layerType = VRCAvatarDescriptor.AnimLayerType.FX; merge.deleteAttachedAnimator = false; merge.pathMode = MergeAnimatorPathMode.Absolute; merge.matchAvatarWriteDefaults = false;
                var parameters = node.AddComponent<ModularAvatarParameters>(); parameters.parameters.Add(new ParameterConfig { nameOrPrefix = plan.ParameterName, syncType = ParameterSyncType.Bool, saved = false, defaultValue = 0 });
                var installer = node.AddComponent<ModularAvatarMenuInstaller>(); installer.menuToAppend = menu; installer.installTargetMenu = null;
                var manifest = ScriptableObject.CreateInstance<ModularAvatarIntegrationManifest>(); manifest.name = "FaceMotion MA Manifest"; manifest.Avatar = plan.Request.Avatar; manifest.AvatarGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(plan.Request.Avatar).ToString(); manifest.IntegrationObjectName = node.name; manifest.IntegrationObjectGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(node).ToString(); manifest.ParameterName = plan.ParameterName; manifest.OwnedAssetPaths = owned.ToArray();
                manifest.SchemaVersion = FaceMotionVersions.IntegrationManifestVersion;
                manifest.BackendId = ModularAvatarIntegrationBackend.BackendId;
                manifest.IntegrationId = string.IsNullOrEmpty(carryIntegrationId) ? StableId.New() : carryIntegrationId;
                manifest.State = IntegrationState.Attached;
                manifest.AnimationId = carryAnimationId ?? string.Empty;
                manifest.AvatarFingerprint = carryAvatarFingerprint ?? string.Empty;
                AssetDatabase.CreateAsset(manifest, root + "/Manifest.asset");
                SessionManifests[plan.Request.Avatar] = manifest;
                EditorUtility.SetDirty(node); AssetDatabase.SaveAssets(); Undo.CollapseUndoOperations(undoGroup);
                var applied = wasDetached ? Info(FaceMotionDiagnosticCodes.ModularAvatarDetached, "A detached integration was reconnected using its retained generated assets.") : Info(FaceMotionDiagnosticCodes.ModularAvatarApplied, "Modular Avatar merge animator, parameters, and menu installer were created.");
                return new ModularAvatarIntegrationResult(true, manifest, new[] { applied });
            }
            catch (Exception exception)
            {
                if (node != null) Undo.DestroyObjectImmediate(node);
                if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup);
                for (var i = owned.Count - 1; i >= 0; i--) AssetDatabase.DeleteAsset(owned[i]);
                GeneratedAssetOwnership.TryDeleteEmptyOwnedFolder(root, owned);
                return new ModularAvatarIntegrationResult(false, null, new[] { Error(FaceMotionDiagnosticCodes.ModularAvatarApply, exception.Message) });
            }
        }

        public ModularAvatarIntegrationResult Remove(VRCAvatarDescriptor avatar)
        {
            var manifest = FindManifest(avatar) ?? FindRemovalCandidateManifest(avatar);
            if (manifest == null) return new ModularAvatarIntegrationResult(false, null, new[] { Error(FaceMotionDiagnosticCodes.ModularAvatarManifest, "No FaceMotion Modular Avatar manifest was found on this avatar.") });
            return RemoveInternal(manifest, avatar, out var diagnostics) ? new ModularAvatarIntegrationResult(true, null, diagnostics) : new ModularAvatarIntegrationResult(false, manifest, diagnostics);
        }

        private static bool RemoveInternal(ModularAvatarIntegrationManifest manifest, VRCAvatarDescriptor owner, out IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        {
            var items = new List<FaceMotionDiagnostic>();
            if (manifest == null) { items.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarManifest, "The Modular Avatar manifest is missing.")); diagnostics = items; return false; }
            var migration = ModularAvatarIntegrationManifestMigration.TryMigrateOnUse(manifest, owner, items);
            if (migration.Blocked) { diagnostics = items; return false; }
            owner = owner ?? ResolveAvatar(manifest);
            var integrationObject = ResolveIntegrationObject(manifest, owner);
if (integrationObject == null)
            {
                items.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarOwnership, "連携オブジェクトの所有権を確認できないため削除しませんでした。", "同じアバター直下のFaceMotion連携オブジェクトと生成アセットを確認してください。"));
                diagnostics = items;
                return false;
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remove FaceMotion Modular Avatar integration");
            Undo.DestroyObjectImmediate(integrationObject);
            if (owner != null) SessionManifests.Remove(owner);
            Undo.CollapseUndoOperations(undoGroup); items.Add(Info(FaceMotionDiagnosticCodes.ModularAvatarRemoved, "FaceMotion-owned Modular Avatar hierarchy was removed. Generated assets and manifest were retained.")); diagnostics = items; return true;
        }

        private static void ValidateConflicts(VRCAvatarDescriptor avatar, AnimationClip clip, string parameter, ModularAvatarIntegrationManifest owned, List<FaceMotionDiagnostic> diagnostics)
        {
            foreach (var parameters in avatar.GetComponentsInChildren<ModularAvatarParameters>(true))
                if (!IsOwned(parameters.gameObject, owned, avatar))
                    if (parameters.parameters != null) foreach (var config in parameters.parameters) if (!config.isPrefix && config.nameOrPrefix == parameter) diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarParameterConflict, "An MA Parameters component already defines the generated parameter."));
            foreach (var merge in avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true))
                if (!IsOwned(merge.gameObject, owned, avatar) && merge.animator != null)
                    foreach (var candidate in merge.animator.animationClips) if (candidate != null && SharesBinding(candidate, clip)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarBindingConflict, "The clip shares an animated binding with another MA merge animator."));
            var fx = GetFx(avatar); if (fx != null) foreach (var candidate in fx.animationClips) if (candidate != null && SharesBinding(candidate, clip)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarCrossBindingConflict, "The clip shares an animated binding with the avatar FX controller."));
        }
        private static ModularAvatarIntegrationManifest FindManifest(VRCAvatarDescriptor avatar) { if (avatar == null) return null; if (SessionManifests.TryGetValue(avatar, out var session) && session != null && ResolveIntegrationObject(session, avatar) != null) return session; foreach (var guid in AssetDatabase.FindAssets("t:ModularAvatarIntegrationManifest")) { var manifest = AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(AssetDatabase.GUIDToAssetPath(guid)); if (manifest != null && ResolveIntegrationObject(manifest, avatar) != null) return manifest; } return null; }
        private static ModularAvatarIntegrationManifest FindRemovalCandidateManifest(VRCAvatarDescriptor avatar) { if (avatar == null) return null; if (SessionManifests.TryGetValue(avatar, out var session) && session != null) return session; foreach (var guid in AssetDatabase.FindAssets("t:ModularAvatarIntegrationManifest")) { var manifest = AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(AssetDatabase.GUIDToAssetPath(guid)); if (manifest != null && (ResolveAvatar(manifest) == avatar || (!string.IsNullOrEmpty(manifest.IntegrationObjectName) && avatar.transform.Find(manifest.IntegrationObjectName) != null))) return manifest; } return null; }
        internal static VRCAvatarDescriptor ResolveAvatar(ModularAvatarIntegrationManifest manifest) { if (manifest == null) return null; if (manifest.Avatar != null) return manifest.Avatar; return GlobalObjectId.TryParse(manifest.AvatarGlobalId, out var id) ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as VRCAvatarDescriptor : null; }
        internal static GameObject ResolveIntegrationObject(ModularAvatarIntegrationManifest manifest, VRCAvatarDescriptor owner)
        {
            if (manifest == null || owner == null) return null;
            if (!string.IsNullOrEmpty(manifest.IntegrationObjectGlobalId) && GlobalObjectId.TryParse(manifest.IntegrationObjectGlobalId, out var id))
            {
                var resolved = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;
                if (IsOwnedIntegration(resolved, manifest, owner)) return resolved;
            }
            var legacy = owner.transform.Find(manifest.IntegrationObjectName)?.gameObject;
            return IsOwnedIntegration(legacy, manifest, owner) ? legacy : null;
        }
        private static bool IsOwned(GameObject value, ModularAvatarIntegrationManifest manifest, VRCAvatarDescriptor owner) { return value != null && value == ResolveIntegrationObject(manifest, owner); }
        private static bool IsOwnedIntegration(GameObject value, ModularAvatarIntegrationManifest manifest, VRCAvatarDescriptor owner)
        {
            if (value == null || manifest == null || owner == null || value.transform.parent != owner.transform) return false;
            var merge = value.GetComponent<ModularAvatarMergeAnimator>();
            var parameters = value.GetComponent<ModularAvatarParameters>();
            var installer = value.GetComponent<ModularAvatarMenuInstaller>();
            if (merge == null || parameters == null || installer == null || merge.animator == null || installer.menuToAppend == null) return false;
            if (!IsOwnedAsset(manifest, AssetDatabase.GetAssetPath(merge.animator)) || !IsOwnedAsset(manifest, AssetDatabase.GetAssetPath(installer.menuToAppend))) return false;
            if (parameters.parameters == null || !parameters.parameters.Exists(p => !p.isPrefix && p.nameOrPrefix == manifest.ParameterName && p.syncType == ParameterSyncType.Bool)) return false;
            if (installer.menuToAppend.controls == null || !installer.menuToAppend.controls.Exists(c => c.type == VRCExpressionsMenu.Control.ControlType.Toggle && c.parameter != null && c.parameter.name == manifest.ParameterName)) return false;
            var controller = merge.animator as AnimatorController;
            return controller == null || Array.Exists(controller.parameters, p => p.name == manifest.ParameterName && p.type == AnimatorControllerParameterType.Bool);
        }
        private static bool IsOwnedAsset(ModularAvatarIntegrationManifest manifest, string path) { if (manifest == null || string.IsNullOrEmpty(path) || manifest.OwnedAssetPaths == null) return false; foreach (var owned in manifest.OwnedAssetPaths) if (string.Equals(owned, path, StringComparison.Ordinal)) return true; return false; }
        private static bool IsOwnedAssetPath(string path, string root)
        {
            path = NormalizeAssetPath(path);
            root = NormalizeAssetPath(root);
            return path != null && root != null && path.StartsWith(root + "/", StringComparison.Ordinal);
        }
        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            path = path.Replace('\\', '/');
            var parts = path.Split('/');
            for (var i = 0; i < parts.Length; i++) if (string.IsNullOrEmpty(parts[i]) || parts[i] == "." || parts[i] == "..") return null;
            return path;
        }
        private static RuntimeAnimatorController GetFx(VRCAvatarDescriptor avatar) { if (avatar == null || avatar.baseAnimationLayers == null) return null; foreach (var layer in avatar.baseAnimationLayers) if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX) return layer.animatorController; return null; }
        private static bool SharesBinding(AnimationClip a, AnimationClip b) { var set = new HashSet<EditorCurveBinding>(AnimationUtility.GetCurveBindings(a)); foreach (var binding in AnimationUtility.GetCurveBindings(b)) if (set.Contains(binding)) return true; return false; }

        private static bool IsAssetFolder(string path) { return !string.IsNullOrEmpty(path) && (path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal)) && AssetDatabase.IsValidFolder(path) && !path.Contains(".."); }
        private static string Sanitize(string value) { var c = (value ?? "FaceMotion").ToCharArray(); for (var i = 0; i < c.Length; i++) if (!char.IsLetterOrDigit(c[i]) && c[i] != '_') c[i] = '_'; return new string(c); }
        private static FaceMotionDiagnostic Error(string code, string message, string fix = "Resolve the conflict or use Direct integration.") { return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Error, message, "modular-avatar", true, fix); }
        private static FaceMotionDiagnostic Info(string code, string message) { return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Info, message, "modular-avatar", false, string.Empty); }
    }
}
