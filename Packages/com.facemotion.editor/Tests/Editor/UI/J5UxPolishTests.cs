using FaceMotion.Data;
using FaceMotion.Editor.UI.Guidance;
using FaceMotion.Editor.UI.Localization;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Window;
using FaceMotion.Editor.VRChat.Integration;
using FaceMotion.Integration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// Phase J5 UX-polish regression tests. They cover the pure guidance state machine, the
    /// onboarding/empty-state/tooltip/backend/success localization contract, the advanced-foldout
    /// affordance, and layout safety at the two reference window sizes.
    /// </summary>
    public sealed class J5UxPolishTests
    {
        private static readonly string[] J5Keys =
        {
            "guidanceNextAction", "guidanceStepFormat", "guidanceHintProject", "guidanceHintAvatar", "guidanceHintAnimation",
            "guidanceHintTrack", "guidanceHintPreview", "guidanceHintReady", "guidanceCompleteBadge",
            "emptyAnimations", "emptyTracks", "emptyKeys",
            "shortcutHelpTitle", "shortcutTimelineZoom", "shortcutDeleteKey", "shortcutPreviewCamera", "shortcutFocus",
            "shortcutSelectKey", "shortcutToggleMultiSelect", "shortcutRangeSelect", "shortcutClearSelection",
            "shortcutMoveKeys", "shortcutCopyKeys", "shortcutPasteKeys", "shortcutDuplicateKeys", "shortcutMultiKeyInspector",
            "tooltipBackend", "tooltipDuration", "tooltipFrameRate", "tooltipLoop", "tooltipTrack",
            "tooltipAddToVrchat", "tooltipAdvancedSettings",
            "backendModularAvatarDescription", "backendDirectDescription",
            "successBackendLabel", "successAnimationLabel"
        };

        [Test]
        public void Guidance_ForwardSteps_WalkAllSixBeginnerStepsInOrder()
        {
            AssertStep(FaceMotionWorkflowHintService.Evaluate(false, false, false, false, false),
                FaceMotionUxState.CreateProject, 1, FaceMotionWorkflowHintService.HintCreateProject);
            AssertStep(FaceMotionWorkflowHintService.Evaluate(true, false, false, false, false),
                FaceMotionUxState.SelectAvatar, 2, FaceMotionWorkflowHintService.HintSelectAvatar);
            AssertStep(FaceMotionWorkflowHintService.Evaluate(true, true, false, false, false),
                FaceMotionUxState.CreateAnimation, 3, FaceMotionWorkflowHintService.HintCreateAnimation);
            AssertStep(FaceMotionWorkflowHintService.Evaluate(true, true, true, false, false),
                FaceMotionUxState.StartPreview, 4, FaceMotionWorkflowHintService.HintStartPreview);
            AssertStep(FaceMotionWorkflowHintService.Evaluate(true, true, true, false, true),
                FaceMotionUxState.AddTrack, 5, FaceMotionWorkflowHintService.HintAddTrack);
            AssertStep(FaceMotionWorkflowHintService.Evaluate(true, true, true, true, true),
                FaceMotionUxState.PreviewAndIntegrate, 6, FaceMotionWorkflowHintService.HintPreviewAndIntegrate);
        }

        [Test]
        public void Guidance_BackwardTransition_RemovingAnyPrerequisiteStepsBack()
        {
            // Each case keeps every later prerequisite satisfied, so only the removed one can win.
            AssertStep(FaceMotionWorkflowHintService.Evaluate(true, true, true, true, false),
                FaceMotionUxState.StartPreview, 4, FaceMotionWorkflowHintService.HintStartPreview);
            AssertStep(FaceMotionWorkflowHintService.Evaluate(true, true, true, false, true),
                FaceMotionUxState.AddTrack, 5, FaceMotionWorkflowHintService.HintAddTrack);
            AssertStep(FaceMotionWorkflowHintService.Evaluate(true, true, false, true, true),
                FaceMotionUxState.CreateAnimation, 3, FaceMotionWorkflowHintService.HintCreateAnimation);
            AssertStep(FaceMotionWorkflowHintService.Evaluate(true, false, true, true, true),
                FaceMotionUxState.SelectAvatar, 2, FaceMotionWorkflowHintService.HintSelectAvatar);
            AssertStep(FaceMotionWorkflowHintService.Evaluate(false, true, true, true, true),
                FaceMotionUxState.CreateProject, 1, FaceMotionWorkflowHintService.HintCreateProject);
        }

        [Test]
        public void Guidance_FinalStep_ReportsComplete()
        {
            FaceMotionGuidanceModel model = FaceMotionWorkflowHintService.Evaluate(true, true, true, true, true);
            Assert.That(model.IsComplete, Is.True);
            Assert.That(FaceMotionWorkflowHintService.Evaluate(true, true, true, true, false).IsComplete, Is.False);
        }

        [Test]
        public void Guidance_FinalStep_WordingMatchesTheActualUpdateButton()
        {
            Assert.That(
                FaceMotionUiText.Get(FaceMotionWorkflowHintService.HintPreviewAndIntegrate, SystemLanguage.Japanese),
                Does.Contain(FaceMotionUiText.Get("updateVrchatSelected", SystemLanguage.Japanese)));
            Assert.That(
                FaceMotionUiText.Get(FaceMotionWorkflowHintService.HintPreviewAndIntegrate, SystemLanguage.English),
                Does.Contain(FaceMotionUiText.Get("updateVrchatSelected", SystemLanguage.English)));
        }

        [Test]
        public void Guidance_StepNumber_IsTheUserFacingOrderOfItsState()
        {
            Assert.That((int)FaceMotionUxState.CreateProject, Is.EqualTo(1));
            Assert.That((int)FaceMotionUxState.SelectAvatar, Is.EqualTo(2));
            Assert.That((int)FaceMotionUxState.CreateAnimation, Is.EqualTo(3));
            Assert.That((int)FaceMotionUxState.StartPreview, Is.EqualTo(4));
            Assert.That((int)FaceMotionUxState.AddTrack, Is.EqualTo(5));
            Assert.That((int)FaceMotionUxState.PreviewAndIntegrate, Is.EqualTo(6));
        }

        [Test]
        public void Guidance_NullSession_ReportsTheFirstStep()
        {
            FaceMotionGuidanceModel model = FaceMotionWorkflowHintService.Evaluate((FaceMotionEditorSession)null, false);
            Assert.That(model.State, Is.EqualTo(FaceMotionUxState.CreateProject));
            Assert.That(model.StepNumber, Is.EqualTo(1));
        }

        [Test]
        public void Guidance_EmptySession_ResolvesThroughSessionOverload()
        {
            var session = new FaceMotionEditorSession();
            FaceMotionGuidanceModel model = FaceMotionWorkflowHintService.Evaluate(session, false);
            Assert.That(model.State, Is.EqualTo(FaceMotionUxState.CreateProject));
        }

        [Test]
        public void Guidance_SessionWithProjectOnly_AsksForAvatar()
        {
            var session = new FaceMotionEditorSession();
            session.SetActiveProject(FaceMotionProject.CreateNew(), "Assets/__FM_J5_Test.asset");
            FaceMotionGuidanceModel model = FaceMotionWorkflowHintService.Evaluate(session, false);
            Assert.That(model.State, Is.EqualTo(FaceMotionUxState.SelectAvatar));
            Assert.That(model.StepNumber, Is.EqualTo(2));
        }

        private static void AssertStep(FaceMotionGuidanceModel model, FaceMotionUxState state, int step, string hintKey)
        {
            Assert.That(model.State, Is.EqualTo(state));
            Assert.That(model.StepNumber, Is.EqualTo(step), state.ToString());
            Assert.That(model.HintKey, Is.EqualTo(hintKey), state.ToString());
        }

        [Test]
        public void Localization_JapaneseDefaultCoversEveryJ5Key()
        {
            for (int i = 0; i < J5Keys.Length; i++)
            {
                Assert.That(FaceMotionUiText.Get(J5Keys[i]), Is.Not.EqualTo(J5Keys[i]), J5Keys[i]);
            }
        }

        [Test]
        public void Localization_EnglishFallbackCoversEveryJ5Key()
        {
            for (int i = 0; i < J5Keys.Length; i++)
            {
                Assert.That(
                    FaceMotionUiText.Get(J5Keys[i], SystemLanguage.English),
                    Is.Not.EqualTo(J5Keys[i]),
                    J5Keys[i]);
            }
        }

        [Test]
        public void Localization_UnifiedTerminology_IsPreserved()
        {
            Assert.That(FaceMotionUiText.Get("animations"), Is.EqualTo("アニメーション"));
            Assert.That(FaceMotionUiText.Get("tracks"), Does.Contain("トラック"));
            Assert.That(FaceMotionUiText.Get("keyInspector"), Does.Contain("キー"));
            Assert.That(FaceMotionUiText.Get("preview"), Is.EqualTo("プレビュー"));
            Assert.That(FaceMotionUiText.Get("directIntegration"), Does.Contain("統合"));
            Assert.That(FaceMotionUiText.Get("integrationBackend"), Does.Contain("統合"));
            Assert.That(FaceMotionUiText.Get("tooltipAddToVrchat"), Does.Contain("統合"));
        }

        [Test]
        public void BackendDescription_DistinguishesBothBackends()
        {
            Assert.That(FaceMotionUiText.Get("backendModularAvatarDescription"), Does.Contain("Modular Avatar"));
            Assert.That(FaceMotionUiText.Get("backendDirectDescription"), Does.Contain("Direct"));
        }

        [Test]
        public void BackendLabel_MapsBackendIdsToUserFacingNames()
        {
            Assert.That(
                OneClickIntegrationPanel.BackendLabel(OneClickIntegrationService.ModularAvatarBackendId),
                Is.EqualTo(FaceMotionUiText.Get("modularAvatarBackend")));
            Assert.That(
                OneClickIntegrationPanel.BackendLabel(DirectVRChatIntegration.BackendId),
                Is.EqualTo(FaceMotionUiText.Get("directBackend")));
        }

        [Test]
        public void BackendSelection_DefaultsToModularAvatarWhenAvailable()
        {
            Assert.That(
                IntegrationBackendSelectionStore.ResolveInitial(false, IntegrationBackendSelection.Direct, true),
                Is.EqualTo(IntegrationBackendSelection.ModularAvatar));
            Assert.That(
                IntegrationBackendSelectionStore.ResolveInitial(false, IntegrationBackendSelection.Direct, false),
                Is.EqualTo(IntegrationBackendSelection.Direct));
        }

        [Test]
        public void ShortcutHelp_ExposesExpectedShortcuts_Localized()
        {
            Assert.That(ShortcutHelpPanel.ShortcutKeys, Has.Length.EqualTo(13));
            for (int i = 0; i < ShortcutHelpPanel.ShortcutKeys.Length; i++)
            {
                string key = ShortcutHelpPanel.ShortcutKeys[i];
                Assert.That(FaceMotionUiText.Get(key), Is.Not.EqualTo(key), key);
                Assert.That(FaceMotionUiText.Get(key, SystemLanguage.English), Is.Not.EqualTo(key), key);
            }
        }

        [Test]
        public void AdvancedFoldout_WindowOwned_DefaultsClosed_AndPersistsOpening()
        {
            bool original = FaceMotionWindow.ReadAdvancedFoldoutPref();
            try
            {
                EditorPrefs.DeleteKey(FaceMotionWindow.AdvancedFoldoutKey);
                Assert.That(FaceMotionWindow.ReadAdvancedFoldoutPref(), Is.False);

                EditorPrefs.SetBool(FaceMotionWindow.AdvancedFoldoutKey, true);
                Assert.That(FaceMotionWindow.ReadAdvancedFoldoutPref(), Is.True);
            }
            finally
            {
                if (original)
                {
                    EditorPrefs.SetBool(FaceMotionWindow.AdvancedFoldoutKey, true);
                }
                else
                {
                    EditorPrefs.DeleteKey(FaceMotionWindow.AdvancedFoldoutKey);
                }
            }
        }

        [Test]
        public void AdvancedManualSurface_RemainsAccessibleBehindFoldout()
        {
            Assert.That(typeof(DirectVRChatIntegrationPanel).GetMethod("OnGUI"), Is.Not.Null);
        }

        [Test]
        public void DisabledActions_ProvideExplanationText()
        {
            Assert.That(FaceMotionUiText.Get("selectAnimationToExport"), Is.Not.EqualTo("selectAnimationToExport"));
            Assert.That(FaceMotionUiText.Get("selectAvatar"), Is.Not.EqualTo("selectAvatar"));
            Assert.That(FaceMotionUiText.Get("tooltipAddToVrchat"), Is.Not.EqualTo("tooltipAddToVrchat"));
        }

        [TestCase(1366f, 768f)]
        [TestCase(1920f, 1080f)]
        public void Layout_AtReferenceSizes_PreservesTimelineAndInspector(float width, float height)
        {
            float contentHeight = height - 22f - FaceMotionWindow.GuidanceHeight;
            Assert.That(contentHeight, Is.GreaterThan(0f));

            float left = FaceMotionWindow.CalculateLeftColumnWidth(width, 1f);
            float preview = FaceMotionWindow.CalculatePreviewHeight(contentHeight, 1f);

            Assert.That(
                width - left - FaceMotionWindow.SplitterWidth,
                Is.GreaterThanOrEqualTo(FaceMotionWindow.MinimumTimelineWidth));
            Assert.That(
                contentHeight - preview - FaceMotionWindow.SplitterWidth - FaceMotionWindow.InspectorHeight,
                Is.GreaterThanOrEqualTo(FaceMotionWindow.MinimumTimelineHeight));
        }

        [Test]
        public void GuidanceStrip_HasPositiveReservedHeight()
        {
            Assert.That(FaceMotionWindow.GuidanceHeight, Is.GreaterThan(0f));
        }
    }
}
