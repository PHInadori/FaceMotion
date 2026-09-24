using System;
using FaceMotion.Editor.VRChat.Integration;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.UI.Diagnostics;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Localization;
using FaceMotion.Integration;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.UI.Panels
{
    /// <summary>Phase G's intentionally explicit direct-integration surface.</summary>
    public sealed class DirectVRChatIntegrationPanel
    {
        private readonly FaceMotionEditorSession _session;
        private readonly Action<VRCAvatarDescriptor> _invalidateManagedState;
        private AnimationClip _clip;
        private string _folder = OneClickIntegrationService.DefaultOutputFolder;
        private DirectIntegrationPlan _plan;
        private OptionalIntegrationPlan _optionalPlan;
        private ModularAvatarIntegrationPlan _modularAvatarPlan;
        private IntegrationBackendSelection _backend;
        private IModularAvatarIntegrationBackend _maBackend;
        private ModularAvatarIntegrationPresenceCache _maPresence;

        public DirectVRChatIntegrationPanel(FaceMotionEditorSession session, Action<VRCAvatarDescriptor> invalidateManagedState = null)
        {
            _session = session;
            _invalidateManagedState = invalidateManagedState;
            _backend = IntegrationBackendSelectionStore.Load();
        }

        /// <summary>
        /// Lazily created once and cached: creating it resolves the MA assembly via reflection,
        /// which must never run on a per-repaint path.
        /// </summary>
        internal IModularAvatarIntegrationBackend MaBackend
        {
            get
            {
                if (_backend != IntegrationBackendSelection.ModularAvatar) return null;
                if (_maBackend == null) _maBackend = ModularAvatarIntegrationBackendLocator.Create();
                return _maBackend;
            }
        }

        internal string OutputFolder => _folder;

        private ModularAvatarIntegrationPresenceCache Presence => _maPresence ?? (_maPresence = new ModularAvatarIntegrationPresenceCache(MaBackend, _session));

        public void OnGUI()
        {
            EditorGUILayout.LabelField(FaceMotionUiText.Get("directIntegration"), EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            int backendIndex = EditorGUILayout.Popup(new GUIContent(FaceMotionUiText.Get("integrationBackend"), FaceMotionUiText.Get("tooltipBackend")), (int)_backend, new[] { FaceMotionUiText.Get("directBackend"), FaceMotionUiText.Get("modularAvatarBackend") });
            _backend = (IntegrationBackendSelection)backendIndex;
            if (EditorGUI.EndChangeCheck())
            {
                IntegrationBackendSelectionStore.Save(_backend);
                _plan = null;
                _optionalPlan = null;
                _modularAvatarPlan = null;
            }

            EditorGUILayout.LabelField(
                _backend == IntegrationBackendSelection.ModularAvatar
                    ? FaceMotionUiText.Get("backendModularAvatarDescription")
                    : FaceMotionUiText.Get("backendDirectDescription"),
                EditorStyles.wordWrappedMiniLabel);
            var avatar = _session.ActiveAvatarRoot == null ? null : _session.ActiveAvatarRoot.GetComponent<VRCAvatarDescriptor>();
            EditorGUILayout.ObjectField(FaceMotionUiText.Get("avatar"), avatar, typeof(VRCAvatarDescriptor), true);
            _clip = (AnimationClip)EditorGUILayout.ObjectField(FaceMotionUiText.Get("animationClip"), _clip, typeof(AnimationClip), false);
            _folder = EditorGUILayout.TextField(FaceMotionUiText.Get("outputFolder"), _folder);
            if (_backend == IntegrationBackendSelection.Direct) EditorGUILayout.HelpBox(FaceMotionUiText.Get("expressionBudgetPolicy"), MessageType.Info);
            bool showMaRemove = _backend == IntegrationBackendSelection.ModularAvatar && avatar != null && MaBackend != null && Presence.HasExistingIntegration(avatar);
            if (GUILayout.Button(FaceMotionUiText.Get("planIntegration")))
            {
                if (_backend == IntegrationBackendSelection.Direct)
                {
                    string name = _session.GetSelectedAnimation() == null ? (_clip == null ? "FaceMotion" : _clip.name) : _session.GetSelectedAnimation().DisplayName;
                    _plan = DirectVRChatIntegration.Plan(new DirectIntegrationRequest(avatar, _clip, _folder, name));
                    _optionalPlan = null;
                }
                else
                {
                    if (MaBackend == null) _optionalPlan = new ModularAvatarOptionalBackend().Plan();
                    else
                    {
                        string name = _session.GetSelectedAnimation() == null ? (_clip == null ? "FaceMotion" : _clip.name) : _session.GetSelectedAnimation().DisplayName;
                        _modularAvatarPlan = MaBackend.Plan(new ModularAvatarIntegrationRequest(avatar, _clip, _folder, name));
                        _optionalPlan = null;
                    }
                    _plan = null;
                }
            }
            if (showMaRemove)
            {
                DrawModularAvatarRemoval(MaBackend, avatar);
            }
            if (_optionalPlan != null)
            {
                EditorGUILayout.LabelField(FaceMotionUiText.Get("optionalBackendStatus"), _optionalPlan.Availability.Reason);
                for (int i = 0; i < _optionalPlan.Diagnostics.Count; i++)
                {
                    var diagnostic = _optionalPlan.Diagnostics[i];
                    EditorGUILayout.HelpBox(FormatDiagnostic(diagnostic), MessageType.Error);
                }
                return;
            }
            if (_modularAvatarPlan != null)
            {
                EditorGUILayout.LabelField(FaceMotionUiText.Get("proposedParameter"), _modularAvatarPlan.ParameterName);
                EditorGUILayout.LabelField(FaceMotionUiText.Get("proposedMaObject"), _modularAvatarPlan.ObjectName);
                for (int i = 0; i < _modularAvatarPlan.Diagnostics.Count; i++)
                {
                    var diagnostic = _modularAvatarPlan.Diagnostics[i];
                    EditorGUILayout.HelpBox(FormatDiagnostic(diagnostic), diagnostic.Blocking ? MessageType.Error : MessageType.Info);
                }
                using (new EditorGUI.DisabledScope(!_modularAvatarPlan.IsValid || MaBackend == null))
                    if (GUILayout.Button(FaceMotionUiText.Get("applyModularAvatarIntegration")))
                    {
                        var result = ApplyModularAvatarIntegration(MaBackend, _modularAvatarPlan);
                        if (result.Diagnostics.Count > 0) _session.SetLastOperationDiagnostic(result.Diagnostics[result.Diagnostics.Count - 1]);
                        _session.RecomputeDiagnostics(); _session.NotifyChanged();
                        if (result.Succeeded) Selection.activeObject = result.Manifest;
                    }
                return;
            }
            if (_plan == null) return;
            EditorGUILayout.LabelField(FaceMotionUiText.Get("proposedParameter"), _plan.ParameterName);
            EditorGUILayout.LabelField(FaceMotionUiText.Get("proposedFxLayer"), _plan.LayerName);
            EditorGUILayout.LabelField(FaceMotionUiText.Get("generatedAssetStem"), _plan.AssetStem);
            for (int i = 0; i < _plan.Diagnostics.Count; i++)
            {
                var d = _plan.Diagnostics[i];
                EditorGUILayout.HelpBox(FormatDiagnostic(d), d.Blocking ? MessageType.Error : MessageType.Info);
            }
            using (new EditorGUI.DisabledScope(!_plan.IsValid))
            {
                if (GUILayout.Button(FaceMotionUiText.Get("applyDirectIntegration")))
                {
                    var result = DirectVRChatIntegration.Apply(_plan);
                    if (result.Diagnostics.Count > 0) _session.SetLastOperationDiagnostic(result.Diagnostics[result.Diagnostics.Count - 1]);
                    _session.RecomputeDiagnostics(); _session.NotifyChanged();
                    if (result.Succeeded) Selection.activeObject = result.Manifest;
                }
            }
        }

        internal static bool ShouldShowModularAvatarRemove(
            IntegrationBackendSelection backend,
            IModularAvatarIntegrationBackend modularAvatarBackend,
            VRCAvatarDescriptor avatar)
        {
            return backend == IntegrationBackendSelection.ModularAvatar
                && modularAvatarBackend != null
                && modularAvatarBackend.HasExistingIntegration(avatar);
        }

        internal static IntegrationBackendSelection ResolveInitialBackend(
            bool hasExplicitSelection,
            IntegrationBackendSelection explicitSelection,
            bool modularAvatarAvailable)
        {
            return IntegrationBackendSelectionStore.ResolveInitial(hasExplicitSelection, explicitSelection, modularAvatarAvailable);
        }

        internal static string FormatDiagnostic(
            FaceMotionDiagnostic diagnostic,
            FaceMotionDiagnosticLanguage? language = null)
        {
            if (diagnostic == null)
            {
                return string.Empty;
            }

            FaceMotionDiagnosticLanguage resolvedLanguage = language ?? FaceMotionUiLanguage.Resolve();
            var presentation = new FaceMotionDiagnosticPresentation(diagnostic, resolvedLanguage);
            SystemLanguage systemLanguage = resolvedLanguage == FaceMotionDiagnosticLanguage.Japanese
                ? SystemLanguage.Japanese
                : SystemLanguage.English;
            var lines = new System.Collections.Generic.List<string>
            {
                "[" + presentation.Code + "]",
                presentation.Title,
                presentation.Summary
            };
            AddField(lines, FaceMotionUiText.Get("cause", systemLanguage), presentation.Cause);
            AddDetail(lines, FaceMotionUiText.Get("conflictObject", systemLanguage), presentation, FaceMotionDiagnosticDetailKeys.ConflictObjectName);
            AddDetail(lines, FaceMotionUiText.Get("conflictBinding", systemLanguage), presentation, FaceMotionDiagnosticDetailKeys.Binding);
            AddDetail(lines, FaceMotionUiText.Get("hierarchyPath", systemLanguage), presentation, FaceMotionDiagnosticDetailKeys.ConflictObjectPath);
            AddDetail(lines, FaceMotionUiText.Get("animatorController", systemLanguage), presentation, FaceMotionDiagnosticDetailKeys.ConflictController);
            AddDetail(lines, FaceMotionUiText.Get("conflictAnimationClip", systemLanguage), presentation, FaceMotionDiagnosticDetailKeys.ConflictClip);
            AddDetail(lines, FaceMotionUiText.Get("exportPath", systemLanguage), presentation, FaceMotionDiagnosticDetailKeys.ConflictExportPath);
            AddDetail(lines, FaceMotionUiText.Get("animations", systemLanguage), presentation, FaceMotionDiagnosticDetailKeys.ConflictAnimationNames);
            AddDetail(lines, FaceMotionUiText.Get("component", systemLanguage), presentation, FaceMotionDiagnosticDetailKeys.ConflictComponent);
            AddField(lines, FaceMotionUiText.Get("impact", systemLanguage), presentation.Impact);
            AddField(lines, FaceMotionUiText.Get("resolution", systemLanguage), presentation.Resolution);
            AddField(lines, FaceMotionUiText.Get("caution", systemLanguage), presentation.Caution);
            AddField(lines, FaceMotionUiText.Get("context", systemLanguage), presentation.ContextId);
            return string.Join("\n", lines);
        }

        private static void AddField(System.Collections.Generic.List<string> lines, string label, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                lines.Add(label + ": " + value);
            }
        }

        private static void AddDetail(System.Collections.Generic.List<string> lines, string label, FaceMotionDiagnosticPresentation presentation, string key)
        {
            if (presentation.Details != null && presentation.Details.TryGetValue(key, out string value))
            {
                AddField(lines, label, value);
            }
        }

        private void DrawModularAvatarRemoval(IModularAvatarIntegrationBackend maBackend, VRCAvatarDescriptor avatar)
        {
            if (_modularAvatarPlan != null
                && !string.IsNullOrEmpty(_modularAvatarPlan.ParameterName)
                && Presence.HasExistingIntegration(avatar, _modularAvatarPlan.ParameterName)
                && GUILayout.Button(FaceMotionUiText.Get("removeModularAvatarIntegrationForCurrent")))
            {
                RemoveAndRefresh(RemoveModularAvatarIntegration(maBackend, avatar, _modularAvatarPlan.ParameterName));
            }

            if (GUILayout.Button(FaceMotionUiText.Get("removeAllModularAvatarIntegrations")))
            {
                RemoveAndRefresh(RemoveAllModularAvatarIntegrations(maBackend, avatar));
            }
        }

        internal ModularAvatarIntegrationResult ApplyModularAvatarIntegration(IModularAvatarIntegrationBackend backend, ModularAvatarIntegrationPlan plan)
        {
            try
            {
                return backend.Apply(plan);
            }
            finally
            {
                _invalidateManagedState?.Invoke(plan == null || plan.Request == null ? null : plan.Request.Avatar);
            }
        }

        internal ModularAvatarIntegrationResult RemoveModularAvatarIntegration(IModularAvatarIntegrationBackend backend, VRCAvatarDescriptor avatar, string parameter)
        {
            try
            {
                return backend.RemoveAnimation(avatar, parameter);
            }
            finally
            {
                _invalidateManagedState?.Invoke(avatar);
            }
        }

        internal ModularAvatarIntegrationResult RemoveAllModularAvatarIntegrations(IModularAvatarIntegrationBackend backend, VRCAvatarDescriptor avatar)
        {
            try
            {
                return backend.Remove(avatar);
            }
            finally
            {
                _invalidateManagedState?.Invoke(avatar);
            }
        }

        private void RemoveAndRefresh(ModularAvatarIntegrationResult result)
        {
            if (result.Diagnostics.Count > 0) _session.SetLastOperationDiagnostic(result.Diagnostics[result.Diagnostics.Count - 1]);
            _session.RecomputeDiagnostics();
            _session.NotifyChanged();
        }
    }
}
