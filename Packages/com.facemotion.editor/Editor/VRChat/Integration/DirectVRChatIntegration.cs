using System;
using System.Collections.Generic;
using System.IO;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.Export;
using FaceMotion.Serialization;
using FaceMotion.Versioning;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace FaceMotion.Editor.VRChat.Integration
{
    /// <summary>SDK-neutral input to the direct backend. Planning never mutates Unity assets.</summary>
    public sealed class DirectIntegrationRequest
    {
        public DirectIntegrationRequest(VRCAvatarDescriptor avatar, AnimationClip clip, string outputFolder, string displayName)
        {
            Avatar = avatar; Clip = clip; OutputFolder = outputFolder; DisplayName = displayName;
        }
        public VRCAvatarDescriptor Avatar { get; }
        public AnimationClip Clip { get; }
        public string OutputFolder { get; }
        public string DisplayName { get; }
    }

    /// <summary>Immutable description of the changes that Apply will make.</summary>
    public sealed class DirectIntegrationPlan
    {
        internal DirectIntegrationPlan(DirectIntegrationRequest request, string parameterName, string layerName, string assetStem, IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        { Request = request; ParameterName = parameterName; LayerName = layerName; AssetStem = assetStem; Diagnostics = diagnostics; }
        public DirectIntegrationRequest Request { get; }
        public string ParameterName { get; }
        public string LayerName { get; }
        public string AssetStem { get; }
        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }
        public bool IsValid { get { return !DirectVRChatIntegration.HasBlocking(Diagnostics); } }
    }

    public sealed class DirectIntegrationResult
    {
        internal DirectIntegrationResult(bool succeeded, DirectIntegrationManifest manifest, IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        { Succeeded = succeeded; Manifest = manifest; Diagnostics = diagnostics; }
        public bool Succeeded { get; }
        public DirectIntegrationManifest Manifest { get; }
        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }
    }

    /// <summary>One candidate in a Direct multi-animation integration request.</summary>
    public sealed class DirectIntegrationBatchItemRequest
    {
        public DirectIntegrationBatchItemRequest(AnimationClip clip, string displayName) { Clip = clip; DisplayName = displayName; }
        public AnimationClip Clip { get; }
        public string DisplayName { get; }
    }

    /// <summary>SDK-neutral input for composing several clips into one Direct output set.</summary>
    public sealed class DirectIntegrationBatchRequest
    {
        public DirectIntegrationBatchRequest(VRCAvatarDescriptor avatar, IReadOnlyList<DirectIntegrationBatchItemRequest> items, string outputFolder)
        { Avatar = avatar; Items = items; OutputFolder = outputFolder; }
        public VRCAvatarDescriptor Avatar { get; }
        public IReadOnlyList<DirectIntegrationBatchItemRequest> Items { get; }
        public string OutputFolder { get; }
    }

    public sealed class DirectIntegrationBatchPlan
    {
        internal DirectIntegrationBatchPlan(DirectIntegrationBatchRequest request, IReadOnlyList<string> parameters, IReadOnlyList<string> layers, IReadOnlyList<string> stems, IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        { Request = request; ParameterNames = parameters; LayerNames = layers; AssetStems = stems; Diagnostics = diagnostics; }
        public DirectIntegrationBatchRequest Request { get; }
        public IReadOnlyList<string> ParameterNames { get; }
        public IReadOnlyList<string> LayerNames { get; }
        public IReadOnlyList<string> AssetStems { get; }
        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }
        public bool IsValid { get { return !DirectVRChatIntegration.HasBlocking(Diagnostics); } }
    }

    public sealed class DirectIntegrationBatchResult
    {
        internal DirectIntegrationBatchResult(bool succeeded, DirectBatchIntegrationManifest manifest, IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        { Succeeded = succeeded; Manifest = manifest; Diagnostics = diagnostics; }
        public bool Succeeded { get; }
        public DirectBatchIntegrationManifest Manifest { get; }
        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }
    }

    /// <summary>Phase G's direct SDK 3.10.5 backend. It never edits user-owned FX, parameter, or menu assets.</summary>
    public static class DirectVRChatIntegration
    {
        public const string BackendId = "direct-vrchat-sdk3";
        public const int BackendVersion = 1;
        private const int MenuCapacity = 8;
        private static readonly Dictionary<VRCAvatarDescriptor, DirectIntegrationManifest> SessionManifests = new Dictionary<VRCAvatarDescriptor, DirectIntegrationManifest>();
        internal static Action<string> PlanFailureInjector;
        internal static Action<string> ApplyFailureInjector;

        public static DirectIntegrationPlan Plan(DirectIntegrationRequest request)
        {
            try
            {
                PlanFailureInjector?.Invoke("before-plan");
                return PlanCore(request);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return new DirectIntegrationPlan(
                    request,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    new[] { UnexpectedPlanDiagnostic(exception, request) });
            }
        }

        /// <summary>True when this avatar carries an active Direct integration manifest.</summary>
        public static bool HasExistingIntegration(VRCAvatarDescriptor avatar)
        {
            return FindManifest(avatar) != null;
        }

        /// <summary>Plans a K2 batch without touching assets or the avatar descriptor.</summary>
        public static DirectIntegrationBatchPlan PlanBatch(DirectIntegrationBatchRequest request)
        {
            var diagnostics = new List<FaceMotionDiagnostic>();
            var parameters = new List<string>(); var layers = new List<string>(); var stems = new List<string>();
            if (request == null || request.Avatar == null) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationAvatar, "Select a VRCAvatarDescriptor.", "Select the avatar root."));
            if (request != null && request.Avatar != null && EditorUtility.IsPersistent(request.Avatar)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationPrefabAsset, "Direct integration cannot modify a prefab asset.", "Instantiate the avatar in a scene before integrating."));
            if (request == null || !IsAssetFolder(request.OutputFolder)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationPath, "Output folder must be an existing folder under Assets.", "Choose an existing Assets folder."));
            if (request == null || request.Items == null || request.Items.Count == 0) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationClip, "Provide at least one AnimationClip.", "Select one or more exported clips."));
            if (request != null && request.Items != null && request.Items.Count > MenuCapacity) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationMenuCapacity, "A Direct batch supports at most eight clips in its generated submenu.", "Split the batch into smaller integrations."));
            var clipPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (request != null && request.Items != null) for (int i = 0; i < request.Items.Count; i++)
            {
                var item = request.Items[i]; var stem = Sanitize(item == null ? null : item.DisplayName); var parameter = "FaceMotion_" + stem;
                stems.Add(stem); parameters.Add(parameter); layers.Add("FaceMotion " + stem);
                string clipPath = item == null || item.Clip == null ? string.Empty : AssetDatabase.GetAssetPath(item.Clip);
                if (item == null || item.Clip == null || string.IsNullOrEmpty(clipPath)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationClip, "Every batch item must reference an asset AnimationClip.", "Export the clip to Assets before integrating."));
                else if (!clipPaths.Add(clipPath)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationClip, "A batch cannot contain the same AnimationClip path twice.", "Keep each clip path unique."));
                if (!names.Add(parameter)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationParameterConflict, "Batch display names produce duplicate generated parameter names.", "Use unique animation names."));
                if (parameter.Length > 256) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationParameterName, "A generated parameter name exceeds VRChat's 256 character limit.", "Use a shorter animation name."));
            }
            var existing = request == null ? null : FindBatchManifest(request.Avatar);
            if (request != null && request.Avatar != null && request.Items != null && existing == null) ValidateBatchExisting(request.Avatar, request.Items, parameters, diagnostics);
            if (request != null && request.Avatar != null && FindManifest(request.Avatar) != null) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationManifest, "This avatar has a single Direct integration. Roll it back before applying a batch.", "Use one Direct integration ownership model at a time."));
            if (existing != null && !BatchMatches(existing, request, parameters)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationManifest, "This avatar already has a different Direct batch integration.", "Rollback the existing batch before changing its items."));
            string root = request == null ? string.Empty : request.OutputFolder + "/FaceMotion_Batch";
            if (request != null && IsAssetFolder(request.OutputFolder) && AssetDatabase.IsValidFolder(root) && existing == null) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationOutputConflict, "The batch output folder already exists and is not owned by this avatar's batch.", "Choose a different output folder."));
            return new DirectIntegrationBatchPlan(request, parameters, layers, stems, diagnostics);
        }

        /// <summary>Applies all planned candidates to one copy-on-write FX, parameters, and menu set.</summary>
        public static DirectIntegrationBatchResult ApplyBatch(DirectIntegrationBatchPlan plan)
        {
            if (plan == null) return new DirectIntegrationBatchResult(false, null, new[] { Error(FaceMotionDiagnosticCodes.GenerationPlan, "No batch integration plan was supplied.", "Plan the integration again.") });
            if (!plan.IsValid) return new DirectIntegrationBatchResult(false, null, plan.Diagnostics);
            // Revalidate immediately before writes so a stale plan cannot bypass preflight after the avatar changes.
            plan = PlanBatch(plan.Request);
            if (!plan.IsValid) return new DirectIntegrationBatchResult(false, null, plan.Diagnostics);
            var existing = FindBatchManifest(plan.Request.Avatar);
            if (existing != null && BatchMatches(existing, plan.Request, plan.ParameterNames)) return new DirectIntegrationBatchResult(true, existing, plan.Diagnostics);
            var request = plan.Request; var diagnostics = new List<FaceMotionDiagnostic>(plan.Diagnostics); var owned = new List<string>(); string root = request.OutputFolder + "/FaceMotion_Batch"; int undoGroup = -1;
            try
            {
                Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Apply FaceMotion VRChat batch integration"); Undo.RegisterCompleteObjectUndo(request.Avatar, "Apply FaceMotion VRChat batch integration");
                if (!AssetDatabase.IsValidFolder(root) && string.IsNullOrEmpty(AssetDatabase.CreateFolder(request.OutputFolder, "FaceMotion_Batch"))) throw new InvalidOperationException("Could not create the FaceMotion batch output folder.");
                var fx = CloneFx(request.Avatar, root + "/FX.controller", owned); var parameters = CloneParameters(request.Avatar.expressionParameters, root + "/Parameters.asset", owned); var menu = CloneMenu(request.Avatar.expressionsMenu, root + "/Menu.asset", owned);
                var subMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>(); subMenu.name = "FaceMotion"; AssetDatabase.CreateAsset(subMenu, root + "/FaceMotion.menu.asset"); owned.Add(root + "/FaceMotion.menu.asset");
                var records = new DirectBatchIntegrationManifest.Item[request.Items.Count];
                for (int i = 0; i < request.Items.Count; i++)
                {
                    AddParameter(parameters, plan.ParameterNames[i]); AddMenuControl(subMenu, null, plan.ParameterNames[i], request.Items[i].DisplayName);
                    var reset = ResetClipBuilder.Create(request.Avatar, request.Items[i].Clip); reset.name = "Reset " + plan.AssetStems[i]; string resetPath = root + "/Reset_" + plan.AssetStems[i] + ".anim"; AssetDatabase.CreateAsset(reset, resetPath); owned.Add(resetPath);
                    AddLayer(fx, plan.LayerNames[i], plan.ParameterNames[i], request.Items[i].Clip, reset);
                    records[i] = new DirectBatchIntegrationManifest.Item { ClipPath = AssetDatabase.GetAssetPath(request.Items[i].Clip), DisplayName = request.Items[i].DisplayName, ParameterName = plan.ParameterNames[i], LayerName = plan.LayerNames[i] };
                    InjectFailure("after-batch-item-" + i);
                }
                AddMenuControl(menu, subMenu);
                var manifest = ScriptableObject.CreateInstance<DirectBatchIntegrationManifest>(); manifest.name = "FaceMotion Batch Integration Manifest"; manifest.Avatar = request.Avatar; manifest.AvatarGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(request.Avatar).ToString(); manifest.OriginalFx = GetFx(request.Avatar); manifest.OriginalParameters = request.Avatar.expressionParameters; manifest.OriginalMenu = request.Avatar.expressionsMenu; manifest.GeneratedFx = fx; manifest.GeneratedParameters = parameters; manifest.GeneratedMenu = menu; manifest.GeneratedSubMenu = subMenu; manifest.Items = records;
                string manifestPath = root + "/BatchManifest.asset"; AssetDatabase.CreateAsset(manifest, manifestPath); owned.Add(manifestPath); manifest.OwnedAssetPaths = owned.ToArray(); InjectFailure("before-batch-descriptor-assignment");
                SetFx(request.Avatar, fx); request.Avatar.expressionParameters = parameters; request.Avatar.expressionsMenu = menu; EditorUtility.SetDirty(request.Avatar); EditorUtility.SetDirty(manifest); AssetDatabase.SaveAssets(); Undo.CollapseUndoOperations(undoGroup);
                diagnostics.Add(Info(FaceMotionDiagnosticCodes.GenerationApplied, "Direct VRChat batch integration applied using one copy-on-write asset set.")); return new DirectIntegrationBatchResult(true, manifest, diagnostics);
            }
            catch (Exception exception)
            {
                if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup); for (int i = owned.Count - 1; i >= 0; i--) AssetDatabase.DeleteAsset(owned[i]); GeneratedAssetOwnership.TryDeleteEmptyOwnedFolder(root, owned); AssetDatabase.SaveAssets(); diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationApply, exception.Message, "The batch was rolled back; no descriptor changes were retained.")); return new DirectIntegrationBatchResult(false, null, diagnostics);
            }
        }

        public static bool RollbackBatch(DirectBatchIntegrationManifest manifest, out IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        {
            var items = new List<FaceMotionDiagnostic>(); var avatar = manifest == null ? null : manifest.Avatar;
            if (avatar == null && manifest != null && !string.IsNullOrEmpty(manifest.AvatarGlobalId) && GlobalObjectId.TryParse(manifest.AvatarGlobalId, out var avatarId)) avatar = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(avatarId) as VRCAvatarDescriptor;
            if (avatar == null) { items.Add(Error(FaceMotionDiagnosticCodes.GenerationRollback, "The batch manifest or its avatar is missing.", "Restore the avatar references manually.")); diagnostics = items; return false; }
            Undo.RegisterCompleteObjectUndo(avatar, "Rollback FaceMotion VRChat batch integration"); SetFx(avatar, manifest.OriginalFx); avatar.expressionParameters = manifest.OriginalParameters; avatar.expressionsMenu = manifest.OriginalMenu; EditorUtility.SetDirty(avatar);
            string manifestPath = AssetDatabase.GetAssetPath(manifest); string root = Path.GetDirectoryName(manifestPath)?.Replace('\\', '/');
            if (manifest.OwnedAssetPaths != null) for (int i = 0; i < manifest.OwnedAssetPaths.Length; i++) { var path = manifest.OwnedAssetPaths[i]; if (path != manifestPath && GeneratedAssetOwnership.IsSafeOwnedAssetPath(path, root) && IsBatchGeneratedFileName(Path.GetFileName(path))) AssetDatabase.DeleteAsset(path); else if (path != manifestPath) items.Add(Error(FaceMotionDiagnosticCodes.GenerationOwnership, "Skipped a batch path that is not safely owned.", "Inspect the batch manifest before deleting assets.")); }
            AssetDatabase.DeleteAsset(manifestPath); AssetDatabase.SaveAssets(); GeneratedAssetOwnership.TryDeleteEmptyOwnedFolder(root, null); items.Add(Info(FaceMotionDiagnosticCodes.GenerationRolledBack, "FaceMotion-owned batch integration assets were removed and original references restored.")); diagnostics = items; return true;
        }

        private static DirectIntegrationPlan PlanCore(DirectIntegrationRequest request)
        {
            var diagnostics = new List<FaceMotionDiagnostic>();
            if (request == null || request.Avatar == null) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationAvatar, "Select a VRCAvatarDescriptor.", "Select the avatar root."));
            if (request != null && request.Avatar != null && EditorUtility.IsPersistent(request.Avatar)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationPrefabAsset, "Direct integration cannot modify a prefab asset.", "Instantiate the avatar in a scene before integrating."));
            if (request == null || request.Clip == null) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationClip, "Export or select an AnimationClip before integrating.", "Provide a .anim clip."));
            if (request == null || !IsAssetFolder(request.OutputFolder)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationPath, "Output folder must be an existing folder under Assets.", "Choose an existing Assets folder."));
            string stem = Sanitize(request == null ? "FaceMotion" : request.DisplayName);
            string parameter = "FaceMotion_" + stem;
            if (parameter.Length > 256) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationParameterName, "The generated parameter name exceeds VRChat's 256 character limit.", "Use a shorter animation name."));
            if (request != null && request.Avatar != null)
            {
                // A managed reapply must not treat its generated copies as user conflicts.
                if (FindManifest(request.Avatar) == null) ValidateExisting(request.Avatar, request.Clip, parameter, diagnostics);
            }
            if (request != null && IsAssetFolder(request.OutputFolder) && OutputFolderIsUnmanaged(request)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationOutputConflict, "The generated output folder already exists and is not owned by this avatar's integration.", "Choose a different animation name or output folder."));
            return new DirectIntegrationPlan(request, parameter, "FaceMotion " + stem, stem, diagnostics);
        }

        private static FaceMotionDiagnostic UnexpectedPlanDiagnostic(Exception exception, DirectIntegrationRequest request)
        {
            string contextId = BackendId + "/plan" + (request?.Avatar == null ? string.Empty : "/" + request.Avatar.name);
            string detail = "Exception: " + exception.GetType().FullName
                + "\nMessage: " + exception.Message
                + "\nStack trace:\n" + (exception.StackTrace ?? "(no stack trace)")
                + "\nDiagnostic code: " + FaceMotionDiagnosticCodes.GenerationPlanUnexpected
                + "\nBackend: " + BackendId
                + "\nOperation: Plan and Validate Integration"
                + "\nContextId: " + contextId;
            return new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.GenerationPlanUnexpected,
                FaceMotionDiagnosticSeverity.Error,
                detail,
                contextId,
                true,
                "Review the integration settings and retry. If this repeats, report the diagnostic code and technical details.");
        }

        public static DirectIntegrationResult Apply(DirectIntegrationPlan plan)
        {
            if (plan == null) return new DirectIntegrationResult(false, null, new[] { Error(FaceMotionDiagnosticCodes.GenerationPlan, "No integration plan was supplied.", "Plan the integration again.") });
            if (!plan.IsValid) return new DirectIntegrationResult(false, null, plan.Diagnostics);
            var request = plan.Request;
            var diagnostics = new List<FaceMotionDiagnostic>(plan.Diagnostics);
            var old = FindManifest(request.Avatar);
            string carryIntegrationId = null;
            string carryAnimationId = null;
            string carryAvatarFingerprint = null;
            if (old != null)
            {
                var migration = DirectIntegrationManifestMigration.TryMigrateOnUse(old, diagnostics);
                if (migration.Blocked)
                {
                    return new DirectIntegrationResult(false, old, diagnostics);
                }

                if (!MigrationIds.NeedsRepair(old.IntegrationId)) carryIntegrationId = old.IntegrationId;
                carryAnimationId = old.AnimationId;
                carryAvatarFingerprint = old.AvatarFingerprint;
                if (!Rollback(old, out var rollbackDiagnostics)) { diagnostics.AddRange(rollbackDiagnostics); return new DirectIntegrationResult(false, old, diagnostics); }
                AssetDatabase.Refresh();
            }
            var owned = new List<string>();
            string root = request.OutputFolder + "/FaceMotion_" + plan.AssetStem;
            int undoGroup = -1;
            try
            {
                Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Apply FaceMotion VRChat integration");
                Undo.RegisterCompleteObjectUndo(request.Avatar, "Apply FaceMotion VRChat integration");
                if (!AssetDatabase.IsValidFolder(root) && string.IsNullOrEmpty(AssetDatabase.CreateFolder(request.OutputFolder, "FaceMotion_" + plan.AssetStem))) throw new InvalidOperationException("Could not create the FaceMotion output folder.");
                var fx = CloneFx(request.Avatar, root + "/FX.controller", owned);
                InjectFailure("after-fx");
                var parameters = CloneParameters(request.Avatar.expressionParameters, root + "/Parameters.asset", owned);
                InjectFailure("after-parameters");
                var menu = CloneMenu(request.Avatar.expressionsMenu, root + "/Menu.asset", owned);
                var subMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>(); subMenu.name = "FaceMotion";
                AssetDatabase.CreateAsset(subMenu, root + "/FaceMotion.menu.asset"); owned.Add(root + "/FaceMotion.menu.asset");
                AssetDatabase.SaveAssets();
                InjectFailure("after-menu");
                AddParameter(parameters, plan.ParameterName);
                AddMenuControl(subMenu, null, plan.ParameterName, request.DisplayName); AddMenuControl(menu, subMenu);
                var reset = ResetClipBuilder.Create(request.Avatar, request.Clip); reset.name = "Reset";
                AssetDatabase.CreateAsset(reset, root + "/Reset.anim"); owned.Add(root + "/Reset.anim");
                InjectFailure("after-reset");
                AddLayer(fx, plan.LayerName, plan.ParameterName, request.Clip, reset);
                var manifest = ScriptableObject.CreateInstance<DirectIntegrationManifest>(); manifest.name = "FaceMotion Integration Manifest";
                manifest.Avatar = request.Avatar; manifest.AvatarGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(request.Avatar).ToString(); manifest.OriginalFx = GetFx(request.Avatar); manifest.OriginalParameters = request.Avatar.expressionParameters; manifest.OriginalMenu = request.Avatar.expressionsMenu;
                manifest.GeneratedFx = fx; manifest.GeneratedParameters = parameters; manifest.GeneratedMenu = menu; manifest.GeneratedSubMenu = subMenu; manifest.ParameterName = plan.ParameterName; manifest.OwnedAssetPaths = owned.ToArray();
                manifest.SchemaVersion = FaceMotionVersions.IntegrationManifestVersion;
                manifest.BackendId = DirectVRChatIntegration.BackendId;
                manifest.IntegrationId = string.IsNullOrEmpty(carryIntegrationId) ? StableId.New() : carryIntegrationId;
                manifest.State = IntegrationState.Attached;
                manifest.AnimationId = carryAnimationId ?? string.Empty;
                manifest.AvatarFingerprint = carryAvatarFingerprint ?? string.Empty;
                string manifestPath = root + "/Manifest.asset"; AssetDatabase.CreateAsset(manifest, manifestPath); owned.Add(manifestPath); manifest.OwnedAssetPaths = owned.ToArray(); InjectFailure("before-descriptor-assignment");
                SetFx(request.Avatar, fx); request.Avatar.expressionParameters = parameters; request.Avatar.expressionsMenu = menu;
                SessionManifests[request.Avatar] = manifest;
                EditorUtility.SetDirty(request.Avatar); EditorUtility.SetDirty(manifest); AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                diagnostics.Add(Info(FaceMotionDiagnosticCodes.GenerationApplied, "Direct VRChat integration applied using copy-on-write assets."));
                return new DirectIntegrationResult(true, manifest, diagnostics);
            }
            catch (Exception exception)
            {
                // The descriptor is assigned last. On a write failure, remove only paths created by this attempt.
                if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup);
                for (int i = owned.Count - 1; i >= 0; i--) AssetDatabase.DeleteAsset(owned[i]);
                GeneratedAssetOwnership.TryDeleteEmptyOwnedFolder(root, owned);
                AssetDatabase.SaveAssets();
                diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationApply, exception.Message, "No user-owned asset was edited; inspect the output folder and retry."));
                return new DirectIntegrationResult(false, null, diagnostics);
            }
        }

        public static bool Rollback(DirectIntegrationManifest manifest, out IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        {
            var items = new List<FaceMotionDiagnostic>();
            var avatar = DirectIntegrationManifestMigration.ResolveAvatar(manifest);
            if (manifest == null || avatar == null) { items.Add(Error(FaceMotionDiagnosticCodes.GenerationRollback, "The integration manifest or its avatar is missing.", "Restore the avatar references manually.")); diagnostics = items; return false; }
            var migration = DirectIntegrationManifestMigration.TryMigrateOnUse(manifest, items);
            if (migration.Blocked) { diagnostics = items; return false; }
            Undo.RegisterCompleteObjectUndo(avatar, "Rollback FaceMotion VRChat integration");
            SetFx(avatar, manifest.OriginalFx); avatar.expressionParameters = manifest.OriginalParameters; avatar.expressionsMenu = manifest.OriginalMenu;
            EditorUtility.SetDirty(avatar); AssetDatabase.SaveAssets();
            string manifestPath = AssetDatabase.GetAssetPath(manifest);
            string root = Path.GetDirectoryName(manifestPath)?.Replace('\\', '/');
            if (manifest.OwnedAssetPaths != null) for (int i = 0; i < manifest.OwnedAssetPaths.Length; i++)
            {
                string path = manifest.OwnedAssetPaths[i];
                if (path == manifestPath) continue;
                if (!GeneratedAssetOwnership.IsSafeOwnedAssetPath(path, root) || !GeneratedAssetOwnership.IsCanonicalGeneratedFileName(Path.GetFileName(path)))
                {
                    items.Add(Error(FaceMotionDiagnosticCodes.GenerationOwnership, "Skipped an owned path that is outside the manifest folder or not a FaceMotion-generated asset.", "Inspect the manifest before deleting assets."));
                    continue;
                }
                AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.DeleteAsset(manifestPath); AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(root) && AssetDatabase.IsValidFolder(root))
            {
                if (GeneratedAssetOwnership.FolderHasForeignContent(root, null, out var foreign))
                {
                    items.Add(Info("FM-OWNERSHIP-FOREIGN-CONTENT", "The generated folder was retained because it still contains a foreign asset: " + foreign));
                }
                else
                {
                    AssetDatabase.DeleteAsset(root);
                }
            }
            AssetDatabase.SaveAssets();
            SessionManifests.Remove(avatar);
            items.Add(Info(FaceMotionDiagnosticCodes.GenerationRolledBack, "FaceMotion-owned integration assets were removed and original references restored.")); diagnostics = items; return true;
        }

        private static void ValidateExisting(VRCAvatarDescriptor avatar, AnimationClip clip, string parameter, List<FaceMotionDiagnostic> diagnostics)
        {
            var parameters = avatar.expressionParameters;
            if (parameters != null && parameters.FindParameter(parameter) != null) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationParameterConflict, "A parameter with the generated name already exists.", "Rename the animation before planning."));
            if (parameters != null && parameters.CalcTotalCost() + 1 > VRCExpressionParameters.MAX_PARAMETER_COST) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationBudget, "Adding the Bool parameter exceeds VRChat's expression parameter budget.", "Free at least one expression parameter bit."));
            if (avatar.expressionsMenu != null && avatar.expressionsMenu.controls != null && avatar.expressionsMenu.controls.Count >= MenuCapacity) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationMenuCapacity, "The expression menu has no free control slot.", "Free a root menu slot."));
            var fx = GetFx(avatar) as AnimatorController;
            if (fx == null) { diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationFx, "The avatar needs a custom FX AnimatorController.", "Assign an FX controller in the avatar descriptor.")); return; }
            foreach (var p in fx.parameters) if (p.name == parameter) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationAnimatorParameterConflict, "The FX controller already defines the generated parameter.", "Rename the animation before planning."));
            foreach (var layer in fx.layers)
            {
                if (layer.name.StartsWith("FaceMotion ", StringComparison.Ordinal)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationLayerConflict, "The FX controller already has a FaceMotion layer.", "Rollback the prior integration or use a different name."));
                if (layer.stateMachine != null) foreach (var state in layer.stateMachine.states) if (state.state != null && state.state.writeDefaultValues) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationWriteDefaults, "Direct integration requires Write Defaults disabled on every FX state.", "Disable Write Defaults or use a compatible FX controller."));
            }
            foreach (var existing in fx.animationClips) if (existing != null && SharesBinding(existing, clip)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationBindingConflict, "The exported clip animates a binding already used by the FX controller.", "Remove the competing animation binding or use a different target."));
        }

        private static void ValidateBatchExisting(VRCAvatarDescriptor avatar, IReadOnlyList<DirectIntegrationBatchItemRequest> items, IReadOnlyList<string> parameters, List<FaceMotionDiagnostic> diagnostics)
        {
            var expressionParameters = avatar.expressionParameters;
            if (expressionParameters != null)
            {
                if (expressionParameters.CalcTotalCost() + parameters.Count > VRCExpressionParameters.MAX_PARAMETER_COST) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationBudget, "Adding the batch Bool parameters exceeds VRChat's expression parameter budget.", "Free expression parameter bits or reduce the batch."));
                for (int i = 0; i < parameters.Count; i++) if (expressionParameters.FindParameter(parameters[i]) != null) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationParameterConflict, "A parameter with a generated batch name already exists.", "Rename the animation before planning."));
            }
            if (avatar.expressionsMenu != null && avatar.expressionsMenu.controls != null && avatar.expressionsMenu.controls.Count >= MenuCapacity) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationMenuCapacity, "The expression menu has no free control slot for the batch submenu.", "Free a root menu slot."));
            var fx = GetFx(avatar) as AnimatorController;
            if (fx == null) { diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationFx, "The avatar needs a custom FX AnimatorController.", "Assign an FX controller in the avatar descriptor.")); return; }
            foreach (var parameter in parameters) foreach (var current in fx.parameters) if (current.name == parameter) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationAnimatorParameterConflict, "The FX controller already defines a generated batch parameter.", "Rename the animation before planning."));
            foreach (var layer in fx.layers)
            {
                if (layer.name.StartsWith("FaceMotion ", StringComparison.Ordinal)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationLayerConflict, "The FX controller already has a FaceMotion layer.", "Rollback the prior integration or use a different controller."));
                if (layer.stateMachine != null) foreach (var state in layer.stateMachine.states) if (state.state != null && state.state.writeDefaultValues) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationWriteDefaults, "Direct integration requires Write Defaults disabled on every FX state.", "Disable Write Defaults or use a compatible FX controller."));
            }
            for (int i = 0; i < items.Count; i++)
            {
                var clip = items[i] == null ? null : items[i].Clip;
                if (clip == null) continue;
                foreach (var existing in fx.animationClips) if (existing != null && SharesBinding(existing, clip)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationBindingConflict, "A batch clip animates a binding already used by the FX controller.", "Remove the competing animation binding or use a different target."));
                for (int other = 0; other < i; other++) if (items[other] != null && items[other].Clip != null && SharesBinding(items[other].Clip, clip)) diagnostics.Add(Error(FaceMotionDiagnosticCodes.GenerationBindingConflict, "Two batch clips animate the same binding.", "Keep each batch animation's bindings independent."));
            }
        }

        private static DirectBatchIntegrationManifest FindBatchManifest(VRCAvatarDescriptor avatar)
        {
            if (avatar == null) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:DirectBatchIntegrationManifest"))
            {
                var manifest = AssetDatabase.LoadAssetAtPath<DirectBatchIntegrationManifest>(AssetDatabase.GUIDToAssetPath(guid));
                if (manifest != null && (manifest.Avatar == avatar || (!string.IsNullOrEmpty(manifest.AvatarGlobalId) && GlobalObjectId.TryParse(manifest.AvatarGlobalId, out var id) && GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) == avatar))) return manifest;
            }
            return null;
        }

        private static bool BatchMatches(DirectBatchIntegrationManifest manifest, DirectIntegrationBatchRequest request, IReadOnlyList<string> parameters)
        {
            if (manifest == null || request == null || manifest.Items == null || request.Items == null || manifest.Items.Length != request.Items.Count) return false;
            if (!string.Equals(Path.GetDirectoryName(AssetDatabase.GetAssetPath(manifest))?.Replace('\\', '/'), request.OutputFolder + "/FaceMotion_Batch", StringComparison.Ordinal)) return false;
            for (int i = 0; i < manifest.Items.Length; i++)
            {
                var item = request.Items[i];
                if (item == null || item.Clip == null || manifest.Items[i] == null || !string.Equals(manifest.Items[i].ClipPath, AssetDatabase.GetAssetPath(item.Clip), StringComparison.Ordinal) || !string.Equals(manifest.Items[i].DisplayName, item.DisplayName, StringComparison.Ordinal) || !string.Equals(manifest.Items[i].ParameterName, parameters[i], StringComparison.Ordinal)) return false;
            }
            return true;
        }

        private static bool IsBatchGeneratedFileName(string fileName)
        {
            return fileName == "FX.controller" || fileName == "Parameters.asset" || fileName == "Menu.asset" || fileName == "FaceMotion.menu.asset" || fileName == "BatchManifest.asset"
                || (!string.IsNullOrEmpty(fileName) && fileName.StartsWith("Reset_", StringComparison.Ordinal) && fileName.EndsWith(".anim", StringComparison.Ordinal));
        }

        private static AnimatorController CloneFx(VRCAvatarDescriptor avatar, string path, List<string> owned) { if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(GetFx(avatar)), path)) throw new InvalidOperationException("Could not copy the FX controller."); owned.Add(path); return AssetDatabase.LoadAssetAtPath<AnimatorController>(path); }
        private static VRCExpressionParameters CloneParameters(VRCExpressionParameters source, string path, List<string> owned) { var copy = source == null ? ScriptableObject.CreateInstance<VRCExpressionParameters>() : UnityEngine.Object.Instantiate(source); AssetDatabase.CreateAsset(copy, path); owned.Add(path); return copy; }
        private static VRCExpressionsMenu CloneMenu(VRCExpressionsMenu source, string path, List<string> owned) { var copy = source == null ? ScriptableObject.CreateInstance<VRCExpressionsMenu>() : UnityEngine.Object.Instantiate(source); AssetDatabase.CreateAsset(copy, path); owned.Add(path); return copy; }
        private static void AddParameter(VRCExpressionParameters parameters, string name) { var list = new List<VRCExpressionParameters.Parameter>(parameters.parameters ?? Array.Empty<VRCExpressionParameters.Parameter>()); list.Add(new VRCExpressionParameters.Parameter { name = name, valueType = VRCExpressionParameters.ValueType.Bool, saved = false, defaultValue = 0f }); parameters.parameters = list.ToArray(); EditorUtility.SetDirty(parameters); }
        private static void AddMenuControl(VRCExpressionsMenu menu, VRCExpressionsMenu submenu, string parameter = null, string label = null) { var controls = menu.controls ?? new List<VRCExpressionsMenu.Control>(); controls.Add(new VRCExpressionsMenu.Control { name = submenu == null ? label : "FaceMotion", type = submenu == null ? VRCExpressionsMenu.Control.ControlType.Toggle : VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = submenu, parameter = new VRCExpressionsMenu.Control.Parameter { name = parameter }, value = 1f }); menu.controls = controls; EditorUtility.SetDirty(menu); }
        private static void AddLayer(AnimatorController fx, string layerName, string parameter, AnimationClip clip, AnimationClip reset) { fx.AddParameter(parameter, AnimatorControllerParameterType.Bool); var machine = new AnimatorStateMachine { name = layerName }; AssetDatabase.AddObjectToAsset(machine, fx); var off = machine.AddState("Off"); off.motion = reset; off.writeDefaultValues = false; var on = machine.AddState("On"); on.motion = clip; on.writeDefaultValues = false; var enter = machine.AddEntryTransition(on); enter.AddCondition(AnimatorConditionMode.If, 0f, parameter); var leave = on.AddTransition(off); leave.hasExitTime = false; leave.duration = 0f; leave.AddCondition(AnimatorConditionMode.IfNot, 0f, parameter); var activate = off.AddTransition(on); activate.hasExitTime = false; activate.duration = 0f; activate.AddCondition(AnimatorConditionMode.If, 0f, parameter); fx.AddLayer(new AnimatorControllerLayer { name = layerName, defaultWeight = 1f, stateMachine = machine }); EditorUtility.SetDirty(fx); }
        private static RuntimeAnimatorController GetFx(VRCAvatarDescriptor avatar) { foreach (var layer in avatar.baseAnimationLayers) if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX) return layer.animatorController; return null; }
        private static void SetFx(VRCAvatarDescriptor avatar, RuntimeAnimatorController controller) { var layers = avatar.baseAnimationLayers; for (int i = 0; i < layers.Length; i++) if (layers[i].type == VRCAvatarDescriptor.AnimLayerType.FX) { var layer = layers[i]; layer.isDefault = false; layer.animatorController = controller; layers[i] = layer; avatar.baseAnimationLayers = layers; return; } }
        private static DirectIntegrationManifest FindManifest(VRCAvatarDescriptor avatar) { if (avatar != null && SessionManifests.TryGetValue(avatar, out var current) && current != null) return current; foreach (var guid in AssetDatabase.FindAssets("Manifest")) { var value = AssetDatabase.LoadAssetAtPath<DirectIntegrationManifest>(AssetDatabase.GUIDToAssetPath(guid)); if (value != null && DirectIntegrationManifestMigration.ResolveAvatar(value) == avatar) return value; } return null; }
        private static bool SharesBinding(AnimationClip a, AnimationClip b) { var bindings = new HashSet<EditorCurveBinding>(AnimationUtility.GetCurveBindings(a)); foreach (var binding in AnimationUtility.GetCurveBindings(b)) if (bindings.Contains(binding)) return true; return false; }
        private static bool OutputFolderIsUnmanaged(DirectIntegrationRequest request) { string root = request.OutputFolder + "/FaceMotion_" + Sanitize(request.DisplayName); if (!AssetDatabase.IsValidFolder(root)) return false; var manifest = FindManifest(request.Avatar); return manifest == null || !string.Equals(Path.GetDirectoryName(AssetDatabase.GetAssetPath(manifest))?.Replace('\\', '/'), root, StringComparison.Ordinal); }
        private static bool IsAssetFolder(string path) { if (string.IsNullOrEmpty(path)) return false; path = path.Replace('\\', '/'); if (path != "Assets" && !path.StartsWith("Assets/", StringComparison.Ordinal)) return false; foreach (var part in path.Split('/')) if (part == "." || part == "..") return false; return AssetDatabase.IsValidFolder(path); }
        private static void InjectFailure(string stage) { ApplyFailureInjector?.Invoke(stage); }
        private static string Sanitize(string value) { var chars = (value ?? "FaceMotion").ToCharArray(); for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_') chars[i] = '_'; return new string(chars); }
        internal static bool HasBlocking(IReadOnlyList<FaceMotionDiagnostic> items) { for (int i = 0; i < items.Count; i++) if (items[i].Blocking) return true; return false; }
        private static FaceMotionDiagnostic Error(string code, string message, string fix) { return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Error, message, string.Empty, true, fix); }
        private static FaceMotionDiagnostic Info(string code, string message) { return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Info, message, string.Empty, false, string.Empty); }
    }
}
