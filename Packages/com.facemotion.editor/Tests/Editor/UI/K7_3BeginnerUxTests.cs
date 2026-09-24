using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.UI.Controllers;
using FaceMotion.Editor.UI.Guidance;
using FaceMotion.Editor.UI.Localization;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Editor.UI.Session;
using FaceMotion.Editor.UI.Window;
using FaceMotion.Editor.VRChat.Integration;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// Phase K7-3 beginner-first UI regression tests. They pin the editing-selection
    /// presentation, the single VRChat checklist + Update button, the structured beginner
    /// result messages (A-E), the binding-conflict beginner explanation, and the single
    /// window-owned Advanced foldout that hides manual export, per-animation add, and
    /// AvatarIndex rebuild from the normal workflow.
    /// </summary>
    public sealed class K7_3BeginnerUxTests
    {
        private static readonly string[] BeginnerMessageKeys =
        {
            DesiredStateResultPresenter.TitleSucceeded,
            DesiredStateResultPresenter.CountSucceeded,
            DesiredStateResultPresenter.TitleNoChange,
            DesiredStateResultPresenter.TitleRemovalComplete,
            DesiredStateResultPresenter.TitleFailedRolledBack,
            DesiredStateResultPresenter.TitleFailedRollbackFailed,
            DesiredStateResultPresenter.TitlePreflightFailed,
            DesiredStateResultPresenter.TitleFailed,
            DesiredStateResultPresenter.ConflictBeginnerGeneric,
            DesiredStateResultPresenter.ConflictBeginnerNamed,
            "emptySelectionRemovesIntegrations"
        };

        [Test]
        public void EditingSelection_RowLabel_MarksCurrentAnimation()
        {
            string selected = AnimationListPanel.RowLabel("shirome", true);
            Assert.That(selected, Does.Contain(FaceMotionUiText.Get("editing")));
            Assert.That(selected, Does.Contain("shirome"));
        }

        [Test]
        public void EditingSelection_UnselectedRow_HasNoEditingSuffix()
        {
            Assert.That(AnimationListPanel.RowLabel("shirome", false), Does.Not.Contain(FaceMotionUiText.Get("editing")));
        }

        [Test]
        public void AnimationListPanel_ExposesNoVrchatChecklistSurface()
        {
            Assert.That(AnimationListPanelChecklistMember(), Is.Empty);
            Assert.That(typeof(AnimationListPanel).GetMethod(
                "DrawDesiredAnimationChecklist",
                BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
            Assert.That(typeof(AnimationListPanel).GetMethod(
                "OnGUI",
                BindingFlags.Instance | BindingFlags.Public)
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .Contains(typeof(VRCAvatarDescriptor)), Is.False);
        }

        [Test]
        public void VrchatChecklist_CombinesAnimationNameAndStatus()
        {
            string applied = OneClickIntegrationPanel.ChecklistItemLabel("shirome", VrchatIntegrationStatus.Applied);
            Assert.That(applied, Does.Contain("shirome"));
            Assert.That(applied, Does.Contain(FaceMotionUiText.Get("applied")));

            string notApplied = OneClickIntegrationPanel.ChecklistItemLabel("shirome", VrchatIntegrationStatus.NotApplied);
            Assert.That(notApplied, Does.Contain(FaceMotionUiText.Get("notApplied")));
        }

        [Test]
        public void VrchatChecklist_IsOwnedByIntegrationPanel_WithManagedStateCache()
        {
            var field = typeof(OneClickIntegrationPanel)
                .GetField("_managedStateCache", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            Assert.That(field.FieldType, Is.EqualTo(typeof(ModularAvatarManagedStateCache)));
            Assert.That(typeof(OneClickIntegrationPanel).GetMethod(
                "DrawDesiredAnimationChecklist",
                BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null);
        }

        [Test]
        public void PrimaryUpdateButton_HasSingleNormalActionWithDesiredStateReconciliation()
        {
            Assert.That(OneClickIntegrationPanel.UpdateVrchatSelectedLabel(), Is.EqualTo(FaceMotionUiText.Get("updateVrchatSelected")));
            Assert.That(FaceMotionUiText.Get("updateVrchatSelected"), Is.Not.EqualTo("updateVrchatSelected"));
            Assert.That(typeof(OneClickIntegrationPanel).GetMethod(
                "RunDesiredState",
                BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null);
            Assert.That(typeof(OneClickIntegrationPanel).GetField(
                "_batchController",
                BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
            Assert.That(typeof(OneClickIntegrationPanel).GetMethod(
                "RunBatch",
                BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
        }

        [Test]
        public void ZeroDesiredSelection_LeavesTheNormalUpdateActionAvailable()
        {
            Assert.That(OneClickIntegrationPanel.CanUpdateVrchat(true, true, true), Is.True);
            Assert.That(OneClickIntegrationPanel.CanUpdateVrchat(false, true, true), Is.False);
            Assert.That(OneClickIntegrationPanel.CanUpdateVrchat(true, false, true), Is.False);
            Assert.That(OneClickIntegrationPanel.CanUpdateVrchat(true, true, false), Is.False);
            Assert.That(OneClickIntegrationPanel.EmptyDesiredSelectionHint(1), Does.Contain("削除"));
            Assert.That(OneClickIntegrationPanel.EmptyDesiredSelectionHint(0), Is.Empty);
            Assert.That(FaceMotionUiText.Get("selectAnimationsToReflect"), Is.EqualTo("selectAnimationsToReflect"));
        }

        [Test]
        public void DesiredResult_SucceededWithAdds_ShowsCompleteAndCount()
        {
            var result = Result(
                VrchatDesiredStateReconciliationOutcome.Succeeded,
                true,
                Item("alpha", VrchatDesiredStateAction.Add),
                Item("existing", VrchatDesiredStateAction.Keep));

            DesiredStateResultPresentation presentation = DesiredStateResultPresenter.Build(result);

            Assert.That(presentation.Kind, Is.EqualTo(DesiredStateResultKind.Succeeded));
            Assert.That(presentation.TitleKey, Is.EqualTo(DesiredStateResultPresenter.TitleSucceeded));
            Assert.That(presentation.AppliedCount, Is.EqualTo(1));
        }

        [Test]
        public void DesiredResult_NoChange_ShowsAlreadyLatest()
        {
            var result = Result(
                VrchatDesiredStateReconciliationOutcome.Succeeded,
                true,
                Item("a", VrchatDesiredStateAction.Keep),
                Item("b", VrchatDesiredStateAction.Keep));

            DesiredStateResultPresentation presentation = DesiredStateResultPresenter.Build(result);

            Assert.That(presentation.Kind, Is.EqualTo(DesiredStateResultKind.NoChange));
            Assert.That(presentation.TitleKey, Is.EqualTo(DesiredStateResultPresenter.TitleNoChange));
        }

        [Test]
        public void DesiredResult_EmptyCurrentAndDesiredState_ShowsAlreadyLatest()
        {
            DesiredStateResultPresentation presentation = DesiredStateResultPresenter.Build(Result(
                VrchatDesiredStateReconciliationOutcome.Succeeded,
                true));

            Assert.That(presentation.Kind, Is.EqualTo(DesiredStateResultKind.NoChange));
            Assert.That(presentation.TitleKey, Is.EqualTo(DesiredStateResultPresenter.TitleNoChange));
        }

        [Test]
        public void DesiredResult_EmptyDesiredRemoval_ShowsRemovalComplete()
        {
            var result = Result(
                VrchatDesiredStateReconciliationOutcome.Succeeded,
                true,
                Item("obsolete", VrchatDesiredStateAction.Remove));

            DesiredStateResultPresentation presentation = DesiredStateResultPresenter.Build(result);

            Assert.That(presentation.Kind, Is.EqualTo(DesiredStateResultKind.RemovalComplete));
            Assert.That(presentation.TitleKey, Is.EqualTo(DesiredStateResultPresenter.TitleRemovalComplete));
        }

        [Test]
        public void DesiredResult_FailedRolledBack_ShowsRestoredMessage()
        {
            var result = Result(
                VrchatDesiredStateReconciliationOutcome.FailedRolledBack,
                false,
                Item("a", VrchatDesiredStateAction.Add));

            DesiredStateResultPresentation presentation = DesiredStateResultPresenter.Build(result);

            Assert.That(presentation.Kind, Is.EqualTo(DesiredStateResultKind.FailedRolledBack));
            Assert.That(presentation.TitleKey, Is.EqualTo(DesiredStateResultPresenter.TitleFailedRolledBack));
        }

        [Test]
        public void DesiredResult_FailedRollbackFailed_ShowsStrongWarningAndNeverClaimsRestoreOrSuccess()
        {
            var result = Result(
                VrchatDesiredStateReconciliationOutcome.FailedRollbackFailed,
                false,
                Item("a", VrchatDesiredStateAction.Add));

            DesiredStateResultPresentation presentation = DesiredStateResultPresenter.Build(result);

            Assert.That(presentation.Kind, Is.EqualTo(DesiredStateResultKind.FailedRollbackFailed));
            Assert.That(presentation.TitleKey, Is.EqualTo(DesiredStateResultPresenter.TitleFailedRollbackFailed));
            string text = FaceMotionUiText.Get(DesiredStateResultPresenter.TitleFailedRollbackFailed);
            Assert.That(text, Does.Contain("復元できませんでした"));
            Assert.That(text, Does.Not.Contain("戻しました"));
        }

        [Test]
        public void DesiredResult_PreflightFailed_ShowsNoMutatedStateMessage()
        {
            var result = Result(VrchatDesiredStateReconciliationOutcome.PreflightFailed, false);

            DesiredStateResultPresentation presentation = DesiredStateResultPresenter.Build(result);

            Assert.That(presentation.Kind, Is.EqualTo(DesiredStateResultKind.PreflightFailed));
            Assert.That(presentation.TitleKey, Is.EqualTo(DesiredStateResultPresenter.TitlePreflightFailed));
        }

        [Test]
        public void DesiredResult_FailedWithoutRollback_ShowsGenericFailure()
        {
            var result = Result(VrchatDesiredStateReconciliationOutcome.Failed, false);

            DesiredStateResultPresentation presentation = DesiredStateResultPresenter.Build(result);

            Assert.That(presentation.Kind, Is.EqualTo(DesiredStateResultKind.Failed));
            Assert.That(presentation.TitleKey, Is.EqualTo(DesiredStateResultPresenter.TitleFailed));
        }

        [Test]
        public void DesiredResult_NullResult_ShowsGenericFailure()
        {
            Assert.That(DesiredStateResultPresenter.Build(null).Kind, Is.EqualTo(DesiredStateResultKind.Failed));
        }

        [Test]
        public void BeginnerMessages_AreLocalizedInJapaneseAndEnglish()
        {
            for (int i = 0; i < BeginnerMessageKeys.Length; i++)
            {
                Assert.That(FaceMotionUiText.Get(BeginnerMessageKeys[i]), Is.Not.EqualTo(BeginnerMessageKeys[i]), BeginnerMessageKeys[i]);
                Assert.That(FaceMotionUiText.Get(BeginnerMessageKeys[i], SystemLanguage.English), Is.Not.EqualTo(BeginnerMessageKeys[i]), BeginnerMessageKeys[i]);
            }
        }

        [Test]
        public void BindingConflict_ProducesBeginnerMessage_WithNameAndKeptCode()
        {
            var conflict = new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.ModularAvatarBindingConflict,
                FaceMotionDiagnosticSeverity.Error,
                "The Modular Avatar Merge Animator \"FaceMotion MA Alpha\" shares a binding through clip \"shirome.anim\".",
                "Face/Main",
                true,
                "Resolve the competing Modular Avatar binding before planning the integration again.",
                new Dictionary<string, string>
                {
                    { FaceMotionDiagnosticDetailKeys.Reason, FaceMotionDiagnosticDetailKeys.ReasonMergeAnimatorBinding },
                    { FaceMotionDiagnosticDetailKeys.ConflictObjectName, "FaceMotion MA Alpha" },
                    { FaceMotionDiagnosticDetailKeys.ConflictObjectPath, "Face/Main" },
                    { FaceMotionDiagnosticDetailKeys.ConflictClip, "shirome.anim" },
                    { FaceMotionDiagnosticDetailKeys.BindingPath, "Face/Main" },
                    { FaceMotionDiagnosticDetailKeys.BindingProperty, "blendShape.eye_back" },
                    { FaceMotionDiagnosticDetailKeys.Binding, "Face/Main / SkinnedMeshRenderer / blendShape.eye_back" }
                });

            DesiredStateResultPresentation presentation = DesiredStateResultPresenter.Build(Result(
                VrchatDesiredStateReconciliationOutcome.PreflightFailed,
                false,
                Item("yorokobi", VrchatDesiredStateAction.Add),
                conflict));

            Assert.That(presentation.HasBindingConflict, Is.True);
            Assert.That(presentation.ConflictObjectName, Is.EqualTo("Alpha"));
            Assert.That(DesiredStateResultPresenter.ConflictBeginnerKey(presentation), Is.EqualTo(DesiredStateResultPresenter.ConflictBeginnerNamed));

            string message = FaceMotionUiText.Get(DesiredStateResultPresenter.ConflictBeginnerNamed);
            Assert.That(message, Is.Not.EqualTo(DesiredStateResultPresenter.ConflictBeginnerNamed));
            Assert.That(FaceMotionUiText.Get(DesiredStateResultPresenter.ConflictBeginnerNamed, SystemLanguage.English), Does.Contain("{0}"));
            Assert.That(presentation.TechnicalDetails, Has.Count.EqualTo(1));
            Assert.That(presentation.TechnicalDetails[0], Does.Contain("[FM-H-MA-BINDING-CONFLICT]"));
            Assert.That(presentation.TechnicalDetails[0], Does.Contain("Alpha"));
        }

        [Test]
        public void BindingConflict_WithoutMergeName_FallsBackToGenericBeginnerMessage()
        {
            var conflict = new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.ModularAvatarBindingConflict,
                FaceMotionDiagnosticSeverity.Error,
                "shared binding",
                "Face/Main",
                true,
                string.Empty,
                new Dictionary<string, string>
                {
                    { FaceMotionDiagnosticDetailKeys.BindingPath, "Face/Main" },
                    { FaceMotionDiagnosticDetailKeys.BindingProperty, "blendShape.eye_back" }
                });

            DesiredStateResultPresentation presentation = DesiredStateResultPresenter.Build(Result(
                VrchatDesiredStateReconciliationOutcome.PreflightFailed,
                false,
                Item("a", VrchatDesiredStateAction.Add),
                conflict));

            Assert.That(presentation.HasBindingConflict, Is.True);
            Assert.That(presentation.ConflictObjectName, Is.Empty);
            Assert.That(DesiredStateResultPresenter.ConflictBeginnerKey(presentation), Is.EqualTo(DesiredStateResultPresenter.ConflictBeginnerGeneric));
        }

        [Test]
        public void CrossBindingConflict_IsTreatedAsBindingConflict()
        {
            var conflict = new FaceMotionDiagnostic(
                FaceMotionDiagnosticCodes.ModularAvatarCrossBindingConflict,
                FaceMotionDiagnosticSeverity.Error,
                "shares a binding with the avatar FX controller.",
                "Face/Main",
                true,
                string.Empty);

            DesiredStateResultPresentation presentation = DesiredStateResultPresenter.Build(Result(
                VrchatDesiredStateReconciliationOutcome.PreflightFailed,
                false,
                Item("a", VrchatDesiredStateAction.Add),
                conflict));

            Assert.That(presentation.HasBindingConflict, Is.True);
        }

        [Test]
        public void AdvancedFoldout_IsOwnedByTheWindow_NotNestedInIntegrationPanel()
        {
            Assert.That(FaceMotionWindow.AdvancedFoldoutKey, Is.EqualTo("FaceMotion.Window.v3.AdvancedFoldout"));
            Assert.That(typeof(FaceMotionWindow).GetField("_advancedFoldout", BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null);
            Assert.That(typeof(OneClickIntegrationPanel).GetField("_advancedFoldout", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
        }

        [Test]
        public void AdvancedSurface_ExposesManualExportAndPerAnimationAdd()
        {
            Assert.That(typeof(ExportPanel).GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
            Assert.That(typeof(OneClickIntegrationPanel).GetMethod("DrawAdvanced", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
            Assert.That(typeof(DirectVRChatIntegrationPanel).GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
        }

        [Test]
        public void AvatarIndexRebuild_RemovedFromNormalPanel_RetainedForAdvancedAndController()
        {
            Assert.That(typeof(AvatarPanel).GetMethod("RebuildIndex", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(typeof(FaceMotionWindow).GetMethod("RebuildIndex", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(typeof(AvatarController).GetMethod("RebuildIndex", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
            Assert.That(typeof(FaceMotionWindow).GetMethod("DrawTroubleshooting", BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null);
            Assert.That(FaceMotionUiText.Get("rebuildAvatarIndex"), Is.Not.EqualTo("rebuildAvatarIndex"));
        }

        [Test]
        public void NormalFlow_HasNoExplicitBatchControl()
        {
            Assert.That(typeof(OneClickIntegrationPanel).GetField("_batchController", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
            Assert.That(FaceMotionUiText.Get("batchAddToVrchat"), Is.EqualTo("batchAddToVrchat"));
        }

        [Test]
        public void ReadyHint_PointsToTheReflectSection()
        {
            Assert.That(FaceMotionUiText.Get(FaceMotionWorkflowHintService.HintPreviewAndIntegrate), Does.Contain("反映"));
        }

        [Test]
        public void StatusRefresh_UpdatePassesInvalidateCallback()
        {
            Assert.That(typeof(OneClickIntegrationPanel).GetMethod("InvalidateManagedState", BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null);
        }

        [Test]
        public void SuccessfulDesiredState_ClearsStaleOperationDiagnosticButKeepsProjectDiagnostic()
        {
            var session = new FaceMotionEditorSession();
            var persistent = Diagnostic("project-warning");
            var staleOperation = Diagnostic("stale-operation");
            session.LastProjectValidation = new ValidationReport(new[] { persistent });
            session.SetLastOperationDiagnostic(staleOperation);
            session.RecomputeDiagnostics();

            OneClickIntegrationPanel.SetDesiredStateOperationDiagnostic(session, Result(
                VrchatDesiredStateReconciliationOutcome.Succeeded,
                true));
            session.RecomputeDiagnostics();

            Assert.That(session.LastOperationDiagnostic, Is.Null);
            Assert.That(session.Diagnostics.Any(diagnostic => ReferenceEquals(diagnostic, persistent)), Is.True);
            Assert.That(session.Diagnostics.Any(diagnostic => ReferenceEquals(diagnostic, staleOperation)), Is.False);
        }

        private static IReadOnlyList<string> AnimationListPanelChecklistMember()
        {
            var members = typeof(AnimationListPanel)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.Name.Contains("Checklist") || method.Name.Contains("Vrchat"));
            return members.Select(method => method.Name).ToList();
        }

        private static VrchatDesiredStateReconciliationResult Result(
            VrchatDesiredStateReconciliationOutcome outcome,
            bool succeeded,
            params object[] itemsAndDiagnostics)
        {
            var items = new List<VrchatDesiredStateReconciliationItem>();
            var diagnostics = new List<FaceMotionDiagnostic>();
            if (itemsAndDiagnostics != null)
            {
                for (var i = 0; i < itemsAndDiagnostics.Length; i++)
                {
                    if (itemsAndDiagnostics[i] is VrchatDesiredStateReconciliationItem item) items.Add(item);
                    else if (itemsAndDiagnostics[i] is FaceMotionDiagnostic diagnostic) diagnostics.Add(diagnostic);
                }
            }

            return new VrchatDesiredStateReconciliationResult(succeeded, outcome, items, diagnostics);
        }

        private static VrchatDesiredStateReconciliationItem Item(string animationId, VrchatDesiredStateAction action)
        {
            return new VrchatDesiredStateReconciliationItem(animationId, "FaceMotion_" + animationId, action);
        }

        private static FaceMotionDiagnostic Diagnostic(string code)
        {
            return new FaceMotionDiagnostic(code, FaceMotionDiagnosticSeverity.Warning, code, string.Empty, false, string.Empty);
        }
    }
}
