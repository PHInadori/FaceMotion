using System;
using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.VRChat.Integration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    public sealed class VrchatDesiredStateReconciliationServiceTests
    {
        private const string Folder = "Assets/__FaceMotionTests_DesiredState";
        private GameObject _root;
        private VRCAvatarDescriptor _avatar;
        private FaceMotionProject _project;
        private FaceMotionAnimationData _animation;
        private bool _defaultOutputFolderExisted;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__FaceMotionTests_DesiredState");
            _defaultOutputFolderExisted = AssetDatabase.IsValidFolder(OneClickIntegrationService.DefaultOutputFolder);
            _root = new GameObject("Avatar");
            _avatar = _root.AddComponent<VRCAvatarDescriptor>();
            _project = FaceMotionProject.CreateNew();
            _animation = FaceMotionAnimationData.Create("Renamable");
            _project.AddAnimation(_animation);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
            AssetDatabase.DeleteAsset(Folder);
            if (!_defaultOutputFolderExisted && AssetDatabase.IsValidFolder(OneClickIntegrationService.DefaultOutputFolder)) AssetDatabase.DeleteAsset(OneClickIntegrationService.DefaultOutputFolder);
            AssetDatabase.Refresh();
        }

        [Test]
        public void Execute_EmptyOwnedAndDesiredState_IsNoOp()
        {
            var backend = new RecordingBackend();
            var request = new VrchatDesiredStateReconciliationRequest(_avatar, _project, Array.Empty<string>(), Folder, backend);

            var result = VrchatDesiredStateReconciliationService.Execute(request);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Items, Is.Empty);
            Assert.That(backend.ApplyCalls, Is.Zero);
            Assert.That(backend.RemoveCalls, Is.Zero);
        }

        [Test]
        public void Execute_EmptyOwnedStateToDesiredAnimation_ExportsAndAdds()
        {
            var backend = new RecordingBackend();

            var result = VrchatDesiredStateReconciliationService.Execute(Request(backend));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Items, Has.Exactly(1).Matches<VrchatDesiredStateReconciliationItem>(item => item.AnimationId == _animation.AnimationId && item.Action == VrchatDesiredStateAction.Add));
            Assert.That(backend.ApplyCalls, Is.EqualTo(1));
            Assert.That(backend.RemoveCalls, Is.Zero);
            Assert.That(backend.Calls, Is.EqualTo(new[] { "PlanBatch", "ValidateRemovals", "PlanFinalState", "ApplyBatch" }));
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Renamable.anim"), Is.Not.Null);
        }

        [Test]
        public void Execute_ReboundFinalPlan_PreservesSharedBindingMetadataForApply()
        {
            var backend = new RecordingBackend { IncludePartnerMetadata = true };

            var result = VrchatDesiredStateReconciliationService.Execute(Request(backend));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(backend.AppliedPlan, Is.Not.Null);
            Assert.That(backend.AppliedPlan.Items[0].PartnerParameters, Is.EqualTo(new[] { "FaceMotion_Partner" }));
            Assert.That(backend.AppliedPlan.Items[0].SharedBindings.Count, Is.EqualTo(1));
        }

        [Test]
        public void Execute_StableAnimationIdKeepsOwnedIntegrationDespiteDisplayNameChange()
        {
            var backend = new RecordingBackend(new ModularAvatarManagedState(_animation.AnimationId, "FaceMotion_OldName"));
            _animation.DisplayName = "New Name";

            var result = VrchatDesiredStateReconciliationService.Execute(Request(backend));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Items, Has.Exactly(1).Matches<VrchatDesiredStateReconciliationItem>(item => item.Action == VrchatDesiredStateAction.Keep && item.ParameterName == "FaceMotion_OldName"));
            Assert.That(backend.ApplyCalls, Is.Zero);
            Assert.That(backend.RemoveCalls, Is.Zero);
        }

        [Test]
        public void Execute_LegacyManifestUsesPlannedParameterNotDisplayName()
        {
            var backend = new RecordingBackend(new ModularAvatarManagedState(string.Empty, "FaceMotion_Renamable"));

            var result = VrchatDesiredStateReconciliationService.Execute(Request(backend));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Items[0].Action, Is.EqualTo(VrchatDesiredStateAction.Keep));
            Assert.That(backend.ApplyCalls, Is.Zero);
        }

        [Test]
        public void Execute_EmptyDesiredStateRemovesOnlyProvenOwnedState()
        {
            var backend = new RecordingBackend(new ModularAvatarManagedState("obsolete", "FaceMotion_Obsolete"));
            var request = new VrchatDesiredStateReconciliationRequest(_avatar, _project, Array.Empty<string>(), "Assets", backend);

            var result = VrchatDesiredStateReconciliationService.Execute(request);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Items, Has.Exactly(1).Matches<VrchatDesiredStateReconciliationItem>(item => item.Action == VrchatDesiredStateAction.Remove && item.ParameterName == "FaceMotion_Obsolete"));
            Assert.That(backend.RemoveCalls, Is.EqualTo(1));
        }

        [Test]
        public void Execute_ReplacedOwnedState_RemovesBeforeAdding()
        {
            var backend = new RecordingBackend(new ModularAvatarManagedState("obsolete", "FaceMotion_Obsolete"));

            var result = VrchatDesiredStateReconciliationService.Execute(Request(backend));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Items, Has.Exactly(1).Matches<VrchatDesiredStateReconciliationItem>(item => item.AnimationId == _animation.AnimationId && item.Action == VrchatDesiredStateAction.Add));
            Assert.That(result.Items, Has.Exactly(1).Matches<VrchatDesiredStateReconciliationItem>(item => item.AnimationId == "obsolete" && item.Action == VrchatDesiredStateAction.Remove));
            Assert.That(backend.Calls, Is.EqualTo(new[] { "PlanBatch", "ValidateRemovals", "PlanFinalState", "RemoveAnimations", "ApplyBatch" }));
        }

        [Test]
        public void Execute_ApplyFailureWithoutRollback_ReturnsFailed()
        {
            var backend = new RecordingBackend { FailApply = true };

            var result = VrchatDesiredStateReconciliationService.Execute(Request(backend));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(VrchatDesiredStateReconciliationOutcome.Failed));
        }

        [Test]
        public void Execute_ApplyFailureWithRollback_ReturnsFailedRolledBack()
        {
            var backend = new RollbackRecordingBackend { FailApply = true };

            var result = VrchatDesiredStateReconciliationService.Execute(Request(backend));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(VrchatDesiredStateReconciliationOutcome.FailedRolledBack));
            Assert.That(backend.CaptureCalls, Is.EqualTo(1));
            Assert.That(backend.RestoreCalls, Is.EqualTo(1));
        }

        [Test]
        public void Execute_ApplyFailureWithFailedRollback_ReturnsFailedRollbackFailed()
        {
            var backend = new RollbackRecordingBackend { FailApply = true, RestoreSucceeds = false };

            var result = VrchatDesiredStateReconciliationService.Execute(Request(backend));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(VrchatDesiredStateReconciliationOutcome.FailedRollbackFailed));
            Assert.That(backend.RestoreCalls, Is.EqualTo(1));
        }

        [Test]
        public void Execute_BlockingCanonicalPlanDoesNotApplyOrRemove()
        {
            var backend = new RecordingBackend(new ModularAvatarManagedState("obsolete", "FaceMotion_Obsolete")) { BlockPlans = true };

            var result = VrchatDesiredStateReconciliationService.Execute(Request(backend));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(VrchatDesiredStateReconciliationOutcome.PreflightFailed));
            Assert.That(backend.ApplyCalls, Is.Zero);
            Assert.That(backend.RemoveCalls, Is.Zero);
        }

        [Test]
        public void Execute_CanonicalOnlyBlockingDiagnostic_IsReturnedWithoutGenericPreflightWrapper()
        {
            var backend = new RecordingBackend { BlockCanonicalOnly = true };

            var result = VrchatDesiredStateReconciliationService.Execute(Request(backend));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(VrchatDesiredStateReconciliationOutcome.PreflightFailed));
            Assert.That(result.Diagnostics, Has.Some.Matches<FaceMotionDiagnostic>(d => d.Code == "test" && d.Message == "canonical blocked"));
            Assert.That(result.Diagnostics, Has.None.Matches<FaceMotionDiagnostic>(d => d.Code == FaceMotionDiagnosticCodes.BatchPreflightFailed));
            Assert.That(backend.ApplyCalls, Is.Zero);
            Assert.That(backend.RemoveCalls, Is.Zero);
        }

        [Test]
        public void Execute_RemovalInvalidatesManagedStateCacheCallback()
        {
            var backend = new RecordingBackend(new ModularAvatarManagedState("obsolete", "FaceMotion_Obsolete"));
            var invalidations = 0;
            var request = new VrchatDesiredStateReconciliationRequest(_avatar, _project, Array.Empty<string>(), "Assets", backend, _ => invalidations++);

            VrchatDesiredStateReconciliationService.Execute(request);

            Assert.That(invalidations, Is.EqualTo(1));
        }

        [Test]
        public void Execute_EmptyOutputFolder_ForwardsFaceMotionDefaultToEveryMaPlan()
        {
            var backend = new RecordingBackend();

            var result = VrchatDesiredStateReconciliationService.Execute(new VrchatDesiredStateReconciliationRequest(
                _avatar, _project, new[] { _animation.AnimationId }, null, backend));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(backend.OutputFolders, Is.Not.Empty);
            Assert.That(backend.OutputFolders, Is.All.EqualTo(OneClickIntegrationService.DefaultOutputFolder));
            Assert.That(result.Diagnostics, Has.None.Matches<FaceMotionDiagnostic>(d => d.Code == FaceMotionDiagnosticCodes.ModularAvatarPath));
        }

        [Test]
        public void Execute_RemoveThenReaddWithDefaultOutputFolder_SucceedsWithoutPathDiagnostic()
        {
            var backend = new RecordingBackend(new ModularAvatarManagedState(_animation.AnimationId, "FaceMotion_Renamable"));

            var removed = VrchatDesiredStateReconciliationService.Execute(new VrchatDesiredStateReconciliationRequest(
                _avatar, _project, Array.Empty<string>(), null, backend));
            var readded = VrchatDesiredStateReconciliationService.Execute(new VrchatDesiredStateReconciliationRequest(
                _avatar, _project, new[] { _animation.AnimationId }, null, backend));

            Assert.That(removed.Succeeded, Is.True);
            Assert.That(readded.Succeeded, Is.True);
            Assert.That(readded.Items, Has.Exactly(1).Matches<VrchatDesiredStateReconciliationItem>(item => item.Action == VrchatDesiredStateAction.Add));
            Assert.That(readded.Diagnostics, Has.None.Matches<FaceMotionDiagnostic>(d => d.Code == FaceMotionDiagnosticCodes.ModularAvatarPath));
            Assert.That(backend.OutputFolders, Is.All.EqualTo(OneClickIntegrationService.DefaultOutputFolder));
        }

        [Test]
        public void Execute_TwoAnimations_ForwardsDefaultOutputFolderToBoth()
        {
            var second = FaceMotionAnimationData.Create("Second");
            _project.AddAnimation(second);
            var backend = new RecordingBackend();

            var result = VrchatDesiredStateReconciliationService.Execute(new VrchatDesiredStateReconciliationRequest(
                _avatar, _project, new[] { _animation.AnimationId, second.AnimationId }, null, backend));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(backend.OutputFolders, Has.Count.EqualTo(4));
            Assert.That(backend.OutputFolders, Is.All.EqualTo(OneClickIntegrationService.DefaultOutputFolder));
        }

        [Test]
        public void Execute_ExplicitCustomOutputFolder_WinsOverDefault()
        {
            var backend = new RecordingBackend();

            var result = VrchatDesiredStateReconciliationService.Execute(new VrchatDesiredStateReconciliationRequest(
                _avatar, _project, new[] { _animation.AnimationId }, Folder, backend));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(backend.OutputFolders, Is.All.EqualTo(Folder));
        }

        [Test]
        public void Execute_ExplicitInvalidOutputFolder_IsForwardedWithoutFallback()
        {
            const string invalid = "Assets/../Assets";
            var backend = new RecordingBackend();

            var result = VrchatDesiredStateReconciliationService.Execute(new VrchatDesiredStateReconciliationRequest(
                _avatar, _project, new[] { _animation.AnimationId }, invalid, backend));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(backend.OutputFolders, Is.Empty, "Validation must not replace an explicit invalid path with the beginner default.");
        }

        [Test]
        public void Execute_DefaultRootAbsent_CanonicalPlanFailureIsWriteFreePreflight()
        {
            EnsureDefaultRootAbsent();
            var backend = new RecordingBackend { BlockPlans = true };

            var result = VrchatDesiredStateReconciliationService.Execute(new VrchatDesiredStateReconciliationRequest(
                _avatar, _project, new[] { _animation.AnimationId }, null, backend));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(VrchatDesiredStateReconciliationOutcome.PreflightFailed));
            Assert.That(AssetDatabase.IsValidFolder(OneClickIntegrationService.DefaultOutputFolder), Is.False,
                "Preflight must never create the default output root, even to resolve its own path check.");
            Assert.That(backend.ApplyCalls, Is.Zero);
            Assert.That(backend.RemoveCalls, Is.Zero);
        }

        [Test]
        public void Execute_DefaultRootAbsent_FinalStateValidationFailureIsWriteFreePreflight()
        {
            EnsureDefaultRootAbsent();
            var backend = new RecordingBackend { BlockFinalPlans = true };

            var result = VrchatDesiredStateReconciliationService.Execute(new VrchatDesiredStateReconciliationRequest(
                _avatar, _project, new[] { _animation.AnimationId }, null, backend));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(VrchatDesiredStateReconciliationOutcome.PreflightFailed));
            Assert.That(AssetDatabase.IsValidFolder(OneClickIntegrationService.DefaultOutputFolder), Is.False,
                "A later final-state validation failure must leave the project untouched.");
            Assert.That(backend.ApplyCalls, Is.Zero);
            Assert.That(backend.RemoveCalls, Is.Zero);
        }

        [Test]
        public void Execute_DefaultRootAbsent_SuccessfulAddCreatesRootDuringMutation()
        {
            EnsureDefaultRootAbsent();
            var backend = new RecordingBackend();

            var result = VrchatDesiredStateReconciliationService.Execute(new VrchatDesiredStateReconciliationRequest(
                _avatar, _project, new[] { _animation.AnimationId }, null, backend));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(AssetDatabase.IsValidFolder(OneClickIntegrationService.DefaultOutputFolder), Is.True,
                "The canonical default root is created inside the mutation stage.");
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(OneClickIntegrationService.DefaultOutputFolder + "/Renamable.anim"), Is.Not.Null);
            Assert.That(backend.OutputFolders, Is.Not.Empty);
            Assert.That(backend.OutputFolders, Is.All.EqualTo(OneClickIntegrationService.DefaultOutputFolder));
            Assert.That(result.Diagnostics, Has.None.Matches<FaceMotionDiagnostic>(d => d.Code == FaceMotionDiagnosticCodes.ModularAvatarPath));
        }

        [Test]
        public void Execute_DefaultRootAbsent_ZeroDesiredCreatesNothing()
        {
            EnsureDefaultRootAbsent();
            var backend = new RecordingBackend(new ModularAvatarManagedState("obsolete", "FaceMotion_Obsolete"));
            var request = new VrchatDesiredStateReconciliationRequest(_avatar, _project, Array.Empty<string>(), null, backend);

            var result = VrchatDesiredStateReconciliationService.Execute(request);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(AssetDatabase.IsValidFolder(OneClickIntegrationService.DefaultOutputFolder), Is.False,
                "Removal-only reconciliation must not create FaceMotion's generated root.");
            Assert.That(backend.RemoveCalls, Is.EqualTo(1));
        }

        [Test]
        public void Execute_ApplyFailureAfterRootCreation_RemovesCreatedRoot()
        {
            EnsureDefaultRootAbsent();
            var backend = new RecordingBackend { FailApply = true };

            var result = VrchatDesiredStateReconciliationService.Execute(new VrchatDesiredStateReconciliationRequest(
                _avatar, _project, new[] { _animation.AnimationId }, null, backend));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(AssetDatabase.IsValidFolder(OneClickIntegrationService.DefaultOutputFolder), Is.False,
                "Folders this transaction created are removed when mutation fails.");
        }

        private void EnsureDefaultRootAbsent()
        {
            if (AssetDatabase.IsValidFolder(OneClickIntegrationService.DefaultOutputFolder))
                AssetDatabase.DeleteAsset(OneClickIntegrationService.DefaultOutputFolder);
            AssetDatabase.Refresh();
        }

        private VrchatDesiredStateReconciliationRequest Request(RecordingBackend backend) => new VrchatDesiredStateReconciliationRequest(_avatar, _project, new[] { _animation.AnimationId }, Folder, backend);

        private class RecordingBackend : IModularAvatarIntegrationBackend, IModularAvatarDesiredStateBackend
        {
            private readonly List<ModularAvatarManagedState> _items;
            public int ApplyCalls;
            public int RemoveCalls;
            public bool BlockPlans;
            public bool BlockCanonicalOnly;
            public bool BlockFinalPlans;
            public bool FailApply;
            public bool IncludePartnerMetadata;
            public ModularAvatarIntegrationBatchPlan AppliedPlan;
            public readonly List<string> Calls = new List<string>();
            public readonly List<string> OutputFolders = new List<string>();
            public RecordingBackend(params ModularAvatarManagedState[] items) { _items = new List<ModularAvatarManagedState>(items); }
            public bool HasExistingIntegration(VRCAvatarDescriptor avatar) => _items.Count > 0;
            public bool HasExistingIntegration(VRCAvatarDescriptor avatar, string parameter) => false;
            public ModularAvatarIntegrationPlan Plan(ModularAvatarIntegrationRequest request) => throw new NotImplementedException();
            public ModularAvatarIntegrationResult Apply(ModularAvatarIntegrationPlan plan) => throw new NotImplementedException();
            public ModularAvatarIntegrationResult Remove(VRCAvatarDescriptor avatar) => throw new NotImplementedException();
            public ModularAvatarIntegrationResult RemoveAnimation(VRCAvatarDescriptor avatar, string parameter) => throw new NotImplementedException();
            public ModularAvatarIntegrationBatchResult RemoveAnimations(VRCAvatarDescriptor avatar, IReadOnlyList<string> parameters) { Calls.Add("RemoveAnimations"); RemoveCalls++; _items.Clear(); return new ModularAvatarIntegrationBatchResult(true, Array.Empty<ModularAvatarIntegrationResult>(), Array.Empty<FaceMotionDiagnostic>()); }
            public ModularAvatarIntegrationBatchPlan PlanBatch(IReadOnlyList<ModularAvatarIntegrationRequest> requests)
            {
                Calls.Add("PlanBatch");
                CaptureOutputFolders(requests);
                var plans = new List<ModularAvatarIntegrationPlan>();
                for (var i = 0; i < requests.Count; i++) plans.Add(CreatePlan(requests[i], BlockPlans || BlockCanonicalOnly, BlockCanonicalOnly ? "canonical blocked" : "blocked"));
                return new ModularAvatarIntegrationBatchPlan(plans);
            }
            public ModularAvatarIntegrationBatchPlan PlanFinalState(IReadOnlyList<ModularAvatarIntegrationRequest> requests, IReadOnlyList<string> removingParameters)
            {
                Calls.Add("PlanFinalState");
                CaptureOutputFolders(requests);
                return Plans(requests);
            }
            public virtual ModularAvatarIntegrationBatchResult ApplyBatch(ModularAvatarIntegrationBatchPlan plan) { Calls.Add("ApplyBatch"); ApplyCalls++; AppliedPlan = plan; return new ModularAvatarIntegrationBatchResult(!FailApply, Array.Empty<ModularAvatarIntegrationResult>(), Array.Empty<FaceMotionDiagnostic>()); }
            public ModularAvatarManagedStateSnapshot InspectManagedState(VRCAvatarDescriptor avatar) => new ModularAvatarManagedStateSnapshot(_items, Array.Empty<FaceMotionDiagnostic>());
            public ModularAvatarManagedStateSnapshot ValidateRemovals(VRCAvatarDescriptor avatar, IReadOnlyList<string> parameters) { Calls.Add("ValidateRemovals"); return new ModularAvatarManagedStateSnapshot(_items, Array.Empty<FaceMotionDiagnostic>()); }
            private ModularAvatarIntegrationBatchPlan Plans(IReadOnlyList<ModularAvatarIntegrationRequest> requests)
            {
                var plans = new List<ModularAvatarIntegrationPlan>();
                for (var i = 0; i < requests.Count; i++) plans.Add(CreatePlan(requests[i], BlockPlans || BlockFinalPlans, "final state blocked"));
                return new ModularAvatarIntegrationBatchPlan(plans);
            }
            private ModularAvatarIntegrationPlan CreatePlan(ModularAvatarIntegrationRequest request, bool blocking, string message)
            {
                IReadOnlyList<string> partners = IncludePartnerMetadata ? new[] { "FaceMotion_Partner" } : null;
                IReadOnlyList<EditorCurveBinding> shared = IncludePartnerMetadata
                    ? new[] { EditorCurveBinding.FloatCurve("Body", typeof(Transform), "m_LocalPosition.x") }
                    : null;
                return new ModularAvatarIntegrationPlan(
                    request,
                    "FaceMotion_" + request.DisplayName.Replace(" ", "_"),
                    "FaceMotion MA",
                    "FaceMotionMA",
                    blocking ? new[] { new FaceMotionDiagnostic("test", FaceMotionDiagnosticSeverity.Error, message, string.Empty, true, string.Empty) } : Array.Empty<FaceMotionDiagnostic>(),
                    partners,
                    shared);
            }
            private void CaptureOutputFolders(IReadOnlyList<ModularAvatarIntegrationRequest> requests) { for (var i = 0; i < requests.Count; i++) OutputFolders.Add(requests[i].OutputFolder); }
        }

        private sealed class RollbackRecordingBackend : RecordingBackend, IModularAvatarIntegrationRollbackBackend
        {
            public int CaptureCalls;
            public int RestoreCalls;
            public bool RestoreSucceeds = true;
            public object CaptureRollbackSnapshot(VRCAvatarDescriptor avatar) { CaptureCalls++; return new object(); }
            public ModularAvatarIntegrationBatchResult RestoreRollbackSnapshot(object snapshot) { RestoreCalls++; return new ModularAvatarIntegrationBatchResult(RestoreSucceeds, Array.Empty<ModularAvatarIntegrationResult>(), Array.Empty<FaceMotionDiagnostic>()); }
        }
    }
}
