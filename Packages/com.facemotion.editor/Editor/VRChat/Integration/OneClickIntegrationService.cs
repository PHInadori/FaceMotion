using System;
using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.Export;
using FaceMotion.Integration;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.VRChat.Integration
{
    /// <summary>Executable stage of the one-click integration flow.</summary>
    public enum OneClickStage
    {
        NotStarted = 0,
        Export = 1,
        Plan = 2,
        Validate = 3,
        Apply = 4,
        Done = 5
    }

    /// <summary>Input to the one-click integration. The selected animation is always exported fresh.</summary>
    public sealed class OneClickIntegrationRequest
    {
        public OneClickIntegrationRequest(VRCAvatarDescriptor avatar, FaceMotionAnimationData animation, FaceMotionProject project, string outputFolder)
        {
            Avatar = avatar;
            Animation = animation;
            Project = project;
            OutputFolder = string.IsNullOrWhiteSpace(outputFolder) ? OneClickIntegrationService.DefaultOutputFolder : outputFolder;
        }

        public VRCAvatarDescriptor Avatar { get; }
        public FaceMotionAnimationData Animation { get; }
        public FaceMotionProject Project { get; }
        public string OutputFolder { get; }
    }

    /// <summary>Outcome of running the one-click integration flow.</summary>
    public sealed class OneClickIntegrationResult
    {
        public OneClickIntegrationResult(
            OneClickStage stage,
            bool succeeded,
            AnimationClip clip,
            string backend,
            string parameterName,
            string exportPath,
            UnityEngine.Object manifest,
            IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        {
            Stage = stage;
            Succeeded = succeeded;
            Clip = clip;
            Backend = backend ?? string.Empty;
            ParameterName = parameterName ?? string.Empty;
            ExportPath = exportPath ?? string.Empty;
            Manifest = manifest;
            Diagnostics = diagnostics ?? Array.Empty<FaceMotionDiagnostic>();
        }

        public OneClickStage Stage { get; }
        public bool Succeeded { get; }
        public AnimationClip Clip { get; }
        public string Backend { get; }
        public string ParameterName { get; }
        public string ExportPath { get; }
        public UnityEngine.Object Manifest { get; }
        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }
    }

    /// <summary>
    /// Orchestrates the one-click "VRChatへ追加" flow: Export -> Plan -> Validate -> Apply.
    /// It never owns UI state, never shows the progress bar, and every blocking diagnostic
    /// stops the flow before Apply. Direct and Modular Avatar backends both remain idempotent,
    /// so re-runs replace the existing integration instead of duplicating it.
    /// </summary>
    public static class OneClickIntegrationService
    {
        public const string ModularAvatarBackendId = "modular-avatar";
        public const string DefaultOutputFolder = "Assets/FaceMotion/Exports";

        /// <summary>Empty normal-flow roots use FaceMotion's established generated-asset location.</summary>
        public static string ResolveIntegrationOutputFolder(string outputFolder)
        {
            return string.IsNullOrWhiteSpace(outputFolder) ? DefaultOutputFolder : outputFolder;
        }

        /// <summary>
        /// True for FaceMotion's canonical generated-asset root only. That root is managed by
        /// FaceMotion itself, so it may be planned before it exists; arbitrary user paths never match.
        /// </summary>
        public static bool IsCanonicalDefaultOutputFolder(string outputFolder)
        {
            if (string.IsNullOrEmpty(outputFolder)) return false;
            return string.Equals(outputFolder.Replace('\\', '/').TrimEnd('/'), DefaultOutputFolder, StringComparison.Ordinal);
        }

        /// <summary>
        /// Write-free output-root validation used by backend planning. An explicit path still has to
        /// be an existing folder under Assets; the canonical default may be planned while absent
        /// because the mutation stage creates it before any generated asset is written.
        /// </summary>
        public static bool IsPlannableOutputFolder(string outputFolder)
        {
            if (string.IsNullOrEmpty(outputFolder)) return false;
            string path = outputFolder.Replace('\\', '/');
            if (path != "Assets" && !path.StartsWith("Assets/", StringComparison.Ordinal)) return false;
            foreach (var part in path.Split('/')) if (part.Length == 0 || part == "." || part == "..") return false;
            return AssetDatabase.IsValidFolder(path) || IsCanonicalDefaultOutputFolder(path);
        }

        /// <summary>Write-free preflight summary. It never creates assets and never plans.</summary>
        public static OneClickPreflight Preflight(OneClickIntegrationRequest request)
        {
            var diagnostics = new List<FaceMotionDiagnostic>();
            if (request == null || request.Animation == null)
            {
                diagnostics.Add(OneClickBlocking(FaceMotionDiagnosticCodes.OneClickNoCurrentAnimation));
                return new OneClickPreflight(string.Empty, string.Empty, string.Empty, false, diagnostics);
            }

            if (request.Animation.Timeline == null)
            {
                diagnostics.Add(OneClickBlocking(FaceMotionDiagnosticCodes.ExportNoTimeline));
            }

            var backend = ResolveBackend(request, diagnostics, out bool backendUsable);
            if (backendUsable) CheckCrossBackend(request, backend, diagnostics);
            string exportPath = ResolveExportPath(request, diagnostics);
            string parameter = MaParameterName(request);

            if (!string.IsNullOrEmpty(exportPath))
            {
                var exportValidation = AnimationClipExporter.Validate(request.Animation, exportPath);
                for (int i = 0; i < exportValidation.Diagnostics.Count; i++) diagnostics.Add(exportValidation.Diagnostics[i]);
                CheckForeignClip(request, exportPath, diagnostics);
            }

            bool blocked = HasBlocking(diagnostics);
            return new OneClickPreflight(
                BackendId(backend),
                exportPath,
                parameter,
                !blocked,
                diagnostics);
        }

        /// <summary>Runs the full flow. Blocking diagnostics always stop before Apply.</summary>
        public static OneClickIntegrationResult Execute(OneClickIntegrationRequest request, Action<OneClickStage> progress = null)
        {
            var diagnostics = new List<FaceMotionDiagnostic>();
            if (request == null || request.Animation == null)
            {
                diagnostics.Add(OneClickBlocking(FaceMotionDiagnosticCodes.OneClickNoCurrentAnimation));
                return new OneClickIntegrationResult(OneClickStage.NotStarted, false, null, string.Empty, string.Empty, string.Empty, null, diagnostics);
            }

            if (request.Avatar == null)
            {
                diagnostics.Add(OneClickBlocking(FaceMotionDiagnosticCodes.OneClickNoAvatar));
                return new OneClickIntegrationResult(OneClickStage.NotStarted, false, null, string.Empty, string.Empty, string.Empty, null, diagnostics);
            }

            var selectedBackend = ResolveBackend(request, diagnostics, out bool backendUsable);
            if (!backendUsable)
            {
                return new OneClickIntegrationResult(OneClickStage.NotStarted, false, null, BackendId(selectedBackend), string.Empty, string.Empty, null, diagnostics);
            }

            CheckCrossBackend(request, selectedBackend, diagnostics);
            string exportPath = ResolveExportPath(request, diagnostics);
            CheckForeignClip(request, exportPath, diagnostics);
            if (HasBlocking(diagnostics))
            {
                return new OneClickIntegrationResult(OneClickStage.Export, false, null, BackendId(selectedBackend), MaParameterName(request), exportPath, null, diagnostics);
            }

            // Stage 1: Export.
            progress?.Invoke(OneClickStage.Export);
            var exportResult = AnimationClipExporter.Export(request.Animation, exportPath);
            for (int i = 0; i < exportResult.Diagnostics.Count; i++) diagnostics.Add(exportResult.Diagnostics[i]);
            if (!exportResult.Succeeded)
            {
                return new OneClickIntegrationResult(OneClickStage.Export, false, null, BackendId(selectedBackend), MaParameterName(request), exportPath, null, diagnostics);
            }

            var clip = exportResult.Clip;
            ExportedClipRegistry.Record(request.Animation.AnimationId, exportPath);
            diagnostics.Add(Info(FaceMotionDiagnosticCodes.OneClickExported, "AnimationClip exported for one-click integration."));

            // Stage 2: Plan.
            progress?.Invoke(OneClickStage.Plan);
            string parameterName = MaParameterName(request);
            bool reapplied = selectedBackend == IntegrationBackendSelection.Direct
                ? DirectVRChatIntegration.HasExistingIntegration(request.Avatar)
                : ModularAvatarIntegrationBackendLocator.Create()?.HasExistingIntegration(request.Avatar) == true;
            if (selectedBackend == IntegrationBackendSelection.Direct)
            {
                var plan = DirectVRChatIntegration.Plan(new DirectIntegrationRequest(request.Avatar, clip, request.OutputFolder, request.Animation.DisplayName));
                for (int i = 0; i < plan.Diagnostics.Count; i++) diagnostics.Add(plan.Diagnostics[i]);
                if (!plan.IsValid)
                {
                    return new OneClickIntegrationResult(OneClickStage.Validate, false, clip, DirectVRChatIntegration.BackendId, plan.ParameterName, exportPath, null, diagnostics);
                }

                // Stage 4: Apply.
                progress?.Invoke(OneClickStage.Apply);
                var applied = DirectVRChatIntegration.Apply(plan);
                return Finish(request, OneClickStage.Apply, selectedBackend, clip, plan.ParameterName, exportPath, applied.Succeeded, applied.Manifest, applied.Diagnostics, diagnostics, reapplied);
            }

            var maBackend = ModularAvatarIntegrationBackendLocator.Create();
            if (maBackend == null)
            {
                diagnostics.Add(OneClickBlocking(FaceMotionDiagnosticCodes.OneClickBackendUnavailable));
                return new OneClickIntegrationResult(OneClickStage.NotStarted, false, clip, ModularAvatarBackendId, parameterName, exportPath, null, diagnostics);
            }

            var maPlan = maBackend.Plan(new ModularAvatarIntegrationRequest(request.Avatar, clip, request.OutputFolder, request.Animation.DisplayName));
            for (int i = 0; i < maPlan.Diagnostics.Count; i++) diagnostics.Add(maPlan.Diagnostics[i]);
            if (!maPlan.IsValid)
            {
                return new OneClickIntegrationResult(OneClickStage.Validate, false, clip, ModularAvatarBackendId, maPlan.ParameterName, exportPath, null, diagnostics);
            }

            progress?.Invoke(OneClickStage.Apply);
            var maApplied = maBackend.Apply(maPlan);
            return Finish(request, OneClickStage.Apply, selectedBackend, clip, maPlan.ParameterName, exportPath, maApplied.Succeeded, maApplied.Manifest, maApplied.Diagnostics, diagnostics, reapplied);
        }

        private static OneClickIntegrationResult Finish(
            OneClickIntegrationRequest request,
            OneClickStage failedStage,
            IntegrationBackendSelection backend,
            AnimationClip clip,
            string parameterName,
            string exportPath,
            bool succeeded,
            UnityEngine.Object manifest,
            IReadOnlyList<FaceMotionDiagnostic> applyDiagnostics,
            List<FaceMotionDiagnostic> diagnostics,
            bool reapplied)
        {
            for (int i = 0; i < applyDiagnostics.Count; i++) diagnostics.Add(applyDiagnostics[i]);
            if (succeeded)
            {
                if (reapplied) diagnostics.Add(Info(FaceMotionDiagnosticCodes.OneClickReapplied, "An existing integration was replaced instead of duplicated."));
                diagnostics.Add(Info(FaceMotionDiagnosticCodes.OneClickSucceeded, "VRChat one-click integration applied."));
                return new OneClickIntegrationResult(OneClickStage.Done, true, clip, BackendId(backend), parameterName, exportPath, manifest, diagnostics);
            }

            diagnostics.Add(Info(FaceMotionDiagnosticCodes.OneClickNoPartialState, "No partial integration state remains; generated assets were rolled back."));
            return new OneClickIntegrationResult(failedStage, false, clip, BackendId(backend), parameterName, exportPath, null, diagnostics);
        }

        internal static IntegrationBackendSelection ResolveBackend(OneClickIntegrationRequest request, List<FaceMotionDiagnostic> diagnostics, out bool usable)
        {
            var selected = IntegrationBackendSelectionStore.Load();
            if (selected == IntegrationBackendSelection.ModularAvatar && ModularAvatarIntegrationBackendLocator.Create() == null)
            {
                if (diagnostics != null) diagnostics.Add(OneClickBlocking(FaceMotionDiagnosticCodes.OneClickBackendUnavailable));
                usable = false;
                return selected;
            }

            usable = true;
            return selected;
        }

        private static void CheckCrossBackend(OneClickIntegrationRequest request, IntegrationBackendSelection selected, List<FaceMotionDiagnostic> diagnostics)
        {
            if (request.Avatar == null) return;
            bool directExisting = DirectVRChatIntegration.HasExistingIntegration(request.Avatar);
            var maBackend = ModularAvatarIntegrationBackendLocator.Create();
            bool maExisting = maBackend != null && maBackend.HasExistingIntegration(request.Avatar);

            if (selected == IntegrationBackendSelection.Direct && maExisting)
            {
                diagnostics.Add(Info(FaceMotionDiagnosticCodes.OneClickCrossBackend, "An existing Modular Avatar integration was detected on this avatar; applying Direct integration creates a separate integration."));
            }

            if (selected == IntegrationBackendSelection.ModularAvatar && directExisting)
            {
                diagnostics.Add(Info(FaceMotionDiagnosticCodes.OneClickCrossBackend, "An existing Direct VRChat integration was detected on this avatar; applying Modular Avatar integration creates a separate integration."));
            }
        }

        private static void CheckForeignClip(OneClickIntegrationRequest request, string exportPath, List<FaceMotionDiagnostic> diagnostics)
        {
            if (request == null || string.IsNullOrEmpty(exportPath)) return;
            UnityEngine.Object main = AssetDatabase.LoadMainAssetAtPath(exportPath);
            if (main == null) return;

            if (main is AnimationClip)
            {
                // The exporter would overwrite an existing clip, so ownership must match.
                if (!ExportedClipRegistry.IsOwned(request.Animation.AnimationId, exportPath))
                {
                    diagnostics.Add(OneClickBlocking(FaceMotionDiagnosticCodes.OneClickForeignClip, "The export destination already contains an AnimationClip that FaceMotion does not own.", "Choose a different animation name or remove the foreign clip."));
                }
            }
        }

        public static string ResolveExportPath(OneClickIntegrationRequest request, List<FaceMotionDiagnostic> diagnostics)
        {
            if (ExportedClipRegistry.TryGetPath(request.Animation.AnimationId, out string saved))
            {
                return saved;
            }

            string path = ResolveDefaultExportPath(request);
            if (request.Animation.AnimationId == null)
            {
                diagnostics?.Add(OneClickBlocking(FaceMotionDiagnosticCodes.OneClickForeignClip));
            }

            return path;
        }

        /// <summary>
        /// Produces a readable, deterministic default path without persisting it. Existing registry
        /// entries always win, which preserves both manually selected destinations and old projects.
        /// Unregistered animations are allocated in project order so duplicate names receive _2, _3,
        /// and so on instead of an opaque ID suffix.
        /// </summary>
        internal static string ResolveDefaultExportPath(OneClickIntegrationRequest request)
        {
            string folder = string.IsNullOrWhiteSpace(request?.OutputFolder)
                ? DefaultOutputFolder
                : request.OutputFolder.TrimEnd('/', '\\');
            var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var project = request?.Project;
            var target = request?.Animation;

            if (project != null)
            {
                for (int i = 0; i < project.Animations.Count; i++)
                {
                    var animation = project.Animations[i];
                    if (animation == null) continue;

                    if (ExportedClipRegistry.TryGetPath(animation.AnimationId, out string saved))
                    {
                        reserved.Add(saved);
                        if (object.ReferenceEquals(animation, target)) return saved;
                        continue;
                    }

                    string candidate = NextAvailablePath(folder, SanitizeExportFileName(animation.DisplayName), reserved);
                    if (object.ReferenceEquals(animation, target)) return candidate;
                    reserved.Add(candidate);
                }
            }

            return NextAvailablePath(folder, SanitizeExportFileName(target == null ? null : target.DisplayName), reserved);
        }

        internal static string SanitizeExportFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "FaceMotion";

            var builder = new System.Text.StringBuilder(value.Length);
            bool pendingSpace = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsWhiteSpace(c))
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (IsInvalidExportFileNameCharacter(c)) continue;
                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }
                builder.Append(c);
            }

            string result = builder.ToString().Trim().Trim('.');
            return string.IsNullOrWhiteSpace(result) ? "FaceMotion" : result;
        }

        private static string NextAvailablePath(string folder, string fileName, HashSet<string> reserved)
        {
            string basePath = folder + "/" + fileName;
            string path = basePath + ".anim";
            int suffix = 2;
            while (reserved.Contains(path))
            {
                path = basePath + "_" + suffix + ".anim";
                suffix++;
            }
            return path;
        }

        private static bool IsInvalidExportFileNameCharacter(char value)
        {
            return char.IsControl(value)
                || value == '<' || value == '>' || value == ':' || value == '"'
                || value == '/' || value == '\\' || value == '|' || value == '?' || value == '*';
        }

        /// <summary>
        /// The deterministic Modular Avatar parameter a one-click request produces.
        /// Shared by preflight, execute, and the removal UI so they always agree.
        /// </summary>
        public static string MaParameterName(OneClickIntegrationRequest request)
        {
            return "FaceMotion_" + BuildStem(request);
        }

        internal static string BuildStem(OneClickIntegrationRequest request)
        {
            string stem = Sanitize(request.Animation == null ? "FaceMotion" : request.Animation.DisplayName);
            if (request.Project == null) return stem;
            int count = 0;
            for (int i = 0; i < request.Project.Animations.Count; i++)
            {
                var candidate = request.Project.Animations[i];
                if (candidate == null) continue;
                if (string.Equals(Sanitize(candidate.DisplayName), stem, StringComparison.Ordinal)) count++;
            }

            if (count <= 1) return stem;
            string id = request.Animation == null ? string.Empty : request.Animation.AnimationId ?? string.Empty;
            string suffix = id.Length >= 6 ? id.Substring(0, 6) : (id.Length == 0 ? "face" : id);
            return stem + "_" + suffix;
        }

        internal static string Sanitize(string value)
        {
            var chars = (string.IsNullOrEmpty(value) ? "FaceMotion" : value).ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_') chars[i] = '_';
            }

            return new string(chars);
        }

        private static string BackendId(IntegrationBackendSelection backend)
        {
            return backend == IntegrationBackendSelection.ModularAvatar ? ModularAvatarBackendId : DirectVRChatIntegration.BackendId;
        }

        internal static bool HasBlocking(IReadOnlyList<FaceMotionDiagnostic> items)
        {
            for (int i = 0; i < items.Count; i++) if (items[i].Blocking) return true;
            return false;
        }

        private static FaceMotionDiagnostic OneClickBlocking(string code, string message = null, string fix = null)
        {
            string text = message ?? code;
            return new FaceMotionDiagnostic(
                code,
                FaceMotionDiagnosticSeverity.Error,
                text,
                "one-click",
                true,
                fix ?? ((code == FaceMotionDiagnosticCodes.OneClickForeignClip) ? "Free the export path or rename the animation." : "Review the one-click integration inputs."));
        }

        private static FaceMotionDiagnostic Info(string code, string message)
        {
            return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Info, message, "one-click", false, string.Empty);
        }
    }

    /// <summary>Write-free result of <see cref="OneClickIntegrationService.Preflight"/>.</summary>
    public sealed class OneClickPreflight
    {
        public OneClickPreflight(string backendId, string exportPath, string parameterName, bool ready, IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        {
            BackendId = backendId ?? string.Empty;
            ExportPath = exportPath ?? string.Empty;
            ParameterName = parameterName ?? string.Empty;
            Ready = ready;
            Diagnostics = diagnostics ?? Array.Empty<FaceMotionDiagnostic>();
        }

        public string BackendId { get; }
        public string ExportPath { get; }
        public string ParameterName { get; }
        public bool Ready { get; }
        public IReadOnlyList<FaceMotionDiagnostic> Diagnostics { get; }
    }
}
