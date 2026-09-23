using System;
using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.Export;
using FaceMotion.Integration;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.VRChat.Integration
{
    public enum BatchIntegrationStage { NotStarted, Export, Plan, Validate, Apply, Done }

    /// <summary>Immutable K2 request. Items are animation-ID snapshots, never list indexes.</summary>
    public sealed class BatchIntegrationRequest
    {
        public BatchIntegrationRequest(VRCAvatarDescriptor avatar, FaceMotionProject project, IReadOnlyList<string> animationIds, string outputFolder)
        { Avatar = avatar; Project = project; AnimationIds = animationIds ?? Array.Empty<string>(); OutputFolder = string.IsNullOrEmpty(outputFolder) ? OneClickIntegrationService.DefaultOutputFolder : outputFolder; }
        public VRCAvatarDescriptor Avatar { get; }
        public FaceMotionProject Project { get; }
        public IReadOnlyList<string> AnimationIds { get; }
        public string OutputFolder { get; }
    }

    public sealed class BatchIntegrationItemResult
    {
        internal BatchIntegrationItemResult(string animationId, string displayName, string exportPath, bool succeeded, IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        { AnimationId = animationId ?? string.Empty; DisplayName = displayName ?? string.Empty; ExportPath = exportPath ?? string.Empty; Succeeded = succeeded; Diagnostics = diagnostics ?? Array.Empty<FaceMotionDiagnostic>(); }
        public string AnimationId { get; }
        public string DisplayName { get; }
        public string ExportPath { get; }
        public bool Succeeded { get; }
        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }
    }

    public sealed class BatchIntegrationResult
    {
        internal BatchIntegrationResult(BatchIntegrationStage stage, bool succeeded, string backend, IReadOnlyList<BatchIntegrationItemResult> items, IReadOnlyList<FaceMotionDiagnostic> diagnostics, UnityEngine.Object generatedObject)
        { Stage = stage; Succeeded = succeeded; Backend = backend ?? string.Empty; Items = items ?? Array.Empty<BatchIntegrationItemResult>(); Diagnostics = diagnostics ?? Array.Empty<FaceMotionDiagnostic>(); GeneratedObject = generatedObject; }
        public BatchIntegrationStage Stage { get; }
        public bool Succeeded { get; }
        public string Backend { get; }
        public IReadOnlyList<BatchIntegrationItemResult> Items { get; }
        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }
        public UnityEngine.Object GeneratedObject { get; }
    }

    /// <summary>Coordinates K2 exports and one backend-wide plan/apply without duplicating J4 export safety.</summary>
    public static class BatchIntegrationService
    {
        public static BatchIntegrationResult Execute(BatchIntegrationRequest request, Action<BatchIntegrationStage, int, int> progress = null)
        {
            var diagnostics = new List<FaceMotionDiagnostic>();
            if (request == null)
            {
                diagnostics.Add(Error(FaceMotionDiagnosticCodes.BatchNoSelection, "No batch integration request was supplied."));
                return new BatchIntegrationResult(BatchIntegrationStage.NotStarted, false, string.Empty, Array.Empty<BatchIntegrationItemResult>(), diagnostics, null);
            }
            var items = Snapshot(request, diagnostics);
            if (request == null || request.Avatar == null) diagnostics.Add(Error(FaceMotionDiagnosticCodes.OneClickNoAvatar, "Select the destination avatar before batch integration."));
            if (items.Count == 0) diagnostics.Add(Error(FaceMotionDiagnosticCodes.BatchNoSelection, "Check one or more animations before batch integration."));
            var backend = IntegrationBackendSelectionStore.Load();
            if (backend == IntegrationBackendSelection.ModularAvatar && ModularAvatarIntegrationBackendLocator.Create() == null) diagnostics.Add(Error(FaceMotionDiagnosticCodes.OneClickBackendUnavailable, "The selected Modular Avatar backend is unavailable."));

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var preflight = OneClickIntegrationService.Preflight(new OneClickIntegrationRequest(request.Avatar, item.Animation, request.Project, request.OutputFolder));
                item.ExportPath = preflight.ExportPath;
                item.Diagnostics.AddRange(preflight.Diagnostics);
                diagnostics.AddRange(preflight.Diagnostics);
            }
            AddDuplicateExportPathDiagnostics(items, diagnostics);
            if (HasBlocking(diagnostics)) return Result(BatchIntegrationStage.Validate, false, backend, items, diagnostics, null);

            // Export every validated candidate before backend planning; no backend Apply runs unless all plans validate.
            for (int i = 0; i < items.Count; i++)
            {
                progress?.Invoke(BatchIntegrationStage.Export, i + 1, items.Count);
                var exported = AnimationClipExporter.Export(items[i].Animation, items[i].ExportPath);
                items[i].Diagnostics.AddRange(exported.Diagnostics); diagnostics.AddRange(exported.Diagnostics);
                if (!exported.Succeeded)
                {
                    diagnostics.Add(Error(FaceMotionDiagnosticCodes.BatchPreflightFailed, "Batch export failed; no integration was applied."));
                    return Result(BatchIntegrationStage.Export, false, backend, items, diagnostics, null);
                }
                items[i].Clip = exported.Clip;
                ExportedClipRegistry.Record(items[i].Animation.AnimationId, items[i].ExportPath);
            }

            progress?.Invoke(BatchIntegrationStage.Plan, items.Count, items.Count);
            if (backend == IntegrationBackendSelection.Direct)
            {
                var candidates = new List<DirectIntegrationBatchItemRequest>();
                for (int i = 0; i < items.Count; i++) candidates.Add(new DirectIntegrationBatchItemRequest(items[i].Clip, Stem(request, items[i].Animation)));
                var plan = DirectVRChatIntegration.PlanBatch(new DirectIntegrationBatchRequest(request.Avatar, candidates, request.OutputFolder));
                diagnostics.AddRange(plan.Diagnostics);
                if (!plan.IsValid) return Result(BatchIntegrationStage.Validate, false, backend, items, diagnostics, null);
                progress?.Invoke(BatchIntegrationStage.Validate, items.Count, items.Count);
                progress?.Invoke(BatchIntegrationStage.Apply, 1, 1);
                var applied = DirectVRChatIntegration.ApplyBatch(plan);
                diagnostics.AddRange(applied.Diagnostics);
                return Result(applied.Succeeded ? BatchIntegrationStage.Done : BatchIntegrationStage.Apply, applied.Succeeded, backend, items, diagnostics, applied.Manifest);
            }

            var ma = ModularAvatarIntegrationBackendLocator.Create();
            var requests = new List<ModularAvatarIntegrationRequest>();
            for (int i = 0; i < items.Count; i++) requests.Add(new ModularAvatarIntegrationRequest(request.Avatar, items[i].Clip, request.OutputFolder, Stem(request, items[i].Animation)));
            var maPlan = ma.PlanBatch(requests);
            for (int i = 0; i < maPlan.Items.Count; i++) diagnostics.AddRange(maPlan.Items[i].Diagnostics);
            if (!maPlan.IsValid) return Result(BatchIntegrationStage.Validate, false, backend, items, diagnostics, null);
            progress?.Invoke(BatchIntegrationStage.Validate, items.Count, items.Count);
            progress?.Invoke(BatchIntegrationStage.Apply, 1, items.Count);
            var maApplied = ma.ApplyBatch(maPlan);
            diagnostics.AddRange(maApplied.Diagnostics);
            UnityEngine.Object generated = maApplied.Items.Count == 0 ? null : maApplied.Items[0].Manifest;
            return Result(maApplied.Succeeded ? BatchIntegrationStage.Done : BatchIntegrationStage.Apply, maApplied.Succeeded, backend, items, diagnostics, generated);
        }

        private static List<PreparedItem> Snapshot(BatchIntegrationRequest request, List<FaceMotionDiagnostic> diagnostics)
        {
            var result = new List<PreparedItem>();
            if (request == null || request.Project == null) return result;
            var ids = new HashSet<string>(request.AnimationIds, StringComparer.Ordinal);
            // Project order is the deterministic batch order regardless of checkbox click order.
            for (int i = 0; i < request.Project.Animations.Count; i++)
            {
                var animation = request.Project.Animations[i];
                if (animation == null || !ids.Remove(animation.AnimationId)) continue;
                result.Add(new PreparedItem(animation));
            }
            foreach (var missing in ids) diagnostics.Add(Error(FaceMotionDiagnosticCodes.BatchPreflightFailed, "A checked animation no longer exists: " + missing));
            return result;
        }

        private static BatchIntegrationResult Result(BatchIntegrationStage stage, bool succeeded, IntegrationBackendSelection backend, List<PreparedItem> prepared, List<FaceMotionDiagnostic> diagnostics, UnityEngine.Object generated)
        {
            var results = new List<BatchIntegrationItemResult>();
            for (int i = 0; i < prepared.Count; i++) results.Add(new BatchIntegrationItemResult(prepared[i].Animation.AnimationId, prepared[i].Animation.DisplayName, prepared[i].ExportPath, succeeded, prepared[i].Diagnostics));
            return new BatchIntegrationResult(stage, succeeded, backend == IntegrationBackendSelection.ModularAvatar ? OneClickIntegrationService.ModularAvatarBackendId : DirectVRChatIntegration.BackendId, results, diagnostics, generated);
        }

        private static string Stem(BatchIntegrationRequest request, FaceMotionAnimationData animation)
        {
            return OneClickIntegrationService.BuildStem(new OneClickIntegrationRequest(request.Avatar, animation, request.Project, request.OutputFolder));
        }
        private static void AddDuplicateExportPathDiagnostics(List<PreparedItem> items, List<FaceMotionDiagnostic> diagnostics)
        {
            var paths = new Dictionary<string, List<PreparedItem>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (string.IsNullOrEmpty(item.ExportPath)) continue;
                if (!paths.TryGetValue(item.ExportPath, out var collisions))
                {
                    collisions = new List<PreparedItem>();
                    paths.Add(item.ExportPath, collisions);
                }
                collisions.Add(item);
            }

            foreach (var pair in paths)
            {
                if (pair.Value.Count < 2) continue;
                string names = string.Join("\n", pair.Value.ConvertAll(item => "- " + (string.IsNullOrEmpty(item.Animation.DisplayName) ? "(unnamed)" : item.Animation.DisplayName)));
                var details = new Dictionary<string, string>
                {
                    { FaceMotionDiagnosticDetailKeys.ConflictExportPath, pair.Key },
                    { FaceMotionDiagnosticDetailKeys.ConflictAnimationNames, names }
                };
                var collision = Error(
                    FaceMotionDiagnosticCodes.BatchDuplicateExportPath,
                    "Export path is used by multiple animations:\n" + pair.Key + "\n" + names,
                    details);
                for (int i = 0; i < pair.Value.Count; i++) pair.Value[i].Diagnostics.Add(collision);
                diagnostics.Add(collision);
            }
        }
        private static bool HasBlocking(IReadOnlyList<FaceMotionDiagnostic> diagnostics) { return OneClickIntegrationService.HasBlocking(diagnostics); }
        private static FaceMotionDiagnostic Error(string code, string message, IReadOnlyDictionary<string, string> details = null) { return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Error, message, "batch-integration", true, "Resolve the batch diagnostics and run again.", details); }

        private sealed class PreparedItem
        {
            public PreparedItem(FaceMotionAnimationData animation) { Animation = animation; }
            public FaceMotionAnimationData Animation { get; }
            public string ExportPath { get; set; }
            public AnimationClip Clip { get; set; }
            public List<FaceMotionDiagnostic> Diagnostics { get; } = new List<FaceMotionDiagnostic>();
        }
    }
}
