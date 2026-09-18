using FaceMotion.Data;
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
    /// The one-click "VRChatへ追加" surface. It runs Export -> Plan -> Validate -> Apply,
    /// shows a write-free preflight summary, reports which step failed, and keeps the
    /// explicit Phase G workflow inside a 詳細設定 foldout. All workflow logic lives in
    /// OneClickIntegrationService; this panel only paints and invokes.
    /// </summary>
    public sealed class OneClickIntegrationPanel
    {
        private const string AdvancedFoldoutKey = "FaceMotion.Window.v2.OneClickAdvancedFoldout";
        private readonly FaceMotionEditorSession _session;
        private readonly OneClickIntegrationController _controller;
        private readonly BatchIntegrationController _batchController;
        private readonly DirectVRChatIntegrationPanel _advanced;
        private bool _advancedFoldout;

        public OneClickIntegrationPanel(FaceMotionEditorSession session)
        {
            _session = session;
            _controller = new OneClickIntegrationController(session);
            _batchController = new BatchIntegrationController(session);
            _advanced = new DirectVRChatIntegrationPanel(session);
            _advancedFoldout = EditorPrefs.GetBool(AdvancedFoldoutKey, false);
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField(FaceMotionUiText.Get("oneClickIntegration"), EditorStyles.boldLabel);
            var animation = _session.GetSelectedAnimation();
            var avatar = _session.ActiveAvatarRoot == null
                ? null
                : _session.ActiveAvatarRoot.GetComponent<VRCAvatarDescriptor>();

            if (animation == null)
            {
                EditorGUILayout.HelpBox(FaceMotionUiText.Get("selectAnimationToExport"), MessageType.Info);
                DrawDisabledButton();
            }
            else if (avatar == null)
            {
                EditorGUILayout.HelpBox(FaceMotionUiText.Get("selectAvatar"), MessageType.Info);
                DrawDisabledButton();
            }
            else
            {
                DrawPreflight();
            }

            DrawResult();
            DrawBatch();
            EditorGUILayout.Space();
            DrawAdvancedFoldout();
        }

        private void DrawBatch()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(FaceMotionUiText.Get("batchIntegration"), EditorStyles.boldLabel);
            int count = _session.BatchAnimationIds.Count;
            EditorGUILayout.LabelField(FaceMotionUiText.Get("batchCheckedCount"), count.ToString());
            bool ready = count > 0 && _session.ActiveAvatarRoot != null;
            using (new EditorGUI.DisabledScope(!ready))
            {
                if (GUILayout.Button(new GUIContent(FaceMotionUiText.Get("batchAddToVrchat"), FaceMotionUiText.Get("tooltipBatchAddToVrchat")), GUILayout.Height(28f))) RunBatch();
            }
            if (!ready) EditorGUILayout.HelpBox(count == 0 ? FaceMotionUiText.Get("batchSelectAnimations") : FaceMotionUiText.Get("selectAvatar"), MessageType.Info);

            var result = _batchController.LastResult;
            if (result == null) return;
            EditorGUILayout.LabelField(result.Succeeded ? FaceMotionUiText.Get("batchResultSucceeded") : FaceMotionUiText.Get("batchResultFailed"), EditorStyles.boldLabel);
            for (int i = 0; i < result.Items.Count; i++)
            {
                var item = result.Items[i];
                EditorGUILayout.LabelField((item.Succeeded ? "OK  " : "FAIL  ") + item.DisplayName, EditorStyles.miniLabel);
            }
            for (int i = 0; i < result.Diagnostics.Count; i++)
            {
                var diagnostic = result.Diagnostics[i];
                if (diagnostic.Blocking) EditorGUILayout.HelpBox(DirectVRChatIntegrationPanel.FormatDiagnostic(diagnostic), MessageType.Error);
            }
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

        private void DrawAdvancedFoldout()
        {
            bool next = EditorGUILayout.Foldout(_advancedFoldout, new GUIContent(FaceMotionUiText.Get("advancedSettings"), FaceMotionUiText.Get("tooltipAdvancedSettings")), true);
            if (next != _advancedFoldout)
            {
                _advancedFoldout = next;
                EditorPrefs.SetBool(AdvancedFoldoutKey, next);
            }

            if (_advancedFoldout)
            {
                EditorGUILayout.Space();
                _advanced.OnGUI();
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

        private void RunBatch()
        {
            try
            {
                _batchController.Execute((stage, current, total) => EditorUtility.DisplayProgressBar(
                    "FaceMotion Batch Integration",
                    BatchProgressLabel(stage) + " " + current + " / " + total,
                    total <= 0 ? 0f : (float)current / total));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static string BatchProgressLabel(BatchIntegrationStage stage)
        {
            switch (stage)
            {
                case BatchIntegrationStage.Plan: return FaceMotionUiText.Get("oneClickStagePlan");
                case BatchIntegrationStage.Validate: return FaceMotionUiText.Get("oneClickStageValidate");
                case BatchIntegrationStage.Apply: return FaceMotionUiText.Get("oneClickStageApply");
                default: return FaceMotionUiText.Get("oneClickStageExport");
            }
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
