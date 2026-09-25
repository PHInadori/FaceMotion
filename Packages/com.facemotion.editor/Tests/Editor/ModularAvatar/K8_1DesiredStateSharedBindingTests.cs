using System;
using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.ModularAvatar;
using FaceMotion.Editor.VRChat.Integration;
using FaceMotion.Timeline;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// Exercises the normal desired-state pipeline with the same shape as the fresh-project
    /// reproduction: two constant BlendShape tracks on Yumeka_Body/Breasts_Width_1.
    /// </summary>
    public sealed class K8_1DesiredStateSharedBindingTests
    {
        private const string Folder = "Assets/__FaceMotionTests_DesiredShared";
        private const string RendererPath = "Yumeka_Body";
        private const string Shape = "Breasts_Width_1";
        private const string ParamA = "FaceMotion_SharedA";
        private const string ParamB = "FaceMotion_SharedB";
        private GameObject _root;
        private VRCAvatarDescriptor _avatar;
        private Mesh _mesh;
        private FaceMotionProject _project;
        private FaceMotionAnimationData _animationA;
        private FaceMotionAnimationData _animationB;

        [SetUp]
        public void SetUp()
        {
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.CreateFolder("Assets", "__FaceMotionTests_DesiredShared");
            _root = new GameObject("Avatar");
            _avatar = _root.AddComponent<VRCAvatarDescriptor>();
            var body = new GameObject(RendererPath);
            body.transform.SetParent(_root.transform, false);
            _mesh = CreateMesh(Shape);
            body.AddComponent<SkinnedMeshRenderer>().sharedMesh = _mesh;

            _project = FaceMotionProject.CreateNew();
            _animationA = CreateAnimation("SharedA", 100f);
            _animationB = CreateAnimation("SharedB", 50f);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
            UnityEngine.Object.DestroyImmediate(_mesh);
            AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.Refresh();
        }

        [Test]
        public void Execute_RealDesiredState_SharedBindingPlansAppliesRemovesAndReadds()
        {
            var first = new TraceBackend();
            var added = Execute(first, _animationA.AnimationId, _animationB.AnimationId);

            AssertSucceededShared(added, first, 2);
            AssertPartnerController("SharedA", ParamA, ParamB);
            AssertPartnerController("SharedB", ParamB, ParamA);

            var removed = Execute(new TraceBackend(), _animationA.AnimationId);
            Assert.That(removed.Succeeded, Is.True, Describe(removed));
            AssertNoBlockingSharedDiagnostics(removed.Diagnostics);
            AssertSingleParameterController("SharedA", ParamA);
            Assert.That(_root.transform.Find("FaceMotion MA SharedB"), Is.Null);

            var readdedTrace = new TraceBackend();
            var readded = Execute(readdedTrace, _animationA.AnimationId, _animationB.AnimationId);
            AssertSucceededShared(readded, readdedTrace, 1);
            AssertPartnerController("SharedA", ParamA, ParamB);
            AssertPartnerController("SharedB", ParamB, ParamA);
        }

        [Test]
        public void Execute_CanonicalCurrentStateWarning_DoesNotBlockReplacementAfterOwnedRemoval()
        {
            var old = CreateAnimation("Replacement", 25f);
            var replacement = CreateAnimation("Replacement", 75f);
            Assert.That(Execute(new TraceBackend(), old.AnimationId).Succeeded, Is.True);

            var trace = new TraceBackend();
            var result = Execute(trace, replacement.AnimationId);

            Assert.That(result.Succeeded, Is.True, Describe(result));
            Assert.That(trace.CanonicalPlans, Has.Count.EqualTo(1));
            Assert.That(trace.FinalPlans, Has.Count.EqualTo(1));
            Assert.That(trace.CanonicalPlans[0].Items[0].ParameterName, Is.EqualTo("FaceMotion_Replacement"),
                "canonical planning may reuse a proven-owned parameter without treating it as foreign");
            Assert.That(trace.FinalPlans[0].Items[0].ParameterName, Is.EqualTo("FaceMotion_Replacement"),
                "final-state planning reuses the proven-owned root scheduled for removal");
            Assert.That(trace.CanonicalPlans[0].Items[0].IsValid, Is.True);
            Assert.That(trace.FinalPlans[0].Items[0].IsValid, Is.True);
            Assert.That(result.Items, Has.Exactly(1).Matches<VrchatDesiredStateReconciliationItem>(item => item.AnimationId == old.AnimationId && item.Action == VrchatDesiredStateAction.Remove));
            Assert.That(result.Items, Has.Exactly(1).Matches<VrchatDesiredStateReconciliationItem>(item => item.AnimationId == replacement.AnimationId && item.Action == VrchatDesiredStateAction.Add));
            AssertNoBlockingSharedDiagnostics(result.Diagnostics);
        }

        private VrchatDesiredStateReconciliationResult Execute(TraceBackend backend, params string[] ids)
        {
            return VrchatDesiredStateReconciliationService.Execute(
                new VrchatDesiredStateReconciliationRequest(_avatar, _project, ids, Folder, backend));
        }

        private void AssertSucceededShared(VrchatDesiredStateReconciliationResult result, TraceBackend trace, int expectedApplyItems)
        {
            Assert.That(result.Succeeded, Is.True, Describe(result));
            AssertNoBlockingSharedDiagnostics(result.Diagnostics);
            Assert.That(trace.CanonicalPlans, Has.Count.EqualTo(1));
            Assert.That(trace.FinalPlans, Has.Count.EqualTo(1));
            Assert.That(trace.AppliedPlan, Is.Not.Null);
            Assert.That(trace.AppliedPlan.Items.Count, Is.EqualTo(expectedApplyItems));
            AssertSharedPlan(trace.CanonicalPlans[0]);
            AssertSharedPlan(trace.FinalPlans[0]);
            AssertSharedPlan(trace.AppliedPlan);
        }

        private static void AssertSharedPlan(ModularAvatarIntegrationBatchPlan plan)
        {
            Assert.That(plan.IsValid, Is.True, Describe(plan));
            for (int i = 0; i < plan.Items.Count; i++)
            {
                Assert.That(plan.Items[i].PartnerParameters.Count, Is.EqualTo(1));
                Assert.That(plan.Items[i].SharedBindings.Count, Is.EqualTo(1));
                Assert.That(Find(plan.Items[i].Diagnostics, FaceMotionDiagnosticCodes.ModularAvatarCrossBindingConflict), Is.Null);
            }
        }

        private static void AssertNoBlockingSharedDiagnostics(IReadOnlyList<FaceMotionDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                var diagnostic = diagnostics[i];
                Assert.That(diagnostic.Code, Is.Not.EqualTo(FaceMotionDiagnosticCodes.ModularAvatarCrossBindingConflict));
                Assert.That(diagnostic.Code, Is.Not.EqualTo(FaceMotionDiagnosticCodes.BatchPreflightFailed));
                if (diagnostic.Code == FaceMotionDiagnosticCodes.ModularAvatarSharedBindingWarning)
                {
                    Assert.That(diagnostic.Severity, Is.EqualTo(FaceMotionDiagnosticSeverity.Warning));
                    Assert.That(diagnostic.Blocking, Is.False);
                }
            }
        }

        private void AssertPartnerController(string name, string own, string partner)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/FaceMotionMA_" + name + "/FX.controller");
            Assert.That(controller, Is.Not.Null);
            var parameterNames = new List<string>();
            for (int i = 0; i < controller.parameters.Length; i++) parameterNames.Add(controller.parameters[i].name);
            Assert.That(parameterNames, Is.EquivalentTo(new[] { own, partner }));
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/FaceMotionMA_" + name + "/ResetUnique.anim"), Is.Not.Null);
        }

        private void AssertSingleParameterController(string name, string parameter)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/FaceMotionMA_" + name + "/FX.controller");
            Assert.That(controller.parameters, Has.Length.EqualTo(1));
            Assert.That(controller.parameters[0].name, Is.EqualTo(parameter));
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/FaceMotionMA_" + name + "/ResetUnique.anim"), Is.Null);
        }

        private FaceMotionAnimationData CreateAnimation(string name, float value)
        {
            var animation = FaceMotionAnimationData.Create(name);
            animation.Timeline.Duration = 1f;
            animation.Timeline.FrameRate = 60f;
            animation.Timeline.Loop = false;
            var track = FaceTrackData.CreateBlendShape(RendererPath, Shape);
            track.BlendShape.AddKey(FloatKeyframeData.Create(0f, value, InterpolationType.Linear));
            track.BlendShape.AddKey(FloatKeyframeData.Create(1f, value, InterpolationType.Linear));
            animation.Timeline.AddTrack(track);
            _project.AddAnimation(animation);
            return animation;
        }

        private static Mesh CreateMesh(string blendShape)
        {
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-1f, -0.5f, 0f), new Vector3(1f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f) };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateNormals();
            mesh.AddBlendShapeFrame(blendShape, 0f, new Vector3[3], null, null);
            return mesh;
        }

        private static FaceMotionDiagnostic Find(IReadOnlyList<FaceMotionDiagnostic> diagnostics, string code)
        {
            for (int i = 0; i < diagnostics.Count; i++) if (diagnostics[i].Code == code) return diagnostics[i];
            return null;
        }

        private static string Describe(VrchatDesiredStateReconciliationResult result)
        {
            var messages = new List<string>();
            for (int i = 0; i < result.Diagnostics.Count; i++) messages.Add(result.Diagnostics[i].Code + ": " + result.Diagnostics[i].Message);
            return string.Join(" | ", messages);
        }

        private static string Describe(ModularAvatarIntegrationBatchPlan plan)
        {
            var messages = new List<string>();
            for (int i = 0; i < plan.Items.Count; i++)
                for (int d = 0; d < plan.Items[i].Diagnostics.Count; d++) messages.Add(plan.Items[i].Diagnostics[d].Code + ": " + plan.Items[i].Diagnostics[d].Message);
            return string.Join(" | ", messages);
        }

        private sealed class TraceBackend : IModularAvatarIntegrationBackend, IModularAvatarDesiredStateBackend
        {
            private readonly ModularAvatarIntegrationBackend _inner = new ModularAvatarIntegrationBackend();
            public readonly List<ModularAvatarIntegrationBatchPlan> CanonicalPlans = new List<ModularAvatarIntegrationBatchPlan>();
            public readonly List<ModularAvatarIntegrationBatchPlan> FinalPlans = new List<ModularAvatarIntegrationBatchPlan>();
            public ModularAvatarIntegrationBatchPlan AppliedPlan;
            public bool HasExistingIntegration(VRCAvatarDescriptor avatar) => _inner.HasExistingIntegration(avatar);
            public bool HasExistingIntegration(VRCAvatarDescriptor avatar, string parameter) => _inner.HasExistingIntegration(avatar, parameter);
            public ModularAvatarIntegrationPlan Plan(ModularAvatarIntegrationRequest request) => _inner.Plan(request);
            public ModularAvatarIntegrationResult Apply(ModularAvatarIntegrationPlan plan) => _inner.Apply(plan);
            public ModularAvatarIntegrationResult Remove(VRCAvatarDescriptor avatar) => _inner.Remove(avatar);
            public ModularAvatarIntegrationResult RemoveAnimation(VRCAvatarDescriptor avatar, string parameter) => _inner.RemoveAnimation(avatar, parameter);
            public ModularAvatarIntegrationBatchResult RemoveAnimations(VRCAvatarDescriptor avatar, IReadOnlyList<string> parameters) => _inner.RemoveAnimations(avatar, parameters);
            public ModularAvatarIntegrationBatchPlan PlanBatch(IReadOnlyList<ModularAvatarIntegrationRequest> requests) { var plan = _inner.PlanBatch(requests); CanonicalPlans.Add(plan); return plan; }
            public ModularAvatarIntegrationBatchPlan PlanFinalState(IReadOnlyList<ModularAvatarIntegrationRequest> requests, IReadOnlyList<string> removingParameters) { var plan = _inner.PlanFinalState(requests, removingParameters); FinalPlans.Add(plan); return plan; }
            public ModularAvatarIntegrationBatchResult ApplyBatch(ModularAvatarIntegrationBatchPlan plan) { AppliedPlan = plan; return _inner.ApplyBatch(plan); }
            public ModularAvatarManagedStateSnapshot InspectManagedState(VRCAvatarDescriptor avatar) => _inner.InspectManagedState(avatar);
            public ModularAvatarManagedStateSnapshot ValidateRemovals(VRCAvatarDescriptor avatar, IReadOnlyList<string> parameters) => _inner.ValidateRemovals(avatar, parameters);
        }
    }
}
