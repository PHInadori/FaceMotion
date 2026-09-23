using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.VRChat.Integration;
using FaceMotion.Editor.UI.Diagnostics;
using FaceMotion.Editor.UI.Localization;
using FaceMotion.Integration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    /// <summary>K2 orchestration tests: snapshots are ID based and every Direct item applies as one transaction.</summary>
    public sealed class BatchIntegrationServiceTests
    {
        private const string Folder = "Assets/__FaceMotionTests_K2";
        private GameObject _root;
        private VRCAvatarDescriptor _avatar;
        private FaceMotionProject _project;
        private FaceMotionAnimationData _smile;
        private FaceMotionAnimationData _frown;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__FaceMotionTests_K2");
            _root = new GameObject("Avatar"); _avatar = _root.AddComponent<VRCAvatarDescriptor>();
            var fx = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/original.controller");
            _avatar.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, isDefault = false, animatorController = fx } };
            _project = FaceMotionProject.CreateNew(); _smile = FaceMotionAnimationData.Create("Smile"); _frown = FaceMotionAnimationData.Create("Frown"); _project.AddAnimation(_smile); _project.AddAnimation(_frown);
            IntegrationBackendSelectionStore.Save(IntegrationBackendSelection.Direct);
        }

        [TearDown]
        public void TearDown()
        {
            ExportedClipRegistry.Remove(_smile.AnimationId); ExportedClipRegistry.Remove(_frown.AnimationId);
            Object.DestroyImmediate(_root); AssetDatabase.DeleteAsset(Folder); AssetDatabase.Refresh();
        }

        [Test]
        public void Execute_EmptySnapshotStopsBeforeExportOrApply()
        {
            var result = BatchIntegrationService.Execute(new BatchIntegrationRequest(_avatar, _project, new string[0], Folder));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostics, Has.Some.Matches<FaceMotionDiagnostic>(d => d.Code == FaceMotionDiagnosticCodes.BatchNoSelection));
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/FaceMotion_Batch"), Is.False);
        }

        [Test]
        public void Execute_DirectBatchUsesProjectOrderAndRerunsWithoutDuplicates()
        {
            ExportedClipRegistry.Record(_smile.AnimationId, Folder + "/Smile.anim");
            ExportedClipRegistry.Record(_frown.AnimationId, Folder + "/Frown.anim");
            var request = new BatchIntegrationRequest(_avatar, _project, new[] { _frown.AnimationId, _smile.AnimationId }, Folder);

            var first = BatchIntegrationService.Execute(request);
            var second = BatchIntegrationService.Execute(request);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.Items[0].AnimationId, Is.EqualTo(_smile.AnimationId));
            Assert.That(first.Items[1].AnimationId, Is.EqualTo(_frown.AnimationId));
            Assert.That(second.Succeeded, Is.True);
            Assert.That(_avatar.expressionParameters.parameters, Has.Length.EqualTo(2));
            Assert.That(((AnimatorController)_avatar.baseAnimationLayers[0].animatorController).layers, Has.Length.EqualTo(3));
        }

        [Test]
        public void Execute_DuplicateNamesUseDistinctSafeExportAndBackendNames()
        {
            _frown.DisplayName = "Smile";
            ExportedClipRegistry.Record(_smile.AnimationId, Folder + "/SmileA.anim");
            ExportedClipRegistry.Record(_frown.AnimationId, Folder + "/SmileB.anim");

            var result = BatchIntegrationService.Execute(new BatchIntegrationRequest(_avatar, _project, new[] { _smile.AnimationId, _frown.AnimationId }, Folder));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(_avatar.expressionParameters.parameters, Has.Length.EqualTo(2));
            Assert.That(_avatar.expressionParameters.parameters[0].name, Is.Not.EqualTo(_avatar.expressionParameters.parameters[1].name));
        }

        [Test]
        public void Execute_DuplicateRegisteredPathsRemainBlockedAndNameEveryConflict()
        {
            string path = Folder + "/FaceMotion.anim";
            ExportedClipRegistry.Record(_smile.AnimationId, path);
            ExportedClipRegistry.Record(_frown.AnimationId, path);

            var result = BatchIntegrationService.Execute(new BatchIntegrationRequest(_avatar, _project, new[] { _smile.AnimationId, _frown.AnimationId }, Folder));
            FaceMotionDiagnostic duplicate = null;
            for (int i = 0; i < result.Diagnostics.Count; i++)
            {
                if (result.Diagnostics[i].Code == FaceMotionDiagnosticCodes.BatchDuplicateExportPath)
                {
                    duplicate = result.Diagnostics[i];
                    break;
                }
            }

            Assert.That(result.Succeeded, Is.False);
            Assert.That(duplicate, Is.Not.Null);
            Assert.That(duplicate.Message, Does.Contain(path));
            Assert.That(duplicate.Message, Does.Contain("Smile"));
            Assert.That(duplicate.Message, Does.Contain("Frown"));
            Assert.That(duplicate.Details[FaceMotionDiagnosticDetailKeys.ConflictExportPath], Is.EqualTo(path));
            Assert.That(duplicate.Details[FaceMotionDiagnosticDetailKeys.ConflictAnimationNames], Does.Contain("Smile"));
            Assert.That(duplicate.Details[FaceMotionDiagnosticDetailKeys.ConflictAnimationNames], Does.Contain("Frown"));
            Assert.That(new FaceMotionDiagnosticPresentation(duplicate, FaceMotionDiagnosticLanguage.Japanese).Summary, Does.Contain("出力先"));
            Assert.That(new FaceMotionDiagnosticPresentation(duplicate, FaceMotionDiagnosticLanguage.English).Summary, Does.Contain("export path"));
            Assert.That(result.Items[0].Diagnostics, Has.Some.Matches<FaceMotionDiagnostic>(d => d.Code == FaceMotionDiagnosticCodes.BatchDuplicateExportPath));
            Assert.That(result.Items[1].Diagnostics, Has.Some.Matches<FaceMotionDiagnostic>(d => d.Code == FaceMotionDiagnosticCodes.BatchDuplicateExportPath));
        }
    }
}
