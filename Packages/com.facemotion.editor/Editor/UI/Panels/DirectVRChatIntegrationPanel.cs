using FaceMotion.Editor.VRChat.Integration;
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
        private AnimationClip _clip;
        private string _folder = "Assets";
        private DirectIntegrationPlan _plan;
        private OptionalIntegrationPlan _optionalPlan;
        private ModularAvatarIntegrationPlan _modularAvatarPlan;
        private IntegrationBackendSelection _backend = IntegrationBackendSelection.Direct;

        public DirectVRChatIntegrationPanel(FaceMotionEditorSession session) { _session = session; }

        public void OnGUI()
        {
            EditorGUILayout.LabelField(FaceMotionUiText.Get("directIntegration"), EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            int backendIndex = EditorGUILayout.Popup(FaceMotionUiText.Get("integrationBackend"), (int)_backend, new[] { FaceMotionUiText.Get("directBackend"), FaceMotionUiText.Get("modularAvatarBackend") });
            _backend = (IntegrationBackendSelection)backendIndex;
            if (EditorGUI.EndChangeCheck()) { _plan = null; _optionalPlan = null; _modularAvatarPlan = null; }
            var avatar = _session.ActiveAvatarRoot == null ? null : _session.ActiveAvatarRoot.GetComponent<VRCAvatarDescriptor>();
            EditorGUILayout.ObjectField(FaceMotionUiText.Get("avatar"), avatar, typeof(VRCAvatarDescriptor), true);
            _clip = (AnimationClip)EditorGUILayout.ObjectField(FaceMotionUiText.Get("animationClip"), _clip, typeof(AnimationClip), false);
            _folder = EditorGUILayout.TextField(FaceMotionUiText.Get("outputFolder"), _folder);
            if (_backend == IntegrationBackendSelection.Direct) EditorGUILayout.HelpBox(FaceMotionUiText.Get("expressionBudgetPolicy"), MessageType.Info);
            var maBackend = _backend == IntegrationBackendSelection.ModularAvatar ? ModularAvatarIntegrationBackendLocator.Create() : null;
            bool showMaRemove = ShouldShowModularAvatarRemove(_backend, maBackend, avatar);
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
                    if (maBackend == null) _optionalPlan = new ModularAvatarOptionalBackend().Plan();
                    else
                    {
                        string name = _session.GetSelectedAnimation() == null ? (_clip == null ? "FaceMotion" : _clip.name) : _session.GetSelectedAnimation().DisplayName;
                        _modularAvatarPlan = maBackend.Plan(new ModularAvatarIntegrationRequest(avatar, _clip, _folder, name));
                        _optionalPlan = null;
                    }
                    _plan = null;
                }
            }
            if (showMaRemove)
            {
                DrawModularAvatarRemove(maBackend, avatar);
            }
            if (_optionalPlan != null)
            {
                EditorGUILayout.LabelField(FaceMotionUiText.Get("optionalBackendStatus"), _optionalPlan.Availability.Reason);
                for (int i = 0; i < _optionalPlan.Diagnostics.Count; i++)
                {
                    var diagnostic = _optionalPlan.Diagnostics[i];
                    EditorGUILayout.HelpBox(FaceMotionUiText.FormatDiagnostic(diagnostic.Code, diagnostic.Message, diagnostic.SuggestedFix), MessageType.Error);
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
                    EditorGUILayout.HelpBox(FaceMotionUiText.FormatDiagnostic(diagnostic.Code, diagnostic.Message, diagnostic.SuggestedFix), diagnostic.Blocking ? MessageType.Error : MessageType.Info);
                }
                using (new EditorGUI.DisabledScope(!_modularAvatarPlan.IsValid || maBackend == null))
                    if (GUILayout.Button(FaceMotionUiText.Get("applyModularAvatarIntegration")))
                    {
                        var result = maBackend.Apply(_modularAvatarPlan);
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
                EditorGUILayout.HelpBox(FaceMotionUiText.FormatDiagnostic(d.Code, d.Message, d.SuggestedFix), d.Blocking ? MessageType.Error : MessageType.Info);
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

        private void DrawModularAvatarRemove(IModularAvatarIntegrationBackend maBackend, VRCAvatarDescriptor avatar)
        {
            if (!GUILayout.Button(FaceMotionUiText.Get("removeModularAvatarIntegration")))
            {
                return;
            }

            var result = maBackend.Remove(avatar);
            if (result.Diagnostics.Count > 0) _session.SetLastOperationDiagnostic(result.Diagnostics[result.Diagnostics.Count - 1]);
            _session.RecomputeDiagnostics();
            _session.NotifyChanged();
        }
    }
}
