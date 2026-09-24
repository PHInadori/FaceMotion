using System;
using System.Collections.Generic;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Localization;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.VRChat.Integration;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.UI.Panels
{
    /// <summary>
    /// The normal VRChat section ("VRChatへ反映"). It owns the desired-state checklist
    /// (checkbox + name + applied status), the single primary Update button, and the
    /// beginner result messages derived from structured reconciliation results. The explicit
    /// per-animation manual workflow stays inside the window-owned Advanced foldout via
    /// DrawAdvanced; this panel never renders a nested Advanced foldout.
    /// </summary>
    public sealed class OneClickIntegrationPanel
    {
        private readonly FaceMotionEditorSession _session;
        private readonly OneClickIntegrationController _controller;
        private readonly DirectVRChatIntegrationPanel _advanced;
        private ModularAvatarIntegrationPresenceCache _maPresence;
        private VrchatDesiredStateReconciliationResult _desiredResult;
        private readonly ModularAvatarManagedStateCache _managedStateCache;
        private readonly Action<VRCAvatarDescriptor> _refreshAvatarIndexAfterIntegration;

        public OneClickIntegrationPanel(FaceMotionEditorSession session, ModularAvatarManagedStateCache managedStateCache, Action<VRCAvatarDescriptor> refreshAvatarIndexAfterIntegration = null)
        {
            _session = session;
            _controller = new OneClickIntegrationController(session, InvalidateManagedState);
            _advanced = new DirectVRChatIntegrationPanel(session, InvalidateManagedState);
            _managedStateCache = managedStateCache ?? throw new ArgumentNullException(nameof(managedStateCache));
            _refreshAvatarIndexAfterIntegration = refreshAvatarIndexAfterIntegration;
        }

        private ModularAvatarIntegrationPresenceCache Presence => _maPresence ?? (_maPresence = new ModularAvatarIntegrationPresenceCache(_advanced.MaBackend, _session));

        public void OnGUI()
        {
            EditorGUILayout.LabelField(FaceMotionUiText.Get("vrchatApply"), EditorStyles.boldLabel);
            var avatar = _session.ActiveAvatarRoot == null
                ? null
                : _session.ActiveAvatarRoot.GetComponent<VRCAvatarDescriptor>();

            if (avatar == null)
            {
                EditorGUILayout.HelpBox(FaceMotionUiText.Get("selectAvatar"), MessageType.Info);
            }

            DrawDesiredState();
            EditorGUILayout.Space();
        }

        /// <summary>The one normal-primary action: reconcile the checked selection into VRChat.</summary>
        internal static string UpdateVrchatSelectedLabel()
        {
            return FaceMotionUiText.Get("updateVrchatSelected");
        }

        /// <summary>Renders one checklist row; presentational, so it can be asserted without GUI.</summary>
        internal static string ChecklistItemLabel(string displayName, VrchatIntegrationStatus status)
        {
            string name = string.IsNullOrEmpty(displayName) ? FaceMotionUiText.Get("unnamed") : displayName;
            string state = status == VrchatIntegrationStatus.Applied
                ? FaceMotionUiText.Get("applied")
                : FaceMotionUiText.Get("notApplied");
            return name + "    " + state;
        }

        /// <summary>The desired state may be empty: that removes every proven-owned integration.</summary>
        internal static bool CanUpdateVrchat(bool projectAvailable, bool avatarAvailable, bool backendAvailable)
        {
            return projectAvailable && avatarAvailable && backendAvailable;
        }

        internal static string EmptyDesiredSelectionHint(int currentIntegrationCount)
        {
            return currentIntegrationCount > 0
                ? FaceMotionUiText.Get("emptySelectionRemovesIntegrations")
                : string.Empty;
        }

        private void DrawDesiredState()
        {
            EditorGUILayout.Space();
            DrawDesiredAnimationChecklist();
            int count = _session.BatchAnimationIds.Count;
            bool projectAvailable = _session.ActiveProject != null;
            bool avatarAvailable = _session.ActiveAvatarRoot != null;
            bool backendAvailable = _advanced.MaBackend != null;
            var avatar = avatarAvailable ? _session.ActiveAvatarRoot.GetComponent<VRCAvatarDescriptor>() : null;
            var snapshot = GetManagedSnapshot(avatar, _advanced.MaBackend);
            int currentIntegrationCount = snapshot == null ? 0 : snapshot.Items.Count;
            bool ready = CanUpdateVrchat(projectAvailable, avatarAvailable, backendAvailable);
            using (new EditorGUI.DisabledScope(!ready))
            {
                if (GUILayout.Button(new GUIContent(UpdateVrchatSelectedLabel(), FaceMotionUiText.Get("tooltipUpdateVrchatSelected")), GUILayout.Height(28f)))
                {
                    RunDesiredState();
                }
            }

            if (!ready)
            {
                string reason;
                if (!backendAvailable) reason = FaceMotionUiText.Get("reflectRequiresModularAvatar");
                else if (!avatarAvailable) reason = FaceMotionUiText.Get("selectAvatar");
                else reason = FaceMotionUiText.Get("noProject");
                EditorGUILayout.HelpBox(reason, MessageType.Info);
            }
            else if (count == 0)
            {
                string hint = EmptyDesiredSelectionHint(currentIntegrationCount);
                if (!string.IsNullOrEmpty(hint)) EditorGUILayout.HelpBox(hint, MessageType.Info);
            }

            DrawDesiredStateResult();
        }

        private void DrawDesiredAnimationChecklist()
        {
            var animations = _session.ActiveProject == null ? null : _session.ActiveProject.Animations;
            if (animations == null) return;
            var avatar = _session.ActiveAvatarRoot == null ? null : _session.ActiveAvatarRoot.GetComponent<VRCAvatarDescriptor>();
            var backend = _advanced.MaBackend;
            var snapshot = GetManagedSnapshot(avatar, backend);
            var statuses = VrchatIntegrationStatusService.Resolve(_session.ActiveProject, avatar, snapshot);
            for (int i = 0; i < animations.Count; i++)
            {
                var animation = animations[i];
                if (animation == null) continue;
                bool selected = _session.IsBatchSelected(animation.AnimationId);
                VrchatIntegrationStatus status = statuses != null
                    && statuses.TryGetValue(animation.AnimationId, out var resolved)
                        ? resolved
                        : VrchatIntegrationStatus.NotApplied;
                bool next = EditorGUILayout.ToggleLeft(ChecklistItemLabel(animation.DisplayName, status), selected);
                if (next != selected) _session.SetBatchSelected(animation.AnimationId, next);
            }
        }

        private ModularAvatarManagedStateSnapshot GetManagedSnapshot(VRCAvatarDescriptor avatar, IModularAvatarIntegrationBackend backend)
        {
            if (avatar == null || backend == null) return null;
            return _managedStateCache.Get(backend, avatar);
        }

        private void DrawDesiredStateResult()
        {
            if (_desiredResult == null) return;
            var presentation = DesiredStateResultPresenter.Build(_desiredResult);
            if (presentation.HasBindingConflict)
            {
                string key = DesiredStateResultPresenter.ConflictBeginnerKey(presentation);
                string message = string.IsNullOrEmpty(presentation.ConflictObjectName)
                    ? FaceMotionUiText.Get(key)
                    : string.Format(FaceMotionUiText.Get(key), presentation.ConflictObjectName);
                EditorGUILayout.HelpBox(message, MessageType.Error);
            }

            switch (presentation.Kind)
            {
                case DesiredStateResultKind.Succeeded:
                    EditorGUILayout.LabelField(FaceMotionUiText.Get(presentation.TitleKey), EditorStyles.boldLabel);
                    if (presentation.AppliedCount > 0)
                    {
                        EditorGUILayout.LabelField(string.Format(FaceMotionUiText.Get(DesiredStateResultPresenter.CountSucceeded), presentation.AppliedCount));
                    }

                    break;
                case DesiredStateResultKind.NoChange:
                case DesiredStateResultKind.RemovalComplete:
                    EditorGUILayout.HelpBox(FaceMotionUiText.Get(presentation.TitleKey), MessageType.Info);
                    break;
                default:
                    EditorGUILayout.HelpBox(FaceMotionUiText.Get(presentation.TitleKey), MessageType.Error);
                    break;
            }

            for (var i = 0; i < presentation.TechnicalDetails.Count; i++)
            {
                EditorGUILayout.HelpBox(presentation.TechnicalDetails[i], MessageType.Error);
            }
        }

        private void RunDesiredState()
        {
            var avatar = _session.ActiveAvatarRoot == null ? null : _session.ActiveAvatarRoot.GetComponent<VRCAvatarDescriptor>();
            var backend = _advanced.MaBackend;
            if (avatar == null || backend == null) return;
            var result = VrchatDesiredStateReconciliationService.Execute(
                new VrchatDesiredStateReconciliationRequest(
                    avatar,
                    _session.ActiveProject,
                    new List<string>(_session.BatchAnimationIds),
                    _advanced.OutputFolder,
                    backend,
                    InvalidateManagedState));
            _desiredResult = result;
            SetDesiredStateOperationDiagnostic(_session, result);
            _session.RecomputeDiagnostics();
            _session.NotifyChanged();
        }

        /// <summary>Operation diagnostics must not outlive a successful replacement operation.</summary>
        internal static void SetDesiredStateOperationDiagnostic(
            FaceMotionEditorSession session,
            VrchatDesiredStateReconciliationResult result)
        {
            if (session == null) return;
            if (result == null || result.Succeeded || result.Diagnostics.Count == 0)
            {
                session.SetLastOperationDiagnostic(null);
                return;
            }

            session.SetLastOperationDiagnostic(result.Diagnostics[result.Diagnostics.Count - 1]);
        }

        /// <summary>Called by the window's single Advanced foldout.</summary>
        public void DrawAdvanced()
        {
            var animation = _session.GetSelectedAnimation();
            if (animation == null)
            {
                EditorGUILayout.HelpBox(FaceMotionUiText.Get("selectAnimationToExport"), MessageType.Info);
                DrawDisabledButton();
            }
            else if (_session.ActiveAvatarRoot != null) { DrawPreflight(); DrawResult(); }
            _advanced.OnGUI();
        }

        private void DrawPreflight()
        {
            var preflight = _controller.GetPreflight();
            string backendLabel = BackendLabel(preflight.BackendId);
            EditorGUILayout.LabelField(FaceMotionUiText.Get("oneClickBackendLabel"), backendLabel);
            EditorGUILayout.LabelField(FaceMotionUiText.Get("oneClickExportPathLabel"), preflight.ExportPath);
            EditorGUILayout.LabelField(FaceMotionUiText.Get("oneClickParameterLabel"), preflight.ParameterName);

            for (int i = 0; i < preflight.Diagnostics.Count; i++)
            {
                var d = preflight.Diagnostics[i];
                EditorGUILayout.HelpBox(DirectVRChatIntegrationPanel.FormatDiagnostic(d), d.Blocking ? MessageType.Error : MessageType.Info);
            }

            GUILayout.Label(
                preflight.Ready
                    ? FaceMotionUiText.Get("oneClickStatusReady")
                    : FaceMotionUiText.Get("oneClickStatusReview"),
                EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!preflight.Ready))
            {
                if (GUILayout.Button(new GUIContent(FaceMotionUiText.Get("addToVrchat"), FaceMotionUiText.Get("tooltipAddToVrchat")), GUILayout.Height(28f)))
                {
                    RunOneClick();
                }
            }

            DrawCurrentAnimationRemoval(preflight);
        }

        private void DrawCurrentAnimationRemoval(OneClickPreflight preflight)
        {
            if (preflight == null || preflight.BackendId != OneClickIntegrationService.ModularAvatarBackendId) return;
            if (string.IsNullOrEmpty(preflight.ParameterName)) return;
            var avatar = _session.ActiveAvatarRoot == null
                ? null
                : _session.ActiveAvatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (avatar == null) return;
            var backend = _advanced.MaBackend;
            if (backend == null || !Presence.HasExistingIntegration(avatar, preflight.ParameterName)) return;

            if (GUILayout.Button(new GUIContent(
                    FaceMotionUiText.Get("removeModularAvatarIntegrationForCurrent"),
                    preflight.ParameterName)))
            {
                try
                {
                    RemoveAndRefresh(backend.RemoveAnimation(avatar, preflight.ParameterName).Diagnostics);
                }
                finally
                {
                    InvalidateManagedState(avatar);
                }
            }
        }

        private void DrawResult()
        {
            var result = _controller.LastResult;
            if (result == null) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                result.Succeeded
                    ? FaceMotionUiText.Get("oneClickResultSucceeded")
                    : FaceMotionUiText.Get("oneClickResultFailed"),
                EditorStyles.boldLabel);
            if (result.Succeeded)
            {
                var selected = _session.GetSelectedAnimation();
                string animationName = selected == null || string.IsNullOrEmpty(selected.DisplayName)
                    ? result.ParameterName
                    : selected.DisplayName;
                EditorGUILayout.LabelField(FaceMotionUiText.Get("successBackendLabel"), BackendLabel(result.Backend));
                EditorGUILayout.LabelField(FaceMotionUiText.Get("successAnimationLabel"), animationName ?? string.Empty);
            }

            DrawSteps(result);
            if (result.Succeeded && result.Manifest != null)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(FaceMotionUiText.Get("selectGeneratedObject")))
                {
                    Selection.activeObject = result.Manifest;
                    EditorGUIUtility.PingObject(result.Manifest);
                }

                if (!string.IsNullOrEmpty(result.ExportPath) && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(result.ExportPath) != null)
                {
                    if (GUILayout.Button(FaceMotionUiText.Get("revealExportPath")))
                    {
                        EditorUtility.RevealInFinder(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), result.ExportPath));
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawSteps(OneClickIntegrationResult result)
        {
            bool step1Done = result.Succeeded || result.Stage >= OneClickStage.Plan;
            bool step2Done = result.Succeeded || result.Stage >= OneClickStage.Validate;
            bool step3Done = result.Succeeded || result.Stage >= OneClickStage.Apply;
            bool step4Done = result.Succeeded;
            bool step1Failed = !result.Succeeded && result.Stage == OneClickStage.Export;
            bool step2Failed = !result.Succeeded && result.Stage == OneClickStage.Plan;
            bool step3Failed = !result.Succeeded && result.Stage == OneClickStage.Validate;
            bool step4Failed = !result.Succeeded && result.Stage == OneClickStage.Apply;
            DrawStep(FaceMotionUiText.Get("oneClickStepExport"), step1Done, step1Failed);
            DrawStep(FaceMotionUiText.Get("oneClickStepPlan"), step2Done, step2Failed);
            DrawStep(FaceMotionUiText.Get("oneClickStepValidate"), step3Done, step3Failed);
            DrawStep(FaceMotionUiText.Get("oneClickStepApply"), step4Done, step4Failed);
        }

        private void DrawStep(string label, bool done, bool failed)
        {
            string suffix = done
                ? FaceMotionUiText.Get("succeededShort")
                : failed
                    ? FaceMotionUiText.Get("failedShort")
                    : FaceMotionUiText.Get("notExecutedShort");
            string message = label + ": " + suffix;
            if (failed)
            {
                EditorGUILayout.HelpBox(message, MessageType.Error);
            }
            else if (done)
            {
                EditorGUILayout.HelpBox(message, MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField(message, EditorStyles.miniLabel);
            }
        }

        private void RunOneClick()
        {
            try
            {
                _controller.Execute(stage => EditorUtility.DisplayProgressBar(
                    "FaceMotion",
                    ProgressLabel(stage),
                    ProgressFraction(stage)));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void RemoveAndRefresh(IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        {
            if (diagnostics != null && diagnostics.Count > 0)
            {
                _session.SetLastOperationDiagnostic(diagnostics[diagnostics.Count - 1]);
            }
            _session.RecomputeDiagnostics();
            _session.NotifyChanged();
        }

        private void InvalidateManagedState(VRCAvatarDescriptor avatar)
        {
            _managedStateCache.Invalidate(avatar);
            _refreshAvatarIndexAfterIntegration?.Invoke(avatar);
        }

        private static string ProgressLabel(OneClickStage stage)
        {
            switch (stage)
            {
                case OneClickStage.Plan: return FaceMotionUiText.Get("oneClickStagePlan");
                case OneClickStage.Validate: return FaceMotionUiText.Get("oneClickStageValidate");
                case OneClickStage.Apply: return FaceMotionUiText.Get("oneClickStageApply");
                default: return FaceMotionUiText.Get("oneClickStageExport");
            }
        }

        private static float ProgressFraction(OneClickStage stage)
        {
            switch (stage)
            {
                case OneClickStage.Plan: return 0.5f;
                case OneClickStage.Apply: return 0.9f;
                default: return 0.2f;
            }
        }

        private void DrawDisabledButton()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                GUILayout.Button(new GUIContent(FaceMotionUiText.Get("addToVrchat"), FaceMotionUiText.Get("tooltipAddToVrchat")), GUILayout.Height(28f));
            }
        }

        internal static string BackendLabel(string backendId)
        {
            return backendId == OneClickIntegrationService.ModularAvatarBackendId
                ? FaceMotionUiText.Get("modularAvatarBackend")
                : FaceMotionUiText.Get("directBackend");
        }
    }
}
