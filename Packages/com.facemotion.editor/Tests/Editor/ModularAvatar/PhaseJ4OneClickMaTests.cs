using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.ModularAvatar;
using FaceMotion.Editor.VRChat.Integration;
using FaceMotion.Integration;
using nadena.dev.modular_avatar.core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// J4 one-click "VRChatへ追加" contract tests that run only when Modular Avatar is
    /// installed. They verify the Modular Avatar path of the one-click flow, reapply
    /// idempotency, manifest-conflict blocking, and the cross-backend warnings.
    /// </summary>
    public sealed class PhaseJ4OneClickMaTests
    {
        private const string Folder = "Assets/__FaceMotionTests_J4MA";
        private const string Out = Folder + "/Out";
        private GameObject _root;
        private VRCAvatarDescriptor _avatar;
        private AnimatorController _fx;
        private FaceMotionAnimationData _animation;
        private FaceMotionProject _project;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__FaceMotionTests_J4MA");
            _root = new GameObject("Avatar"); _avatar = _root.AddComponent<VRCAvatarDescriptor>();
            _fx = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/original.controller");
            _avatar.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, isDefault = false, animatorController = _fx } };
            _animation = FaceMotionAnimationData.Create("Smile");
            _project = FaceMotionProject.CreateNew(); _project.AddAnimation(_animation);
            IntegrationBackendSelectionStore.Save(IntegrationBackendSelection.ModularAvatar);
        }

        [TearDown]
        public void TearDown()
        {
            IntegrationBackendSelectionStore.Save(IntegrationBackendSelection.Direct);
            if (_animation != null) ExportedClipRegistry.Remove(_animation.AnimationId);
            Object.DestroyImmediate(_root); AssetDatabase.DeleteAsset(Folder); AssetDatabase.Refresh();
        }

        private OneClickIntegrationRequest Request(FaceMotionAnimationData animation = null)
        {
            return new OneClickIntegrationRequest(_avatar, animation ?? _animation, _project, Out);
        }

        private static string PathFor(string displayName) { return Out + "/" + displayName + ".anim"; }

        private static bool Contains(IReadOnlyList<FaceMotionDiagnostic> items, string code)
        {
            for (int i = 0; i < items.Count; i++) if (items[i].Code == code) return true;
            return false;
        }

        private OneClickIntegrationResult ApplyOneClick(FaceMotionAnimationData animation)
        {
            ExportedClipRegistry.Record(animation.AnimationId, PathFor(animation.DisplayName));
            return OneClickIntegrationService.Execute(Request(animation));
        }

        [Test]
        public void Execute_MaHappyPath_ExportsAppliesAndReportsMaBackend()
        {
            var result = ApplyOneClick(_animation);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Stage, Is.EqualTo(OneClickStage.Done));
            Assert.That(result.Backend, Is.EqualTo(OneClickIntegrationService.ModularAvatarBackendId));
            Assert.That(result.ParameterName, Is.EqualTo("FaceMotion_Smile"));
            Assert.That(result.ExportPath, Is.EqualTo(PathFor("Smile")));
            Assert.That(result.Clip, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(PathFor("Smile")), Is.SameAs(result.Clip));
            Assert.That(result.Manifest, Is.TypeOf<ModularAvatarIntegrationManifest>());
            Assert.That(_root.transform.Find("FaceMotion MA Smile"), Is.Not.Null);
            var controller = _root.transform.Find("FaceMotion MA Smile").GetComponent<ModularAvatarMergeAnimator>().animator as AnimatorController;
            Assert.That(controller.parameters, Has.Some.Matches<AnimatorControllerParameter>(p => p.name == "FaceMotion_Smile" && p.type == AnimatorControllerParameterType.Bool));
            Assert.That(AssetDatabase.IsValidFolder(Out + "/FaceMotionMA_Smile"), Is.True);
            Assert.That(result.Diagnostics, Has.Some.Matches<FaceMotionDiagnostic>(d => d.Code == FaceMotionDiagnosticCodes.OneClickSucceeded));
        }

        [Test]
        public void Execute_MaSecondRun_ReappliesWithoutDuplicatingTheManagedRoot()
        {
            var first = ApplyOneClick(_animation);
            var second = ApplyOneClick(_animation);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(second.Succeeded, Is.True, second.Diagnostics[second.Diagnostics.Count - 1].Message);
            Assert.That(Contains(first.Diagnostics, FaceMotionDiagnosticCodes.OneClickReapplied), Is.False);
            Assert.That(Contains(second.Diagnostics, FaceMotionDiagnosticCodes.OneClickReapplied), Is.True);
            Assert.That(_root.GetComponentsInChildren<ModularAvatarMergeAnimator>(true), Has.Length.EqualTo(1));
            Assert.That(_root.transform.Find("FaceMotion MA Smile"), Is.Not.Null);
            Assert.That(AssetDatabase.IsValidFolder(Out + "/FaceMotionMA_Smile"), Is.True);
            Assert.That(AssetDatabase.IsValidFolder(Out + "/FaceMotionMA_Smile 1"), Is.False);
        }

        [Test]
        public void Execute_MaManifestConflict_StopsBeforeApplyAndKeepsExistingIntegration()
        {
            Assert.That(ApplyOneClick(_animation).Succeeded, Is.True);

            var frown = FaceMotionAnimationData.Create("Frown");
            var result = ApplyOneClick(frown);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Stage, Is.EqualTo(OneClickStage.Validate));
            Assert.That(result.Manifest, Is.Null);
            Assert.That(Contains(result.Diagnostics, "FM-H-MA-MANIFEST-CONFLICT"), Is.True);
            Assert.That(_root.transform.Find("FaceMotion MA Frown"), Is.Null);
            Assert.That(_root.transform.Find("FaceMotion MA Smile"), Is.Not.Null);
            ExportedClipRegistry.Remove(frown.AnimationId);
        }

        [Test]
        public void Execute_MaWhenDirectIntegrationExists_EmitsCrossBackendWarningAndApplies()
        {
            if (!AssetDatabase.IsValidFolder(Folder + "/Dir")) AssetDatabase.CreateFolder(Folder, "Dir");
            var directClip = new AnimationClip();
            AssetDatabase.CreateAsset(directClip, Folder + "/direct.anim");
            var directApply = DirectVRChatIntegration.Apply(DirectVRChatIntegration.Plan(new DirectIntegrationRequest(_avatar, directClip, Folder + "/Dir", "Wave")));
            Assert.That(directApply.Succeeded, Is.True, directApply.Diagnostics.Count == 0 ? "plan" : directApply.Diagnostics[directApply.Diagnostics.Count - 1].Message);

            var wave = FaceMotionAnimationData.Create("Wave");
            var result = ApplyOneClick(wave);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(Contains(result.Diagnostics, FaceMotionDiagnosticCodes.OneClickCrossBackend), Is.True);
            Assert.That(result.Backend, Is.EqualTo(OneClickIntegrationService.ModularAvatarBackendId));
            ExportedClipRegistry.Remove(wave.AnimationId);
        }

        [Test]
        public void Execute_DirectWhenMaIntegrationExists_EmitsCrossBackendWarningAndApplies()
        {
            Assert.That(ApplyOneClick(_animation).Succeeded, Is.True);

            IntegrationBackendSelectionStore.Save(IntegrationBackendSelection.Direct);
            var frown = FaceMotionAnimationData.Create("Frown");
            ExportedClipRegistry.Record(frown.AnimationId, PathFor("Frown"));
            var result = OneClickIntegrationService.Execute(Request(frown));

            Assert.That(result.Succeeded, Is.True, result.Diagnostics[result.Diagnostics.Count - 1].Message);
            Assert.That(Contains(result.Diagnostics, FaceMotionDiagnosticCodes.OneClickCrossBackend), Is.True);
            Assert.That(result.Backend, Is.EqualTo(DirectVRChatIntegration.BackendId));
            Assert.That(AssetDatabase.LoadAssetAtPath<DirectIntegrationManifest>(Out + "/FaceMotion_Frown/Manifest.asset"), Is.Not.Null);
            ExportedClipRegistry.Remove(frown.AnimationId);
        }
    }
}