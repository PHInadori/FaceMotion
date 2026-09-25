using System;
using System.Collections.Generic;
using System.IO;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.Avatar;
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
    public sealed class ModularAvatarIntegrationBackend : IModularAvatarIntegrationBackend, IModularAvatarIntegrationRollbackBackend, IModularAvatarDesiredStateBackend
    {
        public const string BackendId = "modular-avatar";
        private const string Prefix = "FaceMotion MA ";
        private static readonly Dictionary<VRCAvatarDescriptor, ModularAvatarIntegrationManifest> SessionManifests = new Dictionary<VRCAvatarDescriptor, ModularAvatarIntegrationManifest>();
        internal static Action<string> PlanFailureInjector;
        internal static Action<string> ApplyFailureInjector;
        internal static Action<string> RollbackFailureInjector;

        public bool HasExistingIntegration(VRCAvatarDescriptor avatar)
        {
            return FindManifest(avatar) != null || FindRemovalCandidateManifest(avatar) != null;
        }

        public bool HasExistingIntegration(VRCAvatarDescriptor avatar, string parameter)
        {
            return FindManifest(avatar, parameter) != null || FindRemovalCandidateManifest(avatar, parameter) != null;
        }

        public ModularAvatarIntegrationPlan Plan(ModularAvatarIntegrationRequest request)
        {
            try
            {
                PlanFailureInjector?.Invoke("before-plan");
                return PlanCore(request);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return new ModularAvatarIntegrationPlan(
                    request,
                    string.Empty,
                    string.Empty,
                    new[] { UnexpectedPlanDiagnostic(exception, request) });
            }
        }

        public ModularAvatarIntegrationBatchPlan PlanBatch(IReadOnlyList<ModularAvatarIntegrationRequest> requests)
        {
            return PlanBatchCore(requests, null);
        }

        public ModularAvatarIntegrationBatchPlan PlanFinalState(IReadOnlyList<ModularAvatarIntegrationRequest> requests, IReadOnlyList<string> removingParameters)
        {
            return PlanBatchCore(requests, removingParameters == null ? null : new HashSet<string>(removingParameters, StringComparer.Ordinal));
        }

        private ModularAvatarIntegrationBatchPlan PlanBatchCore(IReadOnlyList<ModularAvatarIntegrationRequest> requests, HashSet<string> removingParameters)
        {
            var plans = new List<ModularAvatarIntegrationPlan>();
            if (requests == null || requests.Count == 0) return new ModularAvatarIntegrationBatchPlan(plans);
            var reservedParameters = new HashSet<string>(StringComparer.Ordinal);
            var reservedObjects = new HashSet<string>(StringComparer.Ordinal);
            var reservedRoots = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<BatchEntry>();
            var planned = new List<(ModularAvatarIntegrationRequest request, string parameter, string objectName, string rootName)>();
            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                var stem = Sanitize(request == null ? "FaceMotion" : request.DisplayName);
                var suffix = 1;
                string parameter;
                string objectName;
                string rootName;
                do
                {
                    var discriminator = suffix == 1 ? string.Empty : "_" + suffix;
                    parameter = "FaceMotion_" + stem + discriminator;
                    objectName = Prefix + stem + discriminator;
                    rootName = "FaceMotionMA_" + stem + discriminator;
                    suffix++;
                }
                while (reservedParameters.Contains(parameter) || reservedObjects.Contains(objectName) || reservedRoots.Contains(rootName) || RootIsOccupiedByOtherIntegration(request == null ? null : request.Avatar, request == null ? null : request.OutputFolder, parameter, rootName, removingParameters));
                reservedParameters.Add(parameter); reservedObjects.Add(objectName); reservedRoots.Add(rootName);
                planned.Add((request, parameter, objectName, rootName));
                entries.Add(new BatchEntry(request, parameter));
            }
            for (var i = 0; i < planned.Count; i++)
            {
                var item = planned[i];
                plans.Add(PlanCore(item.request, item.parameter, item.objectName, item.rootName, true, removingParameters, entries));
            }
            return new ModularAvatarIntegrationBatchPlan(plans);
        }

        public ModularAvatarIntegrationBatchResult ApplyBatch(ModularAvatarIntegrationBatchPlan plan)
        {
            var results = new List<ModularAvatarIntegrationResult>();
            var diagnostics = new List<FaceMotionDiagnostic>();
            if (plan == null || !plan.IsValid) return new ModularAvatarIntegrationBatchResult(false, results, diagnostics);
            var created = new List<ModularAvatarIntegrationManifest>();
            VRCAvatarDescriptor batchAvatar = null;
            try
            {
                InsideBatchApply = true;
                for (var i = 0; i < plan.Items.Count; i++)
                {
                    var item = plan.Items[i];
                    batchAvatar = item.Request == null ? batchAvatar : item.Request.Avatar;
                    var existing = FindManifest(item.Request.Avatar, item.ParameterName) ?? FindRemovalCandidateManifest(item.Request.Avatar, item.ParameterName);
                    // A matching owned item is already the desired batch state. Do not reapply it:
                    // later-item failure must never replace an earlier existing integration.
                    var result = existing == null
                        ? Apply(item)
                        : new ModularAvatarIntegrationResult(true, existing, new[] { Info(FaceMotionDiagnosticCodes.ModularAvatarApplied, "Existing Modular Avatar batch item is already applied.") });
                    results.Add(result);
                    for (var d = 0; d < result.Diagnostics.Count; d++) diagnostics.Add(result.Diagnostics[d]);
                    if (!result.Succeeded) throw new InvalidOperationException("A Modular Avatar batch item could not be applied.");
                    if (existing == null && result.Manifest is ModularAvatarIntegrationManifest manifest) created.Add(manifest);
                    ApplyFailureInjector?.Invoke("after-batch-item-" + i);
                }
            }
            catch (Exception exception)
            {
                InsideBatchApply = false;
                for (var i = created.Count - 1; i >= 0; i--) RollbackCreated(created[i]);
                diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarApply, exception.Message));
                return new ModularAvatarIntegrationBatchResult(false, results, diagnostics);
            }
            InsideBatchApply = false;
            var regenDiagnostics = RegenerateStalePartnerMachines(batchAvatar);
            var regenFailed = false;
            for (var r = 0; r < regenDiagnostics.Count; r++)
            {
                diagnostics.Add(regenDiagnostics[r]);
                if (regenDiagnostics[r] != null && regenDiagnostics[r].Blocking) regenFailed = true;
            }
            if (regenFailed) return new ModularAvatarIntegrationBatchResult(false, results, diagnostics);
            return new ModularAvatarIntegrationBatchResult(true, results, diagnostics);
        }

        public ModularAvatarManagedStateSnapshot InspectManagedState(VRCAvatarDescriptor avatar)
        {
            var items = new List<ModularAvatarManagedState>();
            var diagnostics = new List<FaceMotionDiagnostic>();
            foreach (var manifest in FindManagedManifests(avatar))
            {
                if (!ValidateManagedState(manifest, avatar, diagnostics)) continue;
                items.Add(new ModularAvatarManagedState(manifest.AnimationId, manifest.ParameterName));
            }
            return new ModularAvatarManagedStateSnapshot(items, diagnostics);
        }

        public ModularAvatarManagedStateSnapshot ValidateRemovals(VRCAvatarDescriptor avatar, IReadOnlyList<string> parameters)
        {
            var snapshot = InspectManagedState(avatar);
            if (!snapshot.IsValid || parameters == null || parameters.Count == 0) return snapshot;
            var diagnostics = new List<FaceMotionDiagnostic>(snapshot.Diagnostics);
            var requested = new HashSet<string>(parameters, StringComparer.Ordinal);
            for (var i = 0; i < snapshot.Items.Count; i++) requested.Remove(snapshot.Items[i].ParameterName);
            foreach (var parameter in requested) diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarOwnership, "The requested FaceMotion Modular Avatar integration could not be proven owned: " + parameter));
            return new ModularAvatarManagedStateSnapshot(snapshot.Items, diagnostics);
        }

        private static ModularAvatarIntegrationPlan PlanCore(ModularAvatarIntegrationRequest request)
        {
            var stem = Sanitize(request == null ? "FaceMotion" : request.DisplayName);
            return PlanCore(request, "FaceMotion_" + stem, Prefix + stem, "FaceMotionMA_" + stem, false, null, null);
        }

        private static ModularAvatarIntegrationPlan PlanCore(ModularAvatarIntegrationRequest request, string parameter, string objectName, string rootName, bool allowOtherManifests, HashSet<string> removingParameters, IReadOnlyList<BatchEntry> batchEntries = null)
        {
            var diagnostics = new List<FaceMotionDiagnostic>();
            if (request == null || request.Avatar == null) diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarAvatar, "Select a VRCAvatarDescriptor."));
            if (request != null && request.Avatar != null && EditorUtility.IsPersistent(request.Avatar)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarPrefabAsset, "Modular Avatar integration cannot modify a prefab asset.", "Instantiate the avatar in a scene before integrating."));
            if (request == null || request.Clip == null) diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarClip, "Select an AnimationClip before integrating."));
            if (request == null || !IsAssetFolder(request.OutputFolder)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarPath, "Output folder must be an existing folder under Assets."));
            if (parameter.Length > 256) diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarParameterName, "The generated parameter name exceeds VRChat's 256 character limit."));
            var partnerParameters = new List<string>();
            var sharedBindings = new HashSet<EditorCurveBinding>();
            if (request != null && request.Avatar != null)
            {
                var existing = FindManifest(request.Avatar, parameter) ?? FindRemovalCandidateManifest(request.Avatar, parameter);
                if (!allowOtherManifests && existing == null && (FindManifest(request.Avatar) != null || FindRemovalCandidateManifest(request.Avatar) != null)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarManifestConflict, "This avatar is already managed by FaceMotion Modular Avatar integration. Remove it before changing the animation name."));
                if (request.Clip != null)
                {
                    ValidateAmbiguousClipBindings(request.Avatar, request.Clip, diagnostics);
                    ValidateConflicts(request.Avatar, request.Clip, parameter, existing, removingParameters, diagnostics, partnerParameters, sharedBindings);
                    CollectBatchPartners(request.Clip, parameter, batchEntries, partnerParameters, sharedBindings);
                }
            }
            return new ModularAvatarIntegrationPlan(request, parameter, objectName, rootName, diagnostics,
                partnerParameters, new List<EditorCurveBinding>(sharedBindings));
        }

        /// <summary>One planned batch item; batch siblings that share bindings become partner-aware machines.</summary>
        private readonly struct BatchEntry
        {
            public BatchEntry(ModularAvatarIntegrationRequest request, string parameter) { Request = request; Parameter = parameter; }
            public ModularAvatarIntegrationRequest Request { get; }
            public string Parameter { get; }
        }

        private static void CollectBatchPartners(AnimationClip clip, string parameter, IReadOnlyList<BatchEntry> batchEntries, List<string> partnerParameters, HashSet<EditorCurveBinding> sharedBindings)
        {
            if (batchEntries == null || clip == null) return;
            for (var i = 0; i < batchEntries.Count; i++)
            {
                var entry = batchEntries[i];
                if (entry.Request == null || entry.Request.Clip == null || string.Equals(entry.Parameter, parameter, StringComparison.Ordinal)) continue;
                var overlaps = false;
                foreach (var binding in SharedBindings(entry.Request.Clip, clip)) { sharedBindings.Add(binding); overlaps = true; }
                if (overlaps && !partnerParameters.Contains(entry.Parameter)) partnerParameters.Add(entry.Parameter);
            }
        }

        private static FaceMotionDiagnostic UnexpectedPlanDiagnostic(Exception exception, ModularAvatarIntegrationRequest request)
        {
            string contextId = BackendId + "/plan" + (request?.Avatar == null ? string.Empty : "/" + request.Avatar.name);
            string detail = "Exception: " + exception.GetType().FullName
                + "\nMessage: " + exception.Message
                + "\nStack trace:\n" + (exception.StackTrace ?? "(no stack trace)")
                + "\nDiagnostic code: " + FaceMotionDiagnosticCodes.ModularAvatarPlanUnexpected
                + "\nBackend: " + BackendId
                + "\nOperation: Plan and Validate Integration"
                + "\nContextId: " + contextId;
            return new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.ModularAvatarPlanUnexpected,
                FaceMotionDiagnosticSeverity.Error,
                detail,
                contextId,
                true,
                "Review the integration settings and retry. If this repeats, report the diagnostic code and technical details.");
        }

        public ModularAvatarIntegrationResult Apply(ModularAvatarIntegrationPlan plan)
        {
            if (plan == null || !plan.IsValid) return new ModularAvatarIntegrationResult(false, null, plan == null ? new[] { Error(FaceMotionDiagnosticCodes.ModularAvatarPlan, "No valid integration plan was supplied.") } : plan.Diagnostics);
            var avatar = plan.Request.Avatar;
            var existing = FindManifest(avatar, plan.ParameterName) ?? FindRemovalCandidateManifest(avatar, plan.ParameterName);
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
                var candidate = plan.Request.OutputFolder + "/" + plan.RootName;
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
                var partners = plan.PartnerParameters ?? Array.Empty<string>();
                for (var p = 0; p < partners.Count; p++)
                    if (!string.IsNullOrEmpty(partners[p]) && !string.Equals(partners[p], plan.ParameterName, StringComparison.Ordinal) && !HasParameter(controller, partners[p]))
                        controller.AddParameter(partners[p], AnimatorControllerParameterType.Bool);
                var machine = new AnimatorStateMachine { name = plan.ObjectName }; AssetDatabase.AddObjectToAsset(machine, controller);
                var reset = ResetClipBuilder.Create(plan.Request.Avatar, plan.Request.Clip); reset.name = "Reset";
                AssetDatabase.CreateAsset(reset, root + "/Reset.anim"); owned.Add(root + "/Reset.anim");
                if (partners.Count == 0)
                {
                    var off = machine.AddState("Off"); off.motion = reset; off.writeDefaultValues = false;
                    var on = machine.AddState("On"); on.motion = plan.Request.Clip; on.writeDefaultValues = false;
                    var toOn = off.AddTransition(on); toOn.hasExitTime = false; toOn.duration = 0f; toOn.AddCondition(AnimatorConditionMode.If, 0, plan.ParameterName);
                    var toOff = on.AddTransition(off); toOff.hasExitTime = false; toOff.duration = 0f; toOff.AddCondition(AnimatorConditionMode.IfNot, 0, plan.ParameterName);
                }
                else
                {
                    var resetUnique = UnityEngine.Object.Instantiate(reset);
                    resetUnique.name = "Reset Unique";
                    var shared = plan.SharedBindings ?? Array.Empty<EditorCurveBinding>();
                    for (var s = 0; s < shared.Count; s++) AnimationUtility.SetEditorCurve(resetUnique, shared[s], null);
                    var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(resetUnique);
                    for (var s = 0; s < objectBindings.Length; s++)
                        for (var t = 0; t < shared.Count; t++)
                            if (objectBindings[s].Equals(shared[t])) { AnimationUtility.SetObjectReferenceCurve(resetUnique, objectBindings[s], null); break; }
                    AssetDatabase.CreateAsset(resetUnique, root + "/ResetUnique.anim"); owned.Add(root + "/ResetUnique.anim");

                    var offUnique = machine.AddState("OffUnique"); offUnique.motion = resetUnique; offUnique.writeDefaultValues = false;
                    var offAll = machine.AddState("OffAll"); offAll.motion = reset; offAll.writeDefaultValues = false;
                    var on = machine.AddState("On"); on.motion = plan.Request.Clip; on.writeDefaultValues = false;
                    machine.defaultState = offUnique;

                    var allToOn = offAll.AddTransition(on); allToOn.hasExitTime = false; allToOn.duration = 0f; allToOn.AddCondition(AnimatorConditionMode.If, 0, plan.ParameterName);
                    var uniqueToOn = offUnique.AddTransition(on); uniqueToOn.hasExitTime = false; uniqueToOn.duration = 0f; uniqueToOn.AddCondition(AnimatorConditionMode.If, 0, plan.ParameterName);
                    var onToAll = on.AddTransition(offAll); onToAll.hasExitTime = false; onToAll.duration = 0f;
                    onToAll.AddCondition(AnimatorConditionMode.IfNot, 0, plan.ParameterName);
                    for (var p = 0; p < partners.Count; p++) onToAll.AddCondition(AnimatorConditionMode.IfNot, 0, partners[p]);
                    for (var p = 0; p < partners.Count; p++)
                    {
                        var onToUnique = on.AddTransition(offUnique); onToUnique.hasExitTime = false; onToUnique.duration = 0f;
                        onToUnique.AddCondition(AnimatorConditionMode.IfNot, 0, plan.ParameterName);
                        onToUnique.AddCondition(AnimatorConditionMode.If, 0, partners[p]);
                        var allToUnique = offAll.AddTransition(offUnique); allToUnique.hasExitTime = false; allToUnique.duration = 0f;
                        allToUnique.AddCondition(AnimatorConditionMode.If, 0, partners[p]);
                    }
                    var uniqueToAll = offUnique.AddTransition(offAll); uniqueToAll.hasExitTime = false; uniqueToAll.duration = 0f;
                    for (var p = 0; p < partners.Count; p++) uniqueToAll.AddCondition(AnimatorConditionMode.IfNot, 0, partners[p]);
                }
                controller.AddLayer(new AnimatorControllerLayer { name = plan.ObjectName, defaultWeight = 1, stateMachine = machine });
                var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>(); menu.name = plan.ObjectName;
                menu.controls = new List<VRCExpressionsMenu.Control> { new VRCExpressionsMenu.Control { name = MenuLabel(plan), type = VRCExpressionsMenu.Control.ControlType.Toggle, parameter = new VRCExpressionsMenu.Control.Parameter { name = plan.ParameterName }, value = 1 } };
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
                manifest.AnimationId = !string.IsNullOrEmpty(plan.Request.AnimationId)
                    ? plan.Request.AnimationId
                    : carryAnimationId ?? string.Empty;
                manifest.AvatarFingerprint = carryAvatarFingerprint ?? string.Empty;
                AssetDatabase.CreateAsset(manifest, root + "/Manifest.asset");
                SessionManifests[plan.Request.Avatar] = manifest;
                EditorUtility.SetDirty(node); AssetDatabase.SaveAssets(); Undo.CollapseUndoOperations(undoGroup);
                ApplyFailureInjector?.Invoke("after-create");
                var applied = wasDetached ? Info(FaceMotionDiagnosticCodes.ModularAvatarDetached, "A detached integration was reconnected using its retained generated assets.") : Info(FaceMotionDiagnosticCodes.ModularAvatarApplied, "Modular Avatar merge animator, parameters, and menu installer were created.");
                var outcome = new List<FaceMotionDiagnostic> { applied };
                if (!InsideBatchApply)
                {
                    var regen = RegenerateStalePartnerMachines(avatar);
                    for (var r = 0; r < regen.Count; r++) outcome.Add(regen[r]);
                }
                return new ModularAvatarIntegrationResult(true, manifest, outcome);
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

        /// <summary>Removes every FaceMotion Modular Avatar integration (hierarchy, owned assets, manifest) from the avatar.</summary>
        public ModularAvatarIntegrationResult Remove(VRCAvatarDescriptor avatar)
        {
            var parameters = new List<string>();
            foreach (var manifest in FindManagedManifests(avatar))
            {
                if (!string.IsNullOrEmpty(manifest.ParameterName) && !parameters.Contains(manifest.ParameterName)) parameters.Add(manifest.ParameterName);
            }
            var batch = RemoveAnimations(avatar, parameters);
            return new ModularAvatarIntegrationResult(batch.Succeeded, null, batch.Diagnostics);
        }

        /// <summary>
        /// Removes the FaceMotion Modular Avatar integration whose generated parameter matches.
        /// Idempotent: an absent integration is a successful no-op, never an error, so re-adding
        /// a different animation after a full removal never leaves a stale managed state.
        /// </summary>
        public ModularAvatarIntegrationResult RemoveAnimation(VRCAvatarDescriptor avatar, string parameter)
        {
            if (avatar == null || string.IsNullOrEmpty(parameter))
            {
                return new ModularAvatarIntegrationResult(true, null, new[] { Info(FaceMotionDiagnosticCodes.ModularAvatarNothingToRemove, "No FaceMotion Modular Avatar integration matches the requested parameter.") });
            }
            var manifest = FindRemovalCandidateManifest(avatar, parameter);
            if (manifest == null)
            {
                return new ModularAvatarIntegrationResult(true, null, new[] { Info(FaceMotionDiagnosticCodes.ModularAvatarNothingToRemove, "No FaceMotion Modular Avatar integration matches the requested parameter.") });
            }
            if (!RemoveManaged(manifest, avatar, out var removeDiagnostics))
            {
                return new ModularAvatarIntegrationResult(false, manifest, removeDiagnostics);
            }
            var outcome = new List<FaceMotionDiagnostic>(removeDiagnostics);
            var regen = RegenerateStalePartnerMachines(avatar);
            for (var r = 0; r < regen.Count; r++) outcome.Add(regen[r]);
            return new ModularAvatarIntegrationResult(true, null, outcome);
        }

        /// <summary>Removes the FaceMotion Modular Avatar integrations for the given generated parameters. Idempotent per parameter.</summary>
        public ModularAvatarIntegrationBatchResult RemoveAnimations(VRCAvatarDescriptor avatar, IReadOnlyList<string> parameters)
        {
            var results = new List<ModularAvatarIntegrationResult>();
            var diagnostics = new List<FaceMotionDiagnostic>();
            if (avatar == null || parameters == null || parameters.Count == 0)
            {
                var empty = new[] { Info(FaceMotionDiagnosticCodes.ModularAvatarNothingToRemove, "No FaceMotion Modular Avatar integrations are selected for removal.") };
                results.Add(new ModularAvatarIntegrationResult(true, null, empty));
                return new ModularAvatarIntegrationBatchResult(true, results, empty);
            }
            var succeeded = true;
            for (var i = 0; i < parameters.Count; i++)
            {
                var result = RemoveAnimation(avatar, parameters[i]);
                results.Add(result);
                for (var d = 0; d < result.Diagnostics.Count; d++) diagnostics.Add(result.Diagnostics[d]);
                if (!result.Succeeded) succeeded = false;
            }
            return new ModularAvatarIntegrationBatchResult(succeeded, results, diagnostics);
        }

        private static bool RegenerationInProgress;
        private static bool InsideBatchApply;

        /// <summary>
        /// Rebuilds managed integrations whose generated partner-aware state machines no longer match
        /// the current binding-partner graph (a shared partner was added or removed in the same
        /// transaction). Idempotent: controllers whose parameter set already matches the expected
        /// partner set are never touched. Blocking diagnostics mean a regeneration could not be applied.
        /// </summary>
        public IReadOnlyList<FaceMotionDiagnostic> RegenerateStalePartnerMachines(VRCAvatarDescriptor avatar)
        {
            var diagnostics = new List<FaceMotionDiagnostic>();
            if (avatar == null || RegenerationInProgress) return diagnostics;
            RegenerationInProgress = true;
            try
            {
                var manifests = FindManagedManifests(avatar);
                for (var m = 0; m < manifests.Count; m++)
                {
                    var manifest = manifests[m];
                    if (manifest == null) continue;
                    var node = ResolveIntegrationObject(manifest, avatar);
                    if (node == null) continue;
                    var merge = node.GetComponent<ModularAvatarMergeAnimator>();
                    var controller = merge == null ? null : merge.animator as AnimatorController;
                    if (controller == null) continue;
                    var clip = ClipOfController(controller);
                    if (clip == null) continue;
                    var expected = new List<string>();
                    ComputeExpectedPartners(avatar, clip, manifest.ParameterName, expected, new HashSet<EditorCurveBinding>());
                    var actual = new List<string>();
                    var parameters = controller.parameters;
                    for (var p = 0; p < parameters.Length; p++)
                        if (parameters[p] != null && !string.IsNullOrEmpty(parameters[p].name) && !string.Equals(parameters[p].name, manifest.ParameterName, StringComparison.Ordinal))
                            actual.Add(parameters[p].name);
                    if (SameSet(expected, actual)) continue;
                    var manifestPath = AssetDatabase.GetAssetPath(manifest);
                    var root = string.IsNullOrEmpty(manifestPath) ? null : Path.GetDirectoryName(manifestPath)?.Replace('\\', '/');
                    if (string.IsNullOrEmpty(root) || !AssetDatabase.IsValidFolder(root)) continue;
                    var parentFolder = Path.GetDirectoryName(root)?.Replace('\\', '/');
                    var displayName = !string.IsNullOrEmpty(manifest.IntegrationObjectName) && manifest.IntegrationObjectName.StartsWith(Prefix, StringComparison.Ordinal)
                        ? manifest.IntegrationObjectName.Substring(Prefix.Length)
                        : manifest.ParameterName;
                    var request = new ModularAvatarIntegrationRequest(avatar, clip, parentFolder, displayName, manifest.AnimationId);
                    var plan = PlanCore(request, manifest.ParameterName, node.name, Path.GetFileName(root), true, null, null);
                    if (!plan.IsValid)
                    {
                        for (var d = 0; d < plan.Diagnostics.Count; d++) diagnostics.Add(plan.Diagnostics[d]);
                        continue;
                    }
                    var result = Apply(plan);
                    for (var d = 0; d < result.Diagnostics.Count; d++) diagnostics.Add(result.Diagnostics[d]);
                    if (!result.Succeeded) diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarApply, "Partner-aware regeneration failed for \"" + manifest.ParameterName + "\"."));
                }
            }
            finally
            {
                RegenerationInProgress = false;
            }
            return diagnostics;
        }

        /// <summary>Current partner parameters for one clip: other managed merges plus FaceMotion-owned FX clips that share bindings.</summary>
        private static void ComputeExpectedPartners(VRCAvatarDescriptor avatar, AnimationClip clip, string parameter, List<string> partnerParameters, HashSet<EditorCurveBinding> sharedBindings)
        {
            if (avatar == null || clip == null) return;
            foreach (var merge in avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true))
            {
                if (merge.animator == null) continue;
                var managed = FindManifestOwningObject(avatar, merge.gameObject);
                if (managed == null || string.Equals(managed.ParameterName, parameter, StringComparison.Ordinal)) continue;
                foreach (var candidate in merge.animator.animationClips)
                {
                    if (candidate == null) continue;
                    foreach (var binding in SharedBindings(candidate, clip))
                    {
                        sharedBindings.Add(binding);
                        if (!partnerParameters.Contains(managed.ParameterName)) partnerParameters.Add(managed.ParameterName);
                    }
                }
            }
            var fx = GetFx(avatar);
            if (fx == null) return;
            foreach (var candidate in fx.animationClips)
            {
                if (candidate == null) continue;
                var overlaps = false;
                foreach (var binding in SharedBindings(candidate, clip)) { sharedBindings.Add(binding); overlaps = true; }
                if (!overlaps) continue;
                var owning = DirectVRChatIntegration.FindOwningParameterForClip(avatar, candidate);
                if (owning != null && !string.Equals(owning, parameter, StringComparison.Ordinal) && !partnerParameters.Contains(owning)) partnerParameters.Add(owning);
            }
        }

        private static AnimationClip ClipOfController(AnimatorController controller)
        {
            if (controller == null) return null;
            AnimationClip fallback = null;
            var layers = controller.layers;
            for (var l = 0; l < layers.Length; l++)
            {
                var machine = layers[l].stateMachine;
                if (machine == null) continue;
                var states = machine.states;
                for (var s = 0; s < states.Length; s++)
                {
                    var motion = states[s].state == null ? null : states[s].state.motion;
                    if (!(motion is AnimationClip clip)) continue;
                    if (states[s].state.name == "On") return clip;
                    if (fallback == null && !clip.name.StartsWith("Reset", StringComparison.Ordinal)) fallback = clip;
                }
            }
            return fallback;
        }

        private static bool SameSet(List<string> expected, List<string> actual)
        {
            if (expected.Count != actual.Count) return false;
            for (var i = 0; i < expected.Count; i++)
                if (!actual.Contains(expected[i])) return false;
            return true;
        }

        private static bool HasParameter(AnimatorController controller, string name)
        {
            var parameters = controller.parameters;
            for (var i = 0; i < parameters.Length; i++)
                if (parameters[i] != null && string.Equals(parameters[i].name, name, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Hierarchy-only removal used by Apply's replace-in-place path; Apply deletes the old assets and manifest itself so the folder is reused.</summary>
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
                items.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarOwnership, "The integration object ownership could not be confirmed.", "Check the FaceMotion integration object and generated assets under the same avatar."));
                diagnostics = items;
                return false;
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remove FaceMotion Modular Avatar integration");
            Undo.DestroyObjectImmediate(integrationObject);
            if (owner != null) SessionManifests.Remove(owner);
            Undo.CollapseUndoOperations(undoGroup);
            items.Add(Info(FaceMotionDiagnosticCodes.ModularAvatarRemoved, "The FaceMotion Modular Avatar hierarchy was removed; generated assets were retained for reintegration."));
            diagnostics = items;
            return true;
        }

        /// <summary>All manifests attached to, or resolvable on, this avatar (session + persisted), deduplicated.</summary>
        private static IReadOnlyList<ModularAvatarIntegrationManifest> FindManagedManifests(VRCAvatarDescriptor avatar)
        {
            var results = new List<ModularAvatarIntegrationManifest>();
            if (avatar == null) return results;
            var seen = new HashSet<ModularAvatarIntegrationManifest>();
            if (SessionManifests.TryGetValue(avatar, out var session) && session != null && BelongsToAvatar(session, avatar) && seen.Add(session)) results.Add(session);
            foreach (var guid in AssetDatabase.FindAssets("t:ModularAvatarIntegrationManifest"))
            {
                var manifest = AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(AssetDatabase.GUIDToAssetPath(guid));
                if (manifest != null && seen.Add(manifest) && BelongsToAvatar(manifest, avatar)) results.Add(manifest);
            }
            return results;
        }

        /// <summary>
        /// Full user-facing removal of one integration: the owned hierarchy is destroyed and every
        /// FaceMotion-generated asset plus the manifest are deleted. Deletion is confined to the
        /// canonical generated file names listed in OwnedAssetPaths inside the manifest folder, and
        /// the empty folder is removed afterwards. A foreign object holding the integration name
        /// blocks removal so nothing beyond FaceMotion-owned files is ever touched.
        /// </summary>
        private static bool RemoveManaged(ModularAvatarIntegrationManifest manifest, VRCAvatarDescriptor owner, out IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        {
            var items = new List<FaceMotionDiagnostic>();
            if (manifest == null) { items.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarManifest, "The Modular Avatar manifest is missing.")); diagnostics = items; return false; }
            if (!BelongsToAvatar(manifest, owner)) { items.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarOwnership, "The Modular Avatar manifest does not belong to the selected avatar.")); diagnostics = items; return false; }
            var migration = ModularAvatarIntegrationManifestMigration.TryMigrateOnUse(manifest, owner, items);
            if (migration.Blocked) { diagnostics = items; return false; }
            owner = owner ?? ResolveAvatar(manifest);
            if (owner == null) { items.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarAvatar, "The owning avatar could not be resolved from the manifest.")); diagnostics = items; return false; }
            var integrationObject = ResolveIntegrationObject(manifest, owner);
            if (integrationObject == null && !string.IsNullOrEmpty(manifest.IntegrationObjectName) && owner.transform.Find(manifest.IntegrationObjectName) != null)
            {
                items.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarOwnership, "An object with the FaceMotion integration name exists but is not owned by the manifest.", "Rename or remove that object before removing the FaceMotion integration."));
                diagnostics = items;
                return false;
            }
            var manifestPath = AssetDatabase.GetAssetPath(manifest);
            var root = string.IsNullOrEmpty(manifestPath) ? null : Path.GetDirectoryName(manifestPath)?.Replace('\\', '/');
            var owned = manifest.OwnedAssetPaths ?? Array.Empty<string>();

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remove FaceMotion Modular Avatar integration");
            if (integrationObject != null) Undo.DestroyObjectImmediate(integrationObject);
            for (var i = 0; i < owned.Length; i++)
                if (GeneratedAssetOwnership.IsSafeOwnedAssetPath(owned[i], root) && GeneratedAssetOwnership.IsCanonicalGeneratedFileName(Path.GetFileName(owned[i]))) AssetDatabase.DeleteAsset(owned[i]);
            if (!string.IsNullOrEmpty(manifestPath)) AssetDatabase.DeleteAsset(manifestPath);
            GeneratedAssetOwnership.TryDeleteEmptyOwnedFolder(root, owned);
            AssetDatabase.SaveAssets();
            if (owner != null) SessionManifests.Remove(owner);
            var keys = new List<VRCAvatarDescriptor>(SessionManifests.Count);
            foreach (var pair in SessionManifests) if (pair.Value == manifest) keys.Add(pair.Key);
            for (var k = 0; k < keys.Count; k++) SessionManifests.Remove(keys[k]);
            Undo.CollapseUndoOperations(undoGroup);
            items.Add(Info(FaceMotionDiagnosticCodes.ModularAvatarRemoved, "The FaceMotion Modular Avatar integration, its generated assets, and its manifest were removed."));
            diagnostics = items;
            return true;
        }

        // Reconciliation calls this before any export or hierarchy mutation. Unlike migration,
        // it intentionally performs no repair or persistence while proving ownership.
        private static bool ValidateManagedState(ModularAvatarIntegrationManifest manifest, VRCAvatarDescriptor avatar, List<FaceMotionDiagnostic> diagnostics)
        {
            if (!BelongsToAvatar(manifest, avatar))
            {
                diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarOwnership, "The Modular Avatar manifest does not belong to the selected avatar."));
                return false;
            }
            if (manifest.SchemaVersion > FaceMotionVersions.IntegrationManifestVersion)
            {
                diagnostics.Add(Error(FaceMotionDiagnosticCodes.FutureSchemaBlocked, "The Modular Avatar manifest uses a newer schema and cannot be reconciled."));
                return false;
            }
            if (string.IsNullOrEmpty(manifest.ParameterName))
            {
                diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarOwnership, "The Modular Avatar manifest has no ownership parameter."));
                return false;
            }
            if (!HasSafeOwnedAssetPaths(manifest)) { diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarOwnership, "The Modular Avatar manifest contains an unsafe owned asset path.")); return false; }
            if (ResolveIntegrationObject(manifest, avatar) != null) return true;
            if (!string.IsNullOrEmpty(manifest.IntegrationObjectName) && avatar.transform.Find(manifest.IntegrationObjectName) != null)
            {
                diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarOwnership, "An integration object name is occupied by content not owned by FaceMotion."));
                return false;
            }
            return true; // A detached manifest retains its own generated assets and is safe to remove.
        }

        // Batch rollback deliberately differs from user-facing Remove: it deletes only assets and
        // hierarchy proven to have been created by the failed batch.
        private static void RollbackCreated(ModularAvatarIntegrationManifest manifest)
        {
            if (manifest == null) return;
            var owner = ResolveAvatar(manifest);
            var node = ResolveIntegrationObject(manifest, owner);
            if (node != null) UnityEngine.Object.DestroyImmediate(node);
            if (owner != null && SessionManifests.TryGetValue(owner, out var session) && session == manifest) SessionManifests.Remove(owner);
            var paths = manifest.OwnedAssetPaths ?? Array.Empty<string>();
            var manifestPath = AssetDatabase.GetAssetPath(manifest);
            var root = Path.GetDirectoryName(manifestPath)?.Replace('\\', '/');
            for (var i = 0; i < paths.Length; i++)
                if (GeneratedAssetOwnership.IsSafeOwnedAssetPath(paths[i], root) && GeneratedAssetOwnership.IsCanonicalGeneratedFileName(Path.GetFileName(paths[i]))) AssetDatabase.DeleteAsset(paths[i]);
            if (!string.IsNullOrEmpty(manifestPath)) AssetDatabase.DeleteAsset(manifestPath);
            GeneratedAssetOwnership.TryDeleteEmptyOwnedFolder(root, paths);
            AssetDatabase.SaveAssets();
        }

        private static string MenuLabel(ModularAvatarIntegrationPlan plan)
        {
            string displayName = string.IsNullOrEmpty(plan.Request.DisplayName) ? "FaceMotion" : plan.Request.DisplayName;
            return plan.ObjectName == Prefix + Sanitize(displayName) ? displayName : plan.ObjectName.Substring(Prefix.Length);
        }

        private static void ValidateConflicts(VRCAvatarDescriptor avatar, AnimationClip clip, string parameter, ModularAvatarIntegrationManifest owned, HashSet<string> removingParameters, List<FaceMotionDiagnostic> diagnostics, List<string> partnerParameters, HashSet<EditorCurveBinding> sharedBindings)
        {
            foreach (var parameters in avatar.GetComponentsInChildren<ModularAvatarParameters>(true))
                if (!IsOwned(parameters.gameObject, owned, avatar) && !IsScheduledForRemoval(parameters.gameObject, avatar, removingParameters))
                    if (parameters.parameters != null) foreach (var config in parameters.parameters) if (!config.isPrefix && config.nameOrPrefix == parameter) diagnostics.Add(Error(FaceMotionDiagnosticCodes.ModularAvatarParameterConflict, "An MA Parameters component already defines the generated parameter."));
            foreach (var merge in avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true))
            {
                if (IsOwned(merge.gameObject, owned, avatar) || IsScheduledForRemoval(merge.gameObject, avatar, removingParameters) || merge.animator == null) continue;
                var managed = FindManifestOwningObject(avatar, merge.gameObject);
                var warnedShared = false;
                foreach (var candidate in merge.animator.animationClips)
                {
                    if (candidate == null) continue;
                    foreach (var binding in SharedBindings(candidate, clip))
                    {
                        string objectPath = RelativePathUtility.GetRelativePath(avatar.transform, merge.transform) ?? string.Empty;
                        string description = DescribeBinding(binding);
                        if (managed != null && !string.Equals(managed.ParameterName, parameter, StringComparison.Ordinal))
                        {
                            if (!partnerParameters.Contains(managed.ParameterName)) partnerParameters.Add(managed.ParameterName);
                            sharedBindings.Add(binding);
                            if (!warnedShared)
                            {
                                warnedShared = true;
                                diagnostics.Add(SharedBindingWarning(
                                    "The Modular Avatar Merge Animator \"" + merge.gameObject.name + "\" shares binding \"" + description + "\" through clip \"" + candidate.name + "\".",
                                    objectPath, MergeAnimatorConflictDetails(merge, objectPath, candidate, binding)));
                            }
                            continue;
                        }
                        diagnostics.Add(new FaceMotionDiagnostic(
                            FaceMotionDiagnosticCodes.ModularAvatarBindingConflict,
                            FaceMotionDiagnosticSeverity.Error,
                            "The Modular Avatar Merge Animator \"" + merge.gameObject.name + "\" shares binding \"" + description + "\" through clip \"" + candidate.name + "\".",
                            objectPath,
                            true,
                            "Resolve the competing Modular Avatar binding before planning the integration again.",
                            MergeAnimatorConflictDetails(merge, objectPath, candidate, binding)));
                    }
                }
            }
            var fx = GetFx(avatar);
            if (fx != null)
            {
                var warnedFxShared = false;
                foreach (var candidate in fx.animationClips)
                {
                    if (candidate == null) continue;
                    var overlaps = new List<EditorCurveBinding>();
                    foreach (var binding in SharedBindings(candidate, clip)) overlaps.Add(binding);
                    if (overlaps.Count == 0) continue;
                    var owning = DirectVRChatIntegration.FindOwningParameterForClip(avatar, candidate);
                    if (owning != null)
                    {
                        if (!string.Equals(owning, parameter, StringComparison.Ordinal) && !partnerParameters.Contains(owning)) partnerParameters.Add(owning);
                        foreach (var binding in overlaps) sharedBindings.Add(binding);
                        if (!warnedFxShared)
                        {
                            warnedFxShared = true;
                            var first = overlaps[0];
                            diagnostics.Add(SharedBindingWarning(
                                "The avatar FX controller clip \"" + candidate.name + "\" (FaceMotion-owned) shares binding \"" + DescribeBinding(first) + "\".",
                                first.path,
                                new Dictionary<string, string>
                                {
                                    { FaceMotionDiagnosticDetailKeys.Reason, FaceMotionDiagnosticDetailKeys.ReasonMergeAnimatorBinding },
                                    { FaceMotionDiagnosticDetailKeys.ConflictController, fx == null ? string.Empty : fx.name },
                                    { FaceMotionDiagnosticDetailKeys.ConflictClip, candidate.name },
                                    { FaceMotionDiagnosticDetailKeys.BindingPath, first.path },
                                    { FaceMotionDiagnosticDetailKeys.BindingProperty, first.propertyName },
                                    { FaceMotionDiagnosticDetailKeys.BindingType, first.type == null ? string.Empty : first.type.Name },
                                    { FaceMotionDiagnosticDetailKeys.Binding, DescribeBinding(first) }
                                }));
                        }
                        continue;
                    }
                    var foreignBinding = overlaps[0];
                    diagnostics.Add(new FaceMotionDiagnostic(
                        FaceMotionDiagnosticCodes.ModularAvatarCrossBindingConflict,
                        FaceMotionDiagnosticSeverity.Error,
                        "The avatar FX controller clip \"" + candidate.name + "\" shares binding \"" + DescribeBinding(foreignBinding) + "\".",
                        foreignBinding.path,
                        true,
                        "Resolve the external avatar FX binding before planning the integration again.",
                        new Dictionary<string, string>
                        {
                            { FaceMotionDiagnosticDetailKeys.Reason, FaceMotionDiagnosticDetailKeys.ReasonMergeAnimatorBinding },
                            { FaceMotionDiagnosticDetailKeys.ConflictController, fx.name },
                            { FaceMotionDiagnosticDetailKeys.ConflictClip, candidate.name },
                            { FaceMotionDiagnosticDetailKeys.BindingPath, foreignBinding.path },
                            { FaceMotionDiagnosticDetailKeys.BindingProperty, foreignBinding.propertyName },
                            { FaceMotionDiagnosticDetailKeys.BindingType, foreignBinding.type == null ? string.Empty : foreignBinding.type.Name },
                            { FaceMotionDiagnosticDetailKeys.Binding, DescribeBinding(foreignBinding) }
                        }));
                }
            }
        }

        private static ModularAvatarIntegrationManifest FindManifestOwningObject(VRCAvatarDescriptor avatar, GameObject integrationObject)
        {
            if (avatar == null || integrationObject == null) return null;
            foreach (var manifest in FindManagedManifests(avatar))
                if (manifest != null && IsOwned(integrationObject, manifest, avatar)) return manifest;
            return null;
        }

        private static FaceMotionDiagnostic SharedBindingWarning(string message, string contextId, Dictionary<string, string> details)
        {
            return new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.ModularAvatarSharedBindingWarning,
                FaceMotionDiagnosticSeverity.Warning,
                message,
                contextId,
                false,
                "When both animations are enabled at the same time the later layer wins; keep the bindings separate if a different outcome is required.",
                details);
        }

        private static void ValidateAmbiguousClipBindings(VRCAvatarDescriptor avatar, AnimationClip clip, List<FaceMotionDiagnostic> diagnostics)
        {
            var pathCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var transform in avatar.GetComponentsInChildren<Transform>(true))
            {
                string path = RelativePathUtility.GetRelativePath(avatar.transform, transform);
                if (path == null) continue;
                pathCounts.TryGetValue(path, out int count);
                pathCounts[path] = count + 1;
            }

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (!pathCounts.TryGetValue(binding.path, out int count) || count < 2) continue;
                string description = DescribeBinding(binding);
                diagnostics.Add(new FaceMotionDiagnostic(
                    FaceMotionDiagnosticCodes.ModularAvatarBindingConflict,
                    FaceMotionDiagnosticSeverity.Error,
                    "The AnimationClip binding \"" + description + "\" targets the ambiguous avatar path \"" + binding.path + "\".",
                    binding.path,
                    true,
                    "Resolve the duplicate relative path before planning the integration again.",
                    AmbiguousBindingDetails(binding, description)));
            }
        }

        private static string DescribeBinding(EditorCurveBinding binding)
        {
            return (string.IsNullOrEmpty(binding.path) ? "<avatar root>" : binding.path)
                + " / " + binding.type.Name + " / " + binding.propertyName;
        }
        private static Dictionary<string, string> MergeAnimatorConflictDetails(ModularAvatarMergeAnimator merge, string objectPath, AnimationClip clip, EditorCurveBinding binding)
        {
            return new Dictionary<string, string>
            {
                { FaceMotionDiagnosticDetailKeys.Reason, FaceMotionDiagnosticDetailKeys.ReasonMergeAnimatorBinding },
                { FaceMotionDiagnosticDetailKeys.ConflictObjectName, merge.gameObject.name },
                { FaceMotionDiagnosticDetailKeys.ConflictObjectPath, objectPath },
                { FaceMotionDiagnosticDetailKeys.ConflictComponent, nameof(ModularAvatarMergeAnimator) },
                { FaceMotionDiagnosticDetailKeys.ConflictController, merge.animator == null ? string.Empty : merge.animator.name },
                { FaceMotionDiagnosticDetailKeys.ConflictClip, clip == null ? string.Empty : clip.name },
                { FaceMotionDiagnosticDetailKeys.BindingPath, binding.path },
                { FaceMotionDiagnosticDetailKeys.BindingProperty, binding.propertyName },
                { FaceMotionDiagnosticDetailKeys.BindingType, binding.type == null ? string.Empty : binding.type.Name },
                { FaceMotionDiagnosticDetailKeys.Binding, DescribeBinding(binding) }
            };
        }
        private static Dictionary<string, string> AmbiguousBindingDetails(EditorCurveBinding binding, string description)
        {
            return new Dictionary<string, string>
            {
                { FaceMotionDiagnosticDetailKeys.Reason, FaceMotionDiagnosticDetailKeys.ReasonAmbiguousRelativePath },
                { FaceMotionDiagnosticDetailKeys.BindingPath, binding.path },
                { FaceMotionDiagnosticDetailKeys.BindingProperty, binding.propertyName },
                { FaceMotionDiagnosticDetailKeys.BindingType, binding.type == null ? string.Empty : binding.type.Name },
                { FaceMotionDiagnosticDetailKeys.Binding, description }
            };
        }
        private static ModularAvatarIntegrationManifest FindManifest(VRCAvatarDescriptor avatar, string parameter = null) { if (avatar == null) return null; if (SessionManifests.TryGetValue(avatar, out var session) && session != null && (parameter == null || session.ParameterName == parameter) && BelongsToAvatar(session, avatar)) return session; foreach (var guid in AssetDatabase.FindAssets("t:ModularAvatarIntegrationManifest")) { var manifest = AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(AssetDatabase.GUIDToAssetPath(guid)); if (manifest != null && (parameter == null || manifest.ParameterName == parameter) && BelongsToAvatar(manifest, avatar)) return manifest; } return null; }
        private static ModularAvatarIntegrationManifest FindRemovalCandidateManifest(VRCAvatarDescriptor avatar, string parameter = null) { if (avatar == null) return null; if (SessionManifests.TryGetValue(avatar, out var session) && session != null && (parameter == null || session.ParameterName == parameter) && BelongsToAvatar(session, avatar)) return session; foreach (var guid in AssetDatabase.FindAssets("t:ModularAvatarIntegrationManifest")) { var manifest = AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(AssetDatabase.GUIDToAssetPath(guid)); if (manifest != null && (parameter == null || manifest.ParameterName == parameter) && BelongsToAvatar(manifest, avatar)) return manifest; } return null; }
        private static bool RootIsOccupiedByOtherIntegration(VRCAvatarDescriptor avatar, string outputFolder, string parameter, string rootName, HashSet<string> removingParameters) { var existing = FindManifest(avatar, parameter) ?? FindRemovalCandidateManifest(avatar, parameter); if (existing != null) return Path.GetFileName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(existing))) != rootName; if (removingParameters != null) foreach (var manifest in FindManagedManifests(avatar)) if (removingParameters.Contains(manifest.ParameterName) && Path.GetFileName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(manifest))) == rootName) return false; return !string.IsNullOrEmpty(outputFolder) && AssetDatabase.IsValidFolder(outputFolder + "/" + rootName); }
        private static bool IsScheduledForRemoval(GameObject value, VRCAvatarDescriptor avatar, HashSet<string> removingParameters) { if (value == null || removingParameters == null) return false; foreach (var manifest in FindManagedManifests(avatar)) if (removingParameters.Contains(manifest.ParameterName) && IsOwned(value, manifest, avatar)) return true; return false; }

        public object CaptureRollbackSnapshot(VRCAvatarDescriptor avatar)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Reconcile FaceMotion Modular Avatar integrations");
            var files = new List<RollbackSnapshotEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var existingRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var manifest in FindManagedManifests(avatar))
            {
                if (manifest == null) continue;
                var manifestPath = AssetDatabase.GetAssetPath(manifest);
                var root = string.IsNullOrEmpty(manifestPath) ? null : Path.GetDirectoryName(manifestPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(root) && AssetDatabase.IsValidFolder(root)) existingRoots.Add(root);
                var owned = manifest.OwnedAssetPaths ?? Array.Empty<string>();
                for (var i = 0; i < owned.Length; i++)
                    if (GeneratedAssetOwnership.IsSafeOwnedAssetPath(owned[i], root) && GeneratedAssetOwnership.IsCanonicalGeneratedFileName(Path.GetFileName(owned[i])))
                        AddSnapshotFile(files, seen, owned[i], root);
                if (!string.IsNullOrEmpty(manifestPath)) AddSnapshotFile(files, seen, manifestPath, root);
            }
            return new RollbackSnapshot(group, avatar, files, existingRoots);
        }

        public ModularAvatarIntegrationBatchResult RestoreRollbackSnapshot(object snapshot)
        {
            if (!(snapshot is RollbackSnapshot rollback)) return new ModularAvatarIntegrationBatchResult(false, Array.Empty<ModularAvatarIntegrationResult>(), new[] { Error(FaceMotionDiagnosticCodes.ModularAvatarApply, "The reconciliation rollback snapshot is unavailable.") });
            try { RollbackFailureInjector?.Invoke("start-rollback"); }
            catch (Exception injected)
            {
                return new ModularAvatarIntegrationBatchResult(false, Array.Empty<ModularAvatarIntegrationResult>(), new[] { Error(FaceMotionDiagnosticCodes.ModularAvatarApply, "Rollback failed: Injected rollback failure: " + injected.Message) });
            }
            var errors = new List<string>();
            try
            {
                try { Undo.RevertAllDownToGroup(rollback.UndoGroup); AssetDatabase.Refresh(); }
                catch (Exception revertException) { errors.Add("Undo revert failed: " + revertException.Message); }
                RemoveLeakedContent(rollback, errors);
                RestoreFolders(rollback, errors);
                RestoreFiles(rollback, errors);
                RebindRestoredManifests(rollback, errors);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                VerifyRollback(rollback, errors);
            }
            catch (Exception rollbackException) { errors.Add("Rollback failed: " + rollbackException.Message); }
            if (errors.Count > 0)
            {
                var detail = string.Join(" / ", errors.ToArray());
                try { RollbackFailureInjector?.Invoke(detail); }
                catch (Exception injected) { detail = detail + " / Injected rollback failure: " + injected.Message; }
                return new ModularAvatarIntegrationBatchResult(false, Array.Empty<ModularAvatarIntegrationResult>(), new[] { Error(FaceMotionDiagnosticCodes.ModularAvatarApply, "Rollback failed: " + detail) });
            }
            return new ModularAvatarIntegrationBatchResult(true, Array.Empty<ModularAvatarIntegrationResult>(), new[] { Info(FaceMotionDiagnosticCodes.ModularAvatarApplied, "The previous Modular Avatar state was restored after reconciliation failed.") });
        }

        /// <summary>Deletes integration content that was created by the failed transaction but did not exist at capture time.</summary>
        private static void RemoveLeakedContent(RollbackSnapshot rollback, List<string> errors)
        {
            var snapshotPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < rollback.Files.Count; i++) snapshotPaths.Add(rollback.Files[i].Path);
            foreach (var manifest in FindManagedManifests(rollback.Avatar))
            {
                if (manifest == null) continue;
                string manifestPath = AssetDatabase.GetAssetPath(manifest);
                if (string.IsNullOrEmpty(manifestPath) || snapshotPaths.Contains(manifestPath)) continue;
                try { RollbackCreated(manifest); }
                catch (Exception exception) { errors.Add("Leaked integration cleanup failed for " + manifestPath + ": " + exception.Message); }
            }
        }

        /// <summary>Recreates every generated folder that disappeared during the failed transaction.</summary>
        private static void RestoreFolders(RollbackSnapshot rollback, List<string> errors)
        {
            for (var i = 0; i < rollback.Files.Count; i++)
            {
                var parent = Path.GetDirectoryName(rollback.Files[i].Path)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(parent)) continue;
                try { EnsureAssetFolder(parent); }
                catch (Exception exception) { errors.Add("Folder restore failed for " + parent + ": " + exception.Message); }
            }
        }

        /// <summary>Restores exact bytes and .meta (and therefore GUIDs) for every file that pre-existed the transaction and drifted or disappeared.</summary>
        private static void RestoreFiles(RollbackSnapshot rollback, List<string> errors)
        {
            for (var i = 0; i < rollback.Files.Count; i++)
            {
                var entry = rollback.Files[i];
                if (!entry.ExistedBefore) continue;
                try
                {
                    string full = AsFullPath(entry.Path);
                    bool drifted = !File.Exists(full) || !ByteEquals(full, entry.Bytes) || entry.MetaBytes != null && (!File.Exists(full + ".meta") || !ByteEquals(full + ".meta", entry.MetaBytes));
                    if (!drifted) continue;
                    File.WriteAllBytes(full, entry.Bytes);
                    if (entry.MetaBytes != null) File.WriteAllBytes(full + ".meta", entry.MetaBytes);
                    AssetDatabase.ImportAsset(entry.Path, ImportAssetOptions.ForceUpdate);
                }
                catch (Exception exception) { errors.Add("File restore failed for " + entry.Path + ": " + exception.Message); }
            }
        }

        /// <summary>Proves every pre-existing file and its .meta survived, and no leaked file remains.</summary>
        private static void VerifyRollback(RollbackSnapshot rollback, List<string> errors)
        {
            for (var i = 0; i < rollback.Files.Count; i++)
            {
                var entry = rollback.Files[i];
                string full = AsFullPath(entry.Path);
                if (entry.ExistedBefore)
                {
                    if (!File.Exists(full)) { errors.Add("Rollback verification failed: missing " + entry.Path); continue; }
                    if (!ByteEquals(full, entry.Bytes)) errors.Add("Rollback verification failed: content differs for " + entry.Path);
                    if (entry.MetaBytes != null && (!File.Exists(full + ".meta") || !ByteEquals(full + ".meta", entry.MetaBytes))) errors.Add("Rollback verification failed: .meta differs for " + entry.Path);
                }
                else if (File.Exists(full) && rollback.IsCanonicalLeak(entry.Path)) errors.Add("Rollback verification failed: leaked file remains " + entry.Path);
            }
        }

        private static void AddSnapshotFile(List<RollbackSnapshotEntry> files, HashSet<string> seen, string path, string root)
        {
            if (!seen.Add(path)) return;
            files.Add(new RollbackSnapshotEntry(path, root, File.Exists(AsFullPath(path)) ? File.ReadAllBytes(AsFullPath(path)) : null, File.Exists(AsFullPath(path) + ".meta") ? File.ReadAllBytes(AsFullPath(path) + ".meta") : null));
        }

        /// <summary>After byte-level restore, references from the manifest back to scene objects (and node references back to restored assets) can fail to rebind automatically; re-establish them so ownership re-validates.</summary>
        private static void RebindRestoredManifests(RollbackSnapshot rollback, List<string> errors)
        {
            if (rollback.Avatar == null) return;
            var snapshotPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < rollback.Files.Count; i++) snapshotPaths.Add(rollback.Files[i].Path);
            var guids = AssetDatabase.FindAssets("t:ModularAvatarIntegrationManifest");
            for (var g = 0; g < guids.Length; g++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[g]);
                if (!snapshotPaths.Contains(path)) continue;
                var manifest = AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(path);
                if (manifest == null) continue;
                if (manifest.Avatar == null)
                {
                    manifest.Avatar = rollback.Avatar;
                    EditorUtility.SetDirty(manifest);
                }
                if (manifest.Avatar != rollback.Avatar) continue;
                var node = ResolveIntegrationObject(manifest, rollback.Avatar)
                    ?? (string.IsNullOrEmpty(manifest.IntegrationObjectName) ? null : rollback.Avatar.transform.Find(manifest.IntegrationObjectName)?.gameObject);
                if (node == null) continue;
                var merge = node.GetComponent<ModularAvatarMergeAnimator>();
                var installer = node.GetComponent<ModularAvatarMenuInstaller>();
                var increments = false;
                if (merge != null && merge.animator == null)
                {
                    var controller = LoadOwned<RuntimeAnimatorController>(manifest, "FX.controller");
                    if (controller != null) { merge.animator = controller; increments = true; }
                }
                if (installer != null && installer.menuToAppend == null)
                {
                    var menu = LoadOwned<VRCExpressionsMenu>(manifest, "Menu.asset");
                    if (menu != null) { installer.menuToAppend = menu; increments = true; }
                }
                if (increments) EditorUtility.SetDirty(node);
            }
        }

        private static T LoadOwned<T>(ModularAvatarIntegrationManifest manifest, string fileName) where T : UnityEngine.Object
        {
            var owned = manifest.OwnedAssetPaths ?? Array.Empty<string>();
            for (var i = 0; i < owned.Length; i++)
                if (string.Equals(Path.GetFileName(owned[i]), fileName, StringComparison.Ordinal))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<T>(owned[i]);
                    if (asset != null) return asset;
                }
            return null;
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            assetPath = assetPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal) && assetPath != "Assets") throw new InvalidOperationException("Refusing to recreate folders outside Assets.");
            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var name = Path.GetFileName(assetPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name)) throw new InvalidOperationException("Invalid folder path: " + assetPath);
            EnsureAssetFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, name))) throw new InvalidOperationException("Could not recreate folder " + assetPath);
        }

        private static bool ByteEquals(string path, byte[] expected)
        {
            if (expected == null || !File.Exists(path)) return false;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length != expected.Length) return false;
                var buffer = new byte[expected.Length];
                int offset = 0;
                while (offset < expected.Length) { int read = stream.Read(buffer, offset, expected.Length - offset); if (read <= 0) return false; offset += read; }
                for (var i = 0; i < expected.Length; i++) if (buffer[i] != expected[i]) return false;
                return true;
            }
        }

        private static string AsFullPath(string assetPath)
        {
            var normalized = (assetPath ?? string.Empty).Replace('\\', '/');
            var relative = normalized.StartsWith("Assets/", StringComparison.Ordinal) ? normalized.Substring("Assets/".Length) : normalized;
            return (Application.dataPath.Replace('\\', '/') + "/" + relative);
        }

        private sealed class RollbackSnapshotEntry
        {
            public RollbackSnapshotEntry(string path, string root, byte[] bytes, byte[] metaBytes) { Path = path; Root = root; Bytes = bytes; MetaBytes = metaBytes; ExistedBefore = bytes != null; }
            public string Path { get; }
            public string Root { get; }
            public bool ExistedBefore { get; }
            public byte[] Bytes { get; }
            public byte[] MetaBytes { get; }
        }

        private sealed class RollbackSnapshot
        {
            public RollbackSnapshot(int undoGroup, VRCAvatarDescriptor avatar, List<RollbackSnapshotEntry> files, HashSet<string> existingRoots)
            { UndoGroup = undoGroup; Avatar = avatar; Files = files; ExistingRoots = existingRoots; }
            public int UndoGroup { get; }
            public VRCAvatarDescriptor Avatar { get; }
            public List<RollbackSnapshotEntry> Files { get; }
            public HashSet<string> ExistingRoots { get; }
            public bool IsCanonicalLeak(string path) { var name = Path.GetFileName(path); var dir = Path.GetDirectoryName(path)?.Replace('\\', '/'); return GeneratedAssetOwnership.IsCanonicalGeneratedFileName(name) && !ExistingRoots.Contains(dir); }
        }
        internal static VRCAvatarDescriptor ResolveAvatar(ModularAvatarIntegrationManifest manifest) { if (manifest == null) return null; if (manifest.Avatar != null) return manifest.Avatar; return GlobalObjectId.TryParse(manifest.AvatarGlobalId, out var id) ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as VRCAvatarDescriptor : null; }

        /// <summary>
        /// Unsaved scenes cannot persist an asset-to-scene avatar reference or a resolvable global
        /// ID. Only in that case, prove ownership from the direct child and every generated MA
        /// component/asset recorded by the manifest. A resolved different avatar never falls back.
        /// </summary>
        private static bool BelongsToAvatar(ModularAvatarIntegrationManifest manifest, VRCAvatarDescriptor avatar)
        {
            if (manifest == null || avatar == null)
            {
                return false;
            }

            VRCAvatarDescriptor resolved = ResolveAvatar(manifest);
            if (resolved != null)
            {
                return resolved == avatar;
            }

            return HasSafeOwnedAssetPaths(manifest)
                && ResolveIntegrationObject(manifest, avatar) != null;
        }

        private static bool HasSafeOwnedAssetPaths(ModularAvatarIntegrationManifest manifest)
        {
            string manifestPath = AssetDatabase.GetAssetPath(manifest);
            string root = string.IsNullOrEmpty(manifestPath) ? null : Path.GetDirectoryName(manifestPath)?.Replace('\\', '/');
            var owned = manifest.OwnedAssetPaths ?? Array.Empty<string>();
            for (var i = 0; i < owned.Length; i++)
            {
                if (!GeneratedAssetOwnership.IsSafeOwnedAssetPath(owned[i], root)
                    || !GeneratedAssetOwnership.IsCanonicalGeneratedFileName(Path.GetFileName(owned[i])))
                {
                    return false;
                }
            }

            return true;
        }
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
        private static IEnumerable<EditorCurveBinding> SharedBindings(AnimationClip a, AnimationClip b) { return AnimationBindingKey.SharedFloatBindings(a, b); }
        private static bool HasSharedBinding(AnimationClip a, AnimationClip b) { foreach (var binding in SharedBindings(a, b)) return true; return false; }

        private static bool IsAssetFolder(string path) { return OneClickIntegrationService.IsPlannableOutputFolder(path); }
        private static string Sanitize(string value) { var c = (value ?? "FaceMotion").ToCharArray(); for (var i = 0; i < c.Length; i++) if (!char.IsLetterOrDigit(c[i]) && c[i] != '_') c[i] = '_'; return new string(c); }
        private static FaceMotionDiagnostic Error(string code, string message, string fix = "Resolve the conflict or use Direct integration.") { return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Error, message, "modular-avatar", true, fix); }
        private static FaceMotionDiagnostic Info(string code, string message) { return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Info, message, "modular-avatar", false, string.Empty); }
    }
}
