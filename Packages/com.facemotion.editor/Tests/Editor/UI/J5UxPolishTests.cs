using System.Reflection;
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
        private const string AdvancedFoldoutKey = "FaceMotion.Window.v2.OneClickAdvancedFoldout";

        private static readonly string[] J5Keys =
        {
            "guidanceNextAction", "guidanceStepFormat", "guidanceHintAvatar", "guidanceHintAnimation",
            "guidanceHintTrack", "guidanceHintKey", "guidanceHintReady", "guidanceCompleteBadge",
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
        public void Guidance_NoAvatar_AsksForAvatar()
        {
            FaceMotionGuidanceModel model = FaceMotionWorkflowHintService.Evaluate(false, false, false, false);
            Assert.That(model.State, Is.EqualTo(FaceMotionUxState.SelectAvatar));
            Assert.That(model.StepNumber, Is.EqualTo(1));
            Assert.That(model.HintKey, Is.EqualTo(FaceMotionWorkflowHintService.HintSelectAvatar));
            Assert.That(model.IsComplete, Is.False);
        }

        [Test]
        public void Guidance_AvatarWithoutAnimation_AsksForAnimation()
        {
            FaceMotionGuidanceModel model = FaceMotionWorkflowHintService.Evaluate(true, false, false, false);
            Assert.That(model.State, Is.EqualTo(FaceMotionUxState.CreateAnimation));
            Assert.That(model.StepNumber, Is.EqualTo(2));
            Assert.That(model.HintKey, Is.EqualTo(FaceMotionWorkflowHintService.HintCreateAnimation));
        }

        [Test]
        public void Guidance_AnimationWithoutTrack_AsksForTrack()
        {
            FaceMotionGuidanceModel model = FaceMotionWorkflowHintService.Evaluate(true, true, false, false);
            Assert.That(model.State, Is.EqualTo(FaceMotionUxState.AddTrack));
            Assert.That(model.StepNumber, Is.EqualTo(3));
            Assert.That(model.HintKey, Is.EqualTo(FaceMotionWorkflowHintService.HintAddTrack));
        }

        [Test]
        public void Guidance_TrackWithoutKey_AsksForKey()
        {
            FaceMotionGuidanceModel model = FaceMotionWorkflowHintService.Evaluate(true, true, true, false);
            Assert.That(model.State, Is.EqualTo(FaceMotionUxState.AddKey));
            Assert.That(model.StepNumber, Is.EqualTo(4));
            Assert.That(model.HintKey, Is.EqualTo(FaceMotionWorkflowHintService.HintAddKey));
        }

        [Test]
        public void Guidance_KeyPresent_AsksToPreviewAndIntegrate()
        {
            FaceMotionGuidanceModel model = FaceMotionWorkflowHintService.Evaluate(true, true, true, true);
            Assert.That(model.State, Is.EqualTo(FaceMotionUxState.PreviewAndIntegrate));
            Assert.That(model.StepNumber, Is.EqualTo(5));
            Assert.That(model.HintKey, Is.EqualTo(FaceMotionWorkflowHintService.HintPreviewAndIntegrate));
            Assert.That(model.IsComplete, Is.True);
        }

        [Test]
        public void Guidance_NullSession_FallsBackToSelectAvatar()
        {
            FaceMotionGuidanceModel model = FaceMotionWorkflowHintService.Evaluate((FaceMotionEditorSession)null);
            Assert.That(model.State, Is.EqualTo(FaceMotionUxState.SelectAvatar));
        }

        [Test]
        public void Guidance_EmptySession_ResolvesThroughSessionOverload()
        {
            var session = new FaceMotionEditorSession();
            FaceMotionGuidanceModel model = FaceMotionWorkflowHintService.Evaluate(session);
            Assert.That(model.State, Is.EqualTo(FaceMotionUxState.SelectAvatar));
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
        public void AdvancedFoldout_DefaultsClosed_AndPersistsOpening()
        {
            bool original = EditorPrefs.GetBool(AdvancedFoldoutKey, false);
            try
            {
                EditorPrefs.DeleteKey(AdvancedFoldoutKey);
                Assert.That(ReadAdvancedFoldout(new OneClickIntegrationPanel(new FaceMotionEditorSession())), Is.False);

                EditorPrefs.SetBool(AdvancedFoldoutKey, true);
                Assert.That(ReadAdvancedFoldout(new OneClickIntegrationPanel(new FaceMotionEditorSession())), Is.True);
            }
            finally
            {
                if (original)
                {
                    EditorPrefs.SetBool(AdvancedFoldoutKey, true);
                }
                else
                {
                    EditorPrefs.DeleteKey(AdvancedFoldoutKey);
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

        private static bool ReadAdvancedFoldout(OneClickIntegrationPanel panel)
        {
            FieldInfo field = typeof(OneClickIntegrationPanel)
                .GetField("_advancedFoldout", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (bool)field.GetValue(panel);
        }
    }
}
