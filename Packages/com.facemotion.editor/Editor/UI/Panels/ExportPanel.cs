using FaceMotion.Editor.Export;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Localization;
using FaceMotion.Editor.VRChat.Integration;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.UI.Panels
{
    /// <summary>Explicit Phase F AnimationClip export UI. It creates no VRChat integration assets.</summary>
    public sealed class ExportPanel
    {
        private readonly FaceMotionEditorSession _session;
        private string _path = "Assets/FaceMotion/Exports/FaceMotion.anim";

        public ExportPanel(FaceMotionEditorSession session) { _session = session; }

        public void OnGUI()
        {
            EditorGUILayout.LabelField(FaceMotionUiText.Get("animationClipExport"), EditorStyles.boldLabel);
            var animation = _session.GetSelectedAnimation();
            if (animation == null)
            {
                EditorGUILayout.HelpBox(FaceMotionUiText.Get("selectAnimationToExport"), MessageType.Info);
                return;
            }

            _path = EditorGUILayout.TextField(FaceMotionUiText.Get("assetPath"), _path);
            if (GUILayout.Button(FaceMotionUiText.Get("chooseExportPath")))
            {
                string selected = EditorUtility.SaveFilePanelInProject(FaceMotionUiText.Get("exportAnimationClipDialog"), animation.DisplayName, "anim", FaceMotionUiText.Get("exportPathPrompt"));
                if (!string.IsNullOrEmpty(selected)) _path = selected;
            }

            var validation = AnimationClipExporter.Validate(animation, _path);
            for (int i = 0; i < validation.Diagnostics.Count; i++)
            {
                var diagnostic = validation.Diagnostics[i];
                string message = FaceMotionUiText.FormatDiagnostic(diagnostic.Code, diagnostic.Message, diagnostic.SuggestedFix);
                EditorGUILayout.HelpBox(message, diagnostic.Blocking ? MessageType.Error : MessageType.Warning);
            }
            using (new EditorGUI.DisabledScope(!validation.IsValid))
            {
                if (GUILayout.Button(FaceMotionUiText.Get("exportAnimationClip")))
                {
                    var result = AnimationClipExporter.Export(animation, _path);
                    if (result.Succeeded) ExportedClipRegistry.Record(animation.AnimationId, _path);
                    _session.SetLastOperationDiagnostic(result.Diagnostics[result.Diagnostics.Count - 1]);
                    _session.RecomputeDiagnostics();
                    _session.NotifyChanged();
                    if (result.Succeeded) Selection.activeObject = result.Clip;
                }
            }
        }
    }
}
