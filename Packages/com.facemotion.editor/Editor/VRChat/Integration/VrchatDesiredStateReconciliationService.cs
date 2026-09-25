using System;
using System.Collections.Generic;
using System.IO;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.Export;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.VRChat.Integration
{
    public enum VrchatDesiredStateAction { Keep, Add, Remove }
    public enum VrchatDesiredStateReconciliationOutcome { PreflightFailed, Succeeded, FailedRolledBack, FailedRollbackFailed, Failed }

    public sealed class VrchatDesiredStateReconciliationRequest
    {
        public VrchatDesiredStateReconciliationRequest(VRCAvatarDescriptor avatar, FaceMotionProject project, IReadOnlyList<string> desiredAnimationIds, string outputFolder, IModularAvatarIntegrationBackend backend, Action<VRCAvatarDescriptor> invalidateManagedState = null)
        { Avatar = avatar; Project = project; DesiredAnimationIds = desiredAnimationIds ?? Array.Empty<string>(); OutputFolder = outputFolder; Backend = backend; InvalidateManagedState = invalidateManagedState; }
        public VRCAvatarDescriptor Avatar { get; } public FaceMotionProject Project { get; } public IReadOnlyList<string> DesiredAnimationIds { get; } public string OutputFolder { get; } public IModularAvatarIntegrationBackend Backend { get; }
        /// <summary>Called after any attempted mutation, including a rollback attempt.</summary>
        public Action<VRCAvatarDescriptor> InvalidateManagedState { get; }
    }

    public sealed class VrchatDesiredStateReconciliationItem
    {
        public VrchatDesiredStateReconciliationItem(string animationId, string parameterName, VrchatDesiredStateAction action)
        { AnimationId = animationId ?? string.Empty; ParameterName = parameterName ?? string.Empty; Action = action; }
        public string AnimationId { get; } public string ParameterName { get; } public VrchatDesiredStateAction Action { get; }
    }

    public sealed class VrchatDesiredStateReconciliationResult
    {
        public VrchatDesiredStateReconciliationResult(bool succeeded, VrchatDesiredStateReconciliationOutcome outcome, IReadOnlyList<VrchatDesiredStateReconciliationItem> items, IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        { Succeeded = succeeded; Outcome = outcome; Items = items ?? Array.Empty<VrchatDesiredStateReconciliationItem>(); Diagnostics = diagnostics ?? Array.Empty<FaceMotionDiagnostic>(); }
        public bool Succeeded { get; } public VrchatDesiredStateReconciliationOutcome Outcome { get; } public IReadOnlyList<VrchatDesiredStateReconciliationItem> Items { get; } public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }
    }

    /// <summary>MA desired-state reconciliation. Identity is stable AnimationId, then the canonical generated parameter for legacy manifests; display names are never identity.</summary>
    public static class VrchatDesiredStateReconciliationService
    {
        public static VrchatDesiredStateReconciliationResult Execute(VrchatDesiredStateReconciliationRequest request)
        {
            var diagnostics = new List<FaceMotionDiagnostic>(); var actions = new List<VrchatDesiredStateReconciliationItem>(); var previews = new List<AnimationClip>(); bool mayHaveMutated = false;
            try
            {
                if (request == null || request.Avatar == null || request.Project == null || request.Backend == null)
                    return Finish(false, VrchatDesiredStateReconciliationOutcome.PreflightFailed, actions, diagnostics, "Select an avatar, project, and Modular Avatar backend before reconciling.");

                diagnostics.AddRange(ProjectValidator.Validate(request.Project).Diagnostics);
                var desired = SnapshotDesired(request, diagnostics);
                var owned = request.Backend.InspectManagedState(request.Avatar); diagnostics.AddRange(owned.Diagnostics);
                if (HasBlocking(diagnostics)) return Finish(false, VrchatDesiredStateReconciliationOutcome.PreflightFailed, actions, diagnostics, null);

                string outputFolder = OneClickIntegrationService.ResolveIntegrationOutputFolder(request.OutputFolder);
                var candidates = new List<Candidate>();
                for (var i = 0; i < desired.Count; i++)
                {
                    var animation = desired[i]; string path = OneClickIntegrationService.ResolveExportPath(new OneClickIntegrationRequest(request.Avatar, animation, request.Project, outputFolder), diagnostics);
                    var export = AnimationClipExporter.Validate(animation, path); diagnostics.AddRange(export.Diagnostics); ValidateExportOwnership(animation, path, diagnostics);
                    if (export.IsValid) { var preview = AnimationClipExporter.CreatePreview(animation); previews.Add(preview); candidates.Add(new Candidate(animation, path, preview)); }
                }
                if (HasBlocking(diagnostics) || candidates.Count != desired.Count) return Finish(false, VrchatDesiredStateReconciliationOutcome.PreflightFailed, actions, diagnostics, null);

                // This pure pass supplies canonical legacy parameters. Do not treat its conflicts as final-state conflicts.
                var canonical = request.Backend.PlanBatch(Requests(request, candidates, outputFolder, true));
                if (canonical == null)
                {
                    diagnostics.Add(Error("The Modular Avatar backend returned no canonical parameter plan."));
                }
                else
                {
                    if (canonical.Items.Count != candidates.Count) diagnostics.Add(Error("The Modular Avatar backend returned an incomplete canonical parameter plan."));
                    for (var i = 0; i < canonical.Items.Count; i++) if (canonical.Items[i] != null) diagnostics.AddRange(canonical.Items[i].Diagnostics);
                }
                if (HasBlocking(diagnostics)) return Finish(false, VrchatDesiredStateReconciliationOutcome.PreflightFailed, actions, diagnostics, null);

                var byId = new Dictionary<string, ModularAvatarManagedState>(StringComparer.Ordinal);
                var byLegacyParameter = new Dictionary<string, ModularAvatarManagedState>(StringComparer.Ordinal);
                for (var i = 0; i < owned.Items.Count; i++)
                {
                    var state = owned.Items[i];
                    if (state.HasAnimationId) { if (byId.ContainsKey(state.AnimationId)) diagnostics.Add(Error("Two proven-owned integrations claim animation ID " + state.AnimationId + ".")); else byId.Add(state.AnimationId, state); }
                    else if (!string.IsNullOrEmpty(state.ParameterName)) { if (byLegacyParameter.ContainsKey(state.ParameterName)) diagnostics.Add(Error("Two legacy integrations claim parameter " + state.ParameterName + ".")); else byLegacyParameter.Add(state.ParameterName, state); }
                }
                var matched = new HashSet<ModularAvatarManagedState>(); var adds = new List<Candidate>();
                for (var i = 0; i < candidates.Count; i++)
                {
                    var candidate = candidates[i]; var parameter = canonical.Items[i] == null ? string.Empty : canonical.Items[i].ParameterName;
                    if (byId.TryGetValue(candidate.Animation.AnimationId, out var stable)) { matched.Add(stable); actions.Add(new VrchatDesiredStateReconciliationItem(candidate.Animation.AnimationId, stable.ParameterName, VrchatDesiredStateAction.Keep)); }
                    else if (byLegacyParameter.TryGetValue(parameter, out var legacy)) { matched.Add(legacy); actions.Add(new VrchatDesiredStateReconciliationItem(candidate.Animation.AnimationId, legacy.ParameterName, VrchatDesiredStateAction.Keep)); }
                    else { actions.Add(new VrchatDesiredStateReconciliationItem(candidate.Animation.AnimationId, parameter, VrchatDesiredStateAction.Add)); adds.Add(candidate); }
                }
                var removals = new List<string>();
                for (var i = 0; i < owned.Items.Count; i++) if (!matched.Contains(owned.Items[i])) { removals.Add(owned.Items[i].ParameterName); actions.Add(new VrchatDesiredStateReconciliationItem(owned.Items[i].AnimationId, owned.Items[i].ParameterName, VrchatDesiredStateAction.Remove)); }
                diagnostics.AddRange(request.Backend.ValidateRemovals(request.Avatar, removals).Diagnostics);

                // Validate additions against the state after the proven-owned removals, without mutating it.
                var finalPlan = adds.Count == 0 ? null : PlanFinalState(request, adds, removals, outputFolder);
                if (finalPlan != null) for (var i = 0; i < finalPlan.Items.Count; i++) if (finalPlan.Items[i] != null) diagnostics.AddRange(finalPlan.Items[i].Diagnostics);
                if (finalPlan != null && (!finalPlan.IsValid || finalPlan.Items.Count != adds.Count) && !HasBlocking(diagnostics)) diagnostics.Add(Error("The final Modular Avatar desired state could not be validated."));
                if (HasBlocking(diagnostics)) return Finish(false, VrchatDesiredStateReconciliationOutcome.PreflightFailed, actions, diagnostics, null);

                // Capture before export: output assets are part of the transaction, not preflight.
                object snapshot = (request.Backend as IModularAvatarIntegrationRollbackBackend)?.CaptureRollbackSnapshot(request.Avatar);
                var exportSnapshot = CaptureExports(adds);
                var createdFolders = new List<string>();
                // Export and mutation happen only after every clip, ownership rule, removal, and final binding check succeeded.
                try
                {
                    // FaceMotion's canonical output root is created here, inside the transaction, never during preflight.
                    if (adds.Count > 0 && !CreateManagedOutputRoot(outputFolder, createdFolders))
                        return MutationFailure(request, actions, diagnostics, snapshot, exportSnapshot, "FaceMotion's default Modular Avatar output folder could not be created.", createdFolders);
                    if (createdFolders.Count > 0) mayHaveMutated = true;

                    for (var i = 0; i < adds.Count; i++) { mayHaveMutated = true; var exported = AnimationClipExporter.Export(adds[i].Animation, adds[i].ExportPath); diagnostics.AddRange(exported.Diagnostics); if (!exported.Succeeded) return MutationFailure(request, actions, diagnostics, snapshot, exportSnapshot, null, createdFolders); adds[i].Clip = exported.Clip; ExportedClipRegistry.Record(adds[i].Animation.AnimationId, adds[i].ExportPath); }

                    if (removals.Count > 0) { mayHaveMutated = true; var removed = request.Backend.RemoveAnimations(request.Avatar, removals); diagnostics.AddRange(removed.Diagnostics); if (!removed.Succeeded) return MutationFailure(request, actions, diagnostics, snapshot, exportSnapshot, null, createdFolders); }
                    if (adds.Count > 0) { mayHaveMutated = true; var applied = request.Backend.ApplyBatch(RebindPlans(finalPlan, adds)); diagnostics.AddRange(applied.Diagnostics); if (!applied.Succeeded) return MutationFailure(request, actions, diagnostics, snapshot, exportSnapshot, null, createdFolders); }
                }
                catch (Exception exception) { return MutationFailure(request, actions, diagnostics, snapshot, exportSnapshot, exception.Message, createdFolders); }
                return new VrchatDesiredStateReconciliationResult(true, VrchatDesiredStateReconciliationOutcome.Succeeded, actions, diagnostics);
            }
            finally { DestroyPreviews(previews); if (mayHaveMutated) request?.InvalidateManagedState?.Invoke(request.Avatar); }
        }

        private static VrchatDesiredStateReconciliationResult MutationFailure(VrchatDesiredStateReconciliationRequest request, List<VrchatDesiredStateReconciliationItem> actions, List<FaceMotionDiagnostic> diagnostics, object snapshot, List<ExportSnapshotEntry> exportSnapshot, string thrownMessage, List<string> createdFolders)
        {
            if (!string.IsNullOrEmpty(thrownMessage)) diagnostics.Add(Error("Reconciliation failed unexpectedly during mutation: " + thrownMessage));
            if (exportSnapshot != null)
            {
                try { RestoreExports(exportSnapshot); }
                catch (Exception exportRestoreException) { diagnostics.Add(Error("Exported clip rollback could not be completed: " + exportRestoreException.Message)); }
            }
            // Folders this transaction created are removed last, once the restored exports are gone again.
            try { DeleteCreatedFolders(createdFolders); }
            catch (Exception folderRestoreException) { diagnostics.Add(Error("Output folder rollback could not be completed: " + folderRestoreException.Message)); }
            var rollback = request.Backend as IModularAvatarIntegrationRollbackBackend;
            if (rollback == null || snapshot == null) return new VrchatDesiredStateReconciliationResult(false, VrchatDesiredStateReconciliationOutcome.Failed, actions, diagnostics);
            var restored = rollback.RestoreRollbackSnapshot(snapshot); diagnostics.AddRange(restored.Diagnostics);
            return new VrchatDesiredStateReconciliationResult(false, restored.Succeeded ? VrchatDesiredStateReconciliationOutcome.FailedRolledBack : VrchatDesiredStateReconciliationOutcome.FailedRollbackFailed, actions, diagnostics);
        }

        /// <summary>Snapshots the pre-transaction file state of every planned export destination so rollback can remove created clips and restore overwritten ones byte-for-byte.</summary>
        private static List<ExportSnapshotEntry> CaptureExports(List<Candidate> adds)
        {
            var result = new List<ExportSnapshotEntry>();
            for (var i = 0; i < adds.Count; i++) result.Add(ExportSnapshotEntry.Capture(adds[i].Animation.AnimationId, adds[i].ExportPath));
            return result;
        }

        /// <summary>Deletes exported clips this transaction created and restores clips it overwrote, then unregisters created clips.</summary>
        private static void RestoreExports(List<ExportSnapshotEntry> exports)
        {
            for (var i = 0; i < exports.Count; i++)
            {
                var entry = exports[i];
                string full = FullPath(entry.AssetPath);
                if (entry.ExistedBefore)
                {
                    if (!File.Exists(full) || !ByteEquals(full, entry.Bytes) || entry.MetaBytes != null && (!File.Exists(full + ".meta") || !ByteEquals(full + ".meta", entry.MetaBytes)))
                    {
                        File.WriteAllBytes(full, entry.Bytes);
                        if (entry.MetaBytes != null) File.WriteAllBytes(full + ".meta", entry.MetaBytes);
                        AssetDatabase.ImportAsset(entry.AssetPath, ImportAssetOptions.ForceUpdate);
                    }
                }
                else
                {
                    if (File.Exists(full)) { File.SetAttributes(full, FileAttributes.Normal); File.Delete(full); }
                    if (File.Exists(full + ".meta")) { File.SetAttributes(full + ".meta", FileAttributes.Normal); File.Delete(full + ".meta"); }
                    ExportedClipRegistry.Remove(entry.AnimationId);
                }
            }
            AssetDatabase.Refresh();
        }

        private static string FullPath(string assetPath)
        {
            var normalized = (assetPath ?? string.Empty).Replace('\\', '/');
            var relative = normalized.StartsWith("Assets/", StringComparison.Ordinal) ? normalized.Substring("Assets/".Length) : normalized;
            return Application.dataPath.Replace('\\', '/') + "/" + relative;
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

        private sealed class ExportSnapshotEntry
        {
            private ExportSnapshotEntry(string animationId, string assetPath, byte[] bytes, byte[] metaBytes)
            { AnimationId = animationId; AssetPath = assetPath; Bytes = bytes; MetaBytes = metaBytes; ExistedBefore = bytes != null; }

            public static ExportSnapshotEntry Capture(string animationId, string assetPath)
            {
                string full = FullPath(assetPath);
                return new ExportSnapshotEntry(animationId, assetPath, File.Exists(full) ? File.ReadAllBytes(full) : null, File.Exists(full + ".meta") ? File.ReadAllBytes(full + ".meta") : null);
            }

            public string AnimationId { get; }
            public string AssetPath { get; }
            public bool ExistedBefore { get; }
            public byte[] Bytes { get; }
            public byte[] MetaBytes { get; }
        }
        private static ModularAvatarIntegrationBatchPlan PlanFinalState(VrchatDesiredStateReconciliationRequest request, List<Candidate> adds, List<string> removals, string outputFolder) => request.Backend is IModularAvatarDesiredStateBackend final ? final.PlanFinalState(Requests(request, adds, outputFolder, true), removals) : request.Backend.PlanBatch(Requests(request, adds, outputFolder, true));
        private static ModularAvatarIntegrationBatchPlan RebindPlans(ModularAvatarIntegrationBatchPlan plan, List<Candidate> adds) { var result = new List<ModularAvatarIntegrationPlan>(); for (var i = 0; i < plan.Items.Count; i++) { var item = plan.Items[i]; var request = new ModularAvatarIntegrationRequest(item.Request.Avatar, adds[i].Clip, item.Request.OutputFolder, item.Request.DisplayName, item.Request.AnimationId); result.Add(new ModularAvatarIntegrationPlan(request, item.ParameterName, item.ObjectName, item.RootName, item.Diagnostics, item.PartnerParameters, item.SharedBindings)); } return new ModularAvatarIntegrationBatchPlan(result); }
        private static List<ModularAvatarIntegrationRequest> Requests(VrchatDesiredStateReconciliationRequest request, List<Candidate> candidates, string outputFolder, bool previews) { var result = new List<ModularAvatarIntegrationRequest>(); for (var i = 0; i < candidates.Count; i++) result.Add(new ModularAvatarIntegrationRequest(request.Avatar, previews ? candidates[i].Preview : candidates[i].Clip, outputFolder, candidates[i].Animation.DisplayName, candidates[i].Animation.AnimationId)); return result; }
        /// <summary>
        /// Creates FaceMotion's canonical default output root while the transaction is running.
        /// Preflight never writes, and an explicit custom path is never created for the user.
        /// </summary>
        private static bool CreateManagedOutputRoot(string outputFolder, List<string> createdFolders)
        {
            if (AssetDatabase.IsValidFolder(outputFolder)) return true;
            if (!OneClickIntegrationService.IsCanonicalDefaultOutputFolder(outputFolder)) return false;
            var segments = outputFolder.Replace('\\', '/').Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(current, segments[i]))) return false;
                    createdFolders.Add(next);
                }
                current = next;
            }
            return AssetDatabase.IsValidFolder(outputFolder);
        }

        private static void DeleteCreatedFolders(List<string> createdFolders)
        {
            if (createdFolders == null) return;
            for (var i = createdFolders.Count - 1; i >= 0; i--)
            {
                var folder = createdFolders[i];
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                string full = FullPath(folder);
                if (Directory.Exists(full) && Directory.GetFileSystemEntries(full).Length > 0)
                    throw new InvalidOperationException("The transaction created '" + folder + "' but it is not empty after rollback.");
                AssetDatabase.DeleteAsset(folder);
            }
        }
        private static VrchatDesiredStateReconciliationResult Finish(bool succeeded, VrchatDesiredStateReconciliationOutcome outcome, List<VrchatDesiredStateReconciliationItem> actions, List<FaceMotionDiagnostic> diagnostics, string error) { if (!string.IsNullOrEmpty(error)) diagnostics.Add(Error(error)); return new VrchatDesiredStateReconciliationResult(succeeded, outcome, actions, diagnostics); }
        private static List<FaceMotionAnimationData> SnapshotDesired(VrchatDesiredStateReconciliationRequest request, List<FaceMotionDiagnostic> diagnostics) { var requested = new HashSet<string>(StringComparer.Ordinal); for (var i = 0; i < request.DesiredAnimationIds.Count; i++) if (!requested.Add(request.DesiredAnimationIds[i] ?? string.Empty)) diagnostics.Add(Error("Desired animation IDs must be unique.")); var result = new List<FaceMotionAnimationData>(); for (var i = 0; i < request.Project.Animations.Count; i++) { var animation = request.Project.Animations[i]; if (animation != null && requested.Remove(animation.AnimationId)) result.Add(animation); } result.Sort((a, b) => string.CompareOrdinal(a.AnimationId, b.AnimationId)); foreach (var missing in requested) diagnostics.Add(Error("Desired animation does not exist: " + missing)); return result; }
        private static void ValidateExportOwnership(FaceMotionAnimationData animation, string path, List<FaceMotionDiagnostic> diagnostics) { var main = AssetDatabase.LoadMainAssetAtPath(path); if (main != null && !(main is AnimationClip)) diagnostics.Add(Error("The export destination is occupied by a non-animation asset: " + path)); else if (main is AnimationClip && !ExportedClipRegistry.IsOwned(animation.AnimationId, path)) diagnostics.Add(Error("The export destination contains an AnimationClip not owned by this animation: " + path)); }
        private static void DestroyPreviews(List<AnimationClip> previews) { for (var i = 0; i < previews.Count; i++) if (previews[i] != null) UnityEngine.Object.DestroyImmediate(previews[i]); }
        private static bool HasBlocking(IReadOnlyList<FaceMotionDiagnostic> diagnostics) { for (var i = 0; i < diagnostics.Count; i++) if (diagnostics[i].Blocking) return true; return false; }
        private static FaceMotionDiagnostic Error(string message) => new FaceMotionDiagnostic(FaceMotionDiagnosticCodes.BatchPreflightFailed, FaceMotionDiagnosticSeverity.Error, message, "ma-desired-state", true, "Fix the listed item, then run reconciliation again.");
        private sealed class Candidate { public Candidate(FaceMotionAnimationData animation, string exportPath, AnimationClip preview) { Animation = animation; ExportPath = exportPath; Preview = preview; } public FaceMotionAnimationData Animation; public string ExportPath; public AnimationClip Preview; public AnimationClip Clip; }
    }
}
