using System;
using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.VRChat.Integration;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Editor.UI.Session;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    public sealed class ModularAvatarCacheAndStatusTests
    {
        private GameObject _rootA;
        private GameObject _rootB;
        private VRCAvatarDescriptor _avatarA;
        private VRCAvatarDescriptor _avatarB;
        private FaceMotionProject _project;
        private FaceMotionAnimationData _animation;

        [SetUp]
        public void SetUp()
        {
            _rootA = new GameObject("Avatar A");
            _rootB = new GameObject("Avatar B");
            _avatarA = _rootA.AddComponent<VRCAvatarDescriptor>();
            _avatarB = _rootB.AddComponent<VRCAvatarDescriptor>();
            _project = FaceMotionProject.CreateNew();
            _animation = FaceMotionAnimationData.Create("Smile");
            _project.AddAnimation(_animation);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootA);
            UnityEngine.Object.DestroyImmediate(_rootB);
        }

        [Test]
        public void Get_SameBackendAndAvatarOneHundredTimes_DiscoversOnce()
        {
            var backend = new RecordingBackend();
            var cache = new ModularAvatarManagedStateCache();

            for (var i = 0; i < 100; i++) cache.Get(backend, _avatarA);

            Assert.That(backend.InspectCalls, Is.EqualTo(1));
            Assert.That(cache.DiscoveryCount, Is.EqualTo(1));
        }

        [Test]
        public void Invalidate_CachedAvatar_DiscoversAgain()
        {
            var backend = new RecordingBackend();
            var cache = new ModularAvatarManagedStateCache();
            cache.Get(backend, _avatarA);

            cache.Invalidate(_avatarA);
            cache.Get(backend, _avatarA);

            Assert.That(backend.InspectCalls, Is.EqualTo(2));
            Assert.That(cache.DiscoveryCount, Is.EqualTo(2));
        }

        [Test]
        public void Get_AThenBThenA_DiscoversThreeTimes()
        {
            var backend = new RecordingBackend();
            var cache = new ModularAvatarManagedStateCache();

            cache.Get(backend, _avatarA);
            cache.Get(backend, _avatarB);
            cache.Get(backend, _avatarA);

            Assert.That(backend.InspectCalls, Is.EqualTo(3));
            Assert.That(cache.DiscoveryCount, Is.EqualTo(3));
        }

        [Test]
        public void Get_SwitchingBackend_DiscoversFromEachBackend()
        {
            var first = new RecordingBackend();
            var second = new RecordingBackend();
            var cache = new ModularAvatarManagedStateCache();

            cache.Get(first, _avatarA);
            cache.Get(second, _avatarA);

            Assert.That(first.InspectCalls, Is.EqualTo(1));
            Assert.That(second.InspectCalls, Is.EqualTo(1));
            Assert.That(cache.DiscoveryCount, Is.EqualTo(2));
        }

        [Test]
        public void Get_NullBackendOrAvatar_ReturnsNullWithoutDiscovery()
        {
            var backend = new RecordingBackend();
            var cache = new ModularAvatarManagedStateCache();

            Assert.That(cache.Get(null, _avatarA), Is.Null);
            Assert.That(cache.Get(backend, null), Is.Null);
            Assert.That(backend.InspectCalls, Is.Zero);
            Assert.That(cache.DiscoveryCount, Is.Zero);
        }

        [Test]
        public void ManualApply_InvalidatesCacheAfterMayMutateFailure()
        {
            var backend = new RecordingBackend();
            var cache = new ModularAvatarManagedStateCache();
            var panel = new DirectVRChatIntegrationPanel(new FaceMotionEditorSession(), cache.Invalidate);
            var plan = Plan();

            AssertCacheRediscovery(cache, backend, () => panel.ApplyModularAvatarIntegration(backend, plan));
        }

        [Test]
        public void ManualSelectedRemoval_InvalidatesCacheAfterMayMutateFailure()
        {
            var backend = new RecordingBackend();
            var cache = new ModularAvatarManagedStateCache();
            var panel = new DirectVRChatIntegrationPanel(new FaceMotionEditorSession(), cache.Invalidate);

            AssertCacheRediscovery(cache, backend, () => panel.RemoveModularAvatarIntegration(backend, _avatarA, "FaceMotion_Smile"));
        }

        [Test]
        public void ManualRemoveAll_InvalidatesCacheAfterMayMutateFailure()
        {
            var backend = new RecordingBackend();
            var cache = new ModularAvatarManagedStateCache();
            var panel = new DirectVRChatIntegrationPanel(new FaceMotionEditorSession(), cache.Invalidate);

            AssertCacheRediscovery(cache, backend, () => panel.RemoveAllModularAvatarIntegrations(backend, _avatarA));
        }

        [Test]
        public void Resolve_StableAnimationId_IsApplied()
        {
            var snapshot = Snapshot(new ModularAvatarManagedState(_animation.AnimationId, "FaceMotion_OldSmile"));

            Assert.That(Status(snapshot), Is.EqualTo(VrchatIntegrationStatus.Applied));
        }

        [Test]
        public void Resolve_RenamedStableAnimationId_IsApplied()
        {
            var snapshot = Snapshot(new ModularAvatarManagedState(_animation.AnimationId, "FaceMotion_Smile"));
            _animation.DisplayName = "Renamed Smile";

            Assert.That(Status(snapshot), Is.EqualTo(VrchatIntegrationStatus.Applied));
        }

        [Test]
        public void Resolve_LegacyCanonicalParameter_IsApplied()
        {
            var snapshot = Snapshot(new ModularAvatarManagedState(string.Empty, "FaceMotion_Smile"));

            Assert.That(Status(snapshot), Is.EqualTo(VrchatIntegrationStatus.Applied));
        }

        [Test]
        public void Resolve_RenamedLegacyParameter_IsNotApplied()
        {
            var snapshot = Snapshot(new ModularAvatarManagedState(string.Empty, "FaceMotion_Smile"));
            _animation.DisplayName = "Renamed Smile";

            Assert.That(Status(snapshot), Is.EqualTo(VrchatIntegrationStatus.NotApplied));
        }

        [Test]
        public void Resolve_UnrelatedManagedState_IsNotApplied()
        {
            var snapshot = Snapshot(new ModularAvatarManagedState("other-animation", "FaceMotion_Other"));

            Assert.That(Status(snapshot), Is.EqualTo(VrchatIntegrationStatus.NotApplied));
        }

        [Test]
        public void Resolve_IsPureAndDoesNotCallBackend()
        {
            var backend = new RecordingBackend(Snapshot(new ModularAvatarManagedState(_animation.AnimationId, "FaceMotion_Smile")));

            var result = VrchatIntegrationStatusService.Resolve(_project, _avatarA, backend.Snapshot);

            Assert.That(result[_animation.AnimationId], Is.EqualTo(VrchatIntegrationStatus.Applied));
            Assert.That(backend.InspectCalls, Is.Zero);
        }

        private VrchatIntegrationStatus Status(ModularAvatarManagedStateSnapshot snapshot)
        {
            return VrchatIntegrationStatusService.Resolve(_project, _avatarA, snapshot)[_animation.AnimationId];
        }

        private static ModularAvatarManagedStateSnapshot Snapshot(params ModularAvatarManagedState[] items)
        {
            return new ModularAvatarManagedStateSnapshot(items, Array.Empty<FaceMotionDiagnostic>());
        }

        private ModularAvatarIntegrationPlan Plan()
        {
            return new ModularAvatarIntegrationPlan(
                new ModularAvatarIntegrationRequest(_avatarA, null, "Assets", "Smile"),
                "FaceMotion_Smile",
                "FaceMotion MA Smile",
                Array.Empty<FaceMotionDiagnostic>());
        }

        private void AssertCacheRediscovery(ModularAvatarManagedStateCache cache, RecordingBackend backend, Action mutate)
        {
            cache.Get(backend, _avatarA);
            mutate();
            cache.Get(backend, _avatarA);

            Assert.That(backend.InspectCalls, Is.EqualTo(2));
            Assert.That(cache.DiscoveryCount, Is.EqualTo(2));
        }

        private sealed class RecordingBackend : IModularAvatarIntegrationBackend
        {
            public RecordingBackend(ModularAvatarManagedStateSnapshot snapshot = null)
            {
                Snapshot = snapshot ?? ModularAvatarCacheAndStatusTests.Snapshot();
            }

            public int InspectCalls { get; private set; }
            public ModularAvatarManagedStateSnapshot Snapshot { get; }
            public bool HasExistingIntegration(VRCAvatarDescriptor avatar) => false;
            public bool HasExistingIntegration(VRCAvatarDescriptor avatar, string parameter) => false;
            public ModularAvatarIntegrationPlan Plan(ModularAvatarIntegrationRequest request) => throw new NotImplementedException();
            public ModularAvatarIntegrationResult Apply(ModularAvatarIntegrationPlan plan) => FailedResult();
            public ModularAvatarIntegrationResult Remove(VRCAvatarDescriptor avatar) => FailedResult();
            public ModularAvatarIntegrationResult RemoveAnimation(VRCAvatarDescriptor avatar, string parameter) => FailedResult();
            public ModularAvatarIntegrationBatchResult RemoveAnimations(VRCAvatarDescriptor avatar, IReadOnlyList<string> parameters) => throw new NotImplementedException();
            public ModularAvatarIntegrationBatchPlan PlanBatch(IReadOnlyList<ModularAvatarIntegrationRequest> requests) => throw new NotImplementedException();
            public ModularAvatarIntegrationBatchResult ApplyBatch(ModularAvatarIntegrationBatchPlan plan) => throw new NotImplementedException();
            public ModularAvatarManagedStateSnapshot InspectManagedState(VRCAvatarDescriptor avatar) { InspectCalls++; return Snapshot; }
            public ModularAvatarManagedStateSnapshot ValidateRemovals(VRCAvatarDescriptor avatar, IReadOnlyList<string> parameters) => throw new NotImplementedException();

            private static ModularAvatarIntegrationResult FailedResult()
            {
                return new ModularAvatarIntegrationResult(false, null, Array.Empty<FaceMotionDiagnostic>());
            }
        }
    }
}
