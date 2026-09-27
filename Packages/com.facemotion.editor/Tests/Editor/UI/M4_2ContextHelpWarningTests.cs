using System;
using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.UI.Diagnostics;
using FaceMotion.Editor.UI.Guidance;
using FaceMotion.Editor.UI.Localization;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Support;
using FaceMotion.Editor.VRChat.Integration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    public sealed class M4_2ContextHelpWarningTests
    {
        [Test]
        public void ContextHelp_TogglesWithoutDirtyingProjectOrMutatingSession()
        {
            var project = FaceMotionProject.CreateNew();
            var session = new FaceMotionEditorSession();
            try
            {
                session.SetActiveProject(project, string.Empty);
                EditorUtility.ClearDirty(project);
                int version = session.Version;
                bool open = ContextHelp.Toggle(false);
                Assert.That(open, Is.True);
                Assert.That(ContextHelp.Text("contextHelpAnimation"), Is.Not.Empty);
                Assert.That(ContextHelp.Toggle(open), Is.False);
                Assert.That(FaceMotionWorkflowHintService.Evaluate(session, false).State, Is.EqualTo(FaceMotionUxState.SelectAvatar));
                Assert.That(session.Version, Is.EqualTo(version));
                Assert.That(project.Animations, Is.Empty);
                Assert.That(EditorUtility.IsDirty(project), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(project);
            }
        }

        [Test]
        public void HelpAndWarningKeys_AreLocalizedForJapaneseAndEnglish()
        {
            foreach (string key in new[] { "contextHelpTooltip", "contextHelpAnimation", "contextHelpAddTrack",
                         "contextHelpAddKey", "contextHelpTrack", "contextHelpPreview", "contextHelpVrchat",
                         "browserWarningFx", "browserWarningFxHelp", "browserWarningMa", "browserWarningMaHelp",
                         "browserWarningOther", "browserWarningOtherHelp", "conflictBindingFx",
                         "selectAvatarForIntegration", "selectProjectForIntegration" })
            {
                Assert.That(FaceMotionUiText.Get(key, SystemLanguage.Japanese), Is.Not.EqualTo(key), key);
                Assert.That(FaceMotionUiText.Get(key, SystemLanguage.English), Is.Not.EqualTo(key), key);
            }
        }

        [TestCase("Existing FX Animator", "FX")]
        [TestCase("Modular Avatar Merge Animator", "Modular Avatar")]
        public void BlendShapeWarning_ExplainsTheSourceAndNextAction(string reason, string source)
        {
            var candidate = new AvatarCandidateSnapshot.BlendShapeCandidate("Face", "Smile",
                conflictStatus: AvatarCandidateSnapshot.BlendShapeConflictStatus.Warning, conflictReason: reason);
            string jaLabel = TrackListPanel.CandidateWarningLabel(candidate);
            string jaHelp = TrackListPanel.CandidateWarningText(candidate);
            Assert.That(jaLabel, Does.Contain(source));
            Assert.That(jaLabel, Does.Not.Contain("注意"));
            Assert.That(jaHelp, Does.Contain("確認"));
            Assert.That(jaHelp, Does.Contain("表情"));
            Assert.That(jaHelp, Is.Not.EqualTo("[注意]"));
        }

        [Test]
        public void CrossBinding_BeginnerTextGivesFXReasonAndAction_TechnicalCodeSurvives()
        {
            var diagnostic = CrossBinding();
            var result = new VrchatDesiredStateReconciliationResult(false,
                VrchatDesiredStateReconciliationOutcome.PreflightFailed,
                new List<VrchatDesiredStateReconciliationItem>(), new[] { diagnostic, diagnostic });
            DesiredStateResultPresentation presentation = DesiredStateResultPresenter.Build(result);
            Assert.That(presentation.HasCrossBindingConflict, Is.True);
            Assert.That(DesiredStateResultPresenter.ConflictBeginnerKey(presentation),
                Is.EqualTo(DesiredStateResultPresenter.ConflictBeginnerFx));
            string ja = FaceMotionUiText.Get(DesiredStateResultPresenter.ConflictBeginnerFx, SystemLanguage.Japanese);
            Assert.That(ja, Does.Contain("既存のFX Animator"));
            Assert.That(ja, Does.Contain("確認"));
            Assert.That(ja, Does.Contain("反映対象から外"));
            Assert.That(ja, Does.Not.Contain(diagnostic.Code));
            Assert.That(presentation.TechnicalDetails, Has.Count.EqualTo(1), "Existing diagnostic dedupe is retained.");
            Assert.That(presentation.TechnicalDetails[0], Does.Contain(diagnostic.Code));

            var localized = new FaceMotionDiagnosticPresentation(diagnostic, FaceMotionDiagnosticLanguage.Japanese);
            Assert.That(localized.Summary, Does.Contain("既存のFX Animator"));
            Assert.That(localized.Resolution, Does.Contain("確認"));
            Assert.That(localized.Code, Is.EqualTo(diagnostic.Code));
            Assert.That(localized.Severity, Is.EqualTo(diagnostic.Severity));
            Assert.That(localized.Diagnostic.Blocking, Is.True);
        }

        [Test]
        public void WarningSeverity_RemainsWarningAndNonBlocking()
        {
            var diagnostic = new FaceMotionDiagnostic(FaceMotionDiagnosticCodes.ModularAvatarSharedBindingWarning,
                FaceMotionDiagnosticSeverity.Warning, "Shared expression", "Face", false, "Review overlapping layers");
            var presentation = new FaceMotionDiagnosticPresentation(diagnostic, FaceMotionDiagnosticLanguage.Japanese);
            Assert.That(presentation.Severity, Is.EqualTo(FaceMotionDiagnosticSeverity.Warning));
            Assert.That(presentation.Diagnostic.Blocking, Is.False);
            Assert.That(DirectVRChatIntegrationPanel.DiagnosticMessageType(diagnostic), Is.EqualTo(MessageType.Warning));
            Assert.That(DirectVRChatIntegrationPanel.DiagnosticMessageType(CrossBinding()), Is.EqualTo(MessageType.Error));
        }

        [TestCase(SystemLanguage.Japanese)]
        [TestCase(SystemLanguage.English)]
        public void PreviewHelp_NarrowLayoutReservesSpaceWithoutCoveringControls(SystemLanguage language)
        {
            var panel = new Rect(0f, 0f, 180f, 400f);
            PreviewPanelLayout layout = PreviewPanel.CalculateLayout(panel, true, language);
            PreviewControlsLayout controls = PreviewPanel.CalculateControlsLayout(layout.ControlsRect);
            Assert.That(layout.ControlsRect.y, Is.GreaterThanOrEqualTo(
                layout.HeaderRect.yMax + PreviewPanel.Padding + PreviewPanel.HelpHeight(layout.HeaderRect.width, language)));
            PreviewHeaderLayout header = PreviewPanel.CalculateHeaderLayout(layout.HeaderRect, language);
            Assert.That(header.TitleRect.xMax, Is.LessThanOrEqualTo(header.IsolationRect.x + 0.001f));
            Assert.That(header.IsolationRect.width, Is.GreaterThanOrEqualTo(0f));
            Assert.That(header.IsolationRect.xMax, Is.LessThanOrEqualTo(header.HelpRect.x + 0.001f));
            Assert.That(header.HelpRect.xMax, Is.EqualTo(layout.HeaderRect.xMax).Within(0.001f));
            foreach (Rect rect in new[] { controls.Play, controls.PlaybackStop, controls.Fit, controls.Reset })
            {
                Assert.That(rect.xMax, Is.LessThanOrEqualTo(layout.ControlsRect.xMax));
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(layout.ControlsRect.yMax));
            }
            Assert.That(layout.RenderRect.y, Is.GreaterThanOrEqualTo(layout.ControlsRect.yMax));
        }

        private static FaceMotionDiagnostic CrossBinding()
        {
            return new FaceMotionDiagnostic(FaceMotionDiagnosticCodes.ModularAvatarCrossBindingConflict,
                FaceMotionDiagnosticSeverity.Error, "FX conflict", "Face", true, "Check FX",
                new Dictionary<string, string>
                {
                    { FaceMotionDiagnosticDetailKeys.Binding, "Face / SkinnedMeshRenderer / blendShape.Smile" }
                });
        }
    }
}
