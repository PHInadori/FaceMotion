using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.ModularAvatar;
using FaceMotion.Editor.VRChat.Integration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// K8-1: a freshly created project never contains Assets/FaceMotion/Exports. The beginner button
    /// must still integrate, because FaceMotion's canonical root is created inside the mutation stage
    /// after every final-state check, never during the write-free preflight. Explicit custom paths
    /// keep their strict rules: they are validated as-is and are never created for the user.
    /// </summary>
    public sealed class K8_1FreshProjectMaRootTests
    {
        private const string CustomFolder = "Assets/__FM_K81_Custom";
        private GameObject _root;
        private VRCAvatarDescriptor _avatar;
        private FaceMotionProject _project;
        private FaceMotionAnimationData _animation;
        private bool _defaultOutputFolderExisted;

        [SetUp]
        public void SetUp()
        {
            _defaultOutputFolderExisted = AssetDatabase.IsValidFolder(OneClickIntegrationService.DefaultOutputFolder);
            DeleteIfPresent(OneClickIntegrationService.DefaultOutputFolder);
            DeleteIfPresent(CustomFolder);
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
            if (!_defaultOutputFolderExisted) DeleteIfPresent(OneClickIntegrationService.DefaultOutputFolder);
            DeleteIfPresent(CustomFolder);
            AssetDatabase.Refresh();
        }

        [Test]
        public void FreshProject_DefaultAdvancedOutputFolder_RealMaBackendIntegrates()
        {
            Assert.That(AssetDatabase.IsValidFolder(OneClickIntegrationService.DefaultOutputFolder), Is.False,
                "This test only proves the fresh-project case.");
            var backend = new ModularAvatarIntegrationBackend();

            var result = VrchatDesiredStateReconciliationService.Execute(new VrchatDesiredStateReconciliationRequest(
                _avatar, _project, new[] { _animation.AnimationId }, OneClickIntegrationService.DefaultOutputFolder, backend));

            Assert.That(result.Succeeded, Is.True, Describe(result));
            Assert.That(result.Diagnostics, Has.None.Matches<FaceMotionDiagnostic>(d => d.Code == FaceMotionDiagnosticCodes.ModularAvatarPath),
                "The canonical default root must be planbable before it exists.");
            Assert.That(result.Diagnostics, Has.None.Matches<FaceMotionDiagnostic>(d => d.Code == FaceMotionDiagnosticCodes.BatchPreflightFailed),
                "Preflight must validate the full final state without writing.");
            Assert.That(result.Outcome, Is.EqualTo(VrchatDesiredStateReconciliationOutcome.Succeeded));

            string root = OneClickIntegrationService.DefaultOutputFolder;
            Assert.That(AssetDatabase.IsValidFolder(root), Is.True, "The mutation stage creates FaceMotion's canonical root.");
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(root + "/Renamable.anim"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(root + "/FaceMotionMA_Renamable/Manifest.asset"), Is.Not.Null);
            Assert.That(_root.transform.Find("FaceMotion MA Renamable"), Is.Not.Null);
        }

        [Test]
        public void FreshProject_EmptyOutputFolder_UsesTheSameCanonicalRoot()
        {
            var backend = new ModularAvatarIntegrationBackend();

            var result = VrchatDesiredStateReconciliationService.Execute(new VrchatDesiredStateReconciliationRequest(
                _avatar, _project, new[] { _animation.AnimationId }, null, backend));

            Assert.That(result.Succeeded, Is.True, Describe(result));
            Assert.That(AssetDatabase.IsValidFolder(OneClickIntegrationService.DefaultOutputFolder), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(OneClickIntegrationService.DefaultOutputFolder + "/Renamable.anim"), Is.Not.Null);
        }

        [Test]
        public void FreshProject_ExplicitCustomFolderThatDoesNotExist_FailsWithoutCreatingIt()
        {
            var backend = new ModularAvatarIntegrationBackend();

            var result = VrchatDesiredStateReconciliationService.Execute(new VrchatDesiredStateReconciliationRequest(
                _avatar, _project, new[] { _animation.AnimationId }, CustomFolder, backend));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(VrchatDesiredStateReconciliationOutcome.PreflightFailed));
            Assert.That(AssetDatabase.IsValidFolder(CustomFolder), Is.False,
                "Only FaceMotion's canonical default root may ever be created automatically.");
            Assert.That(AssetDatabase.IsValidFolder(OneClickIntegrationService.DefaultOutputFolder), Is.False,
                "A rejected custom path must not silently fall back to the beginner default.");
        }

        [Test]
        public void FreshProject_RemoveOnly_CreatesNoRootAtAll()
        {
            var backend = new ModularAvatarIntegrationBackend();

            var result = VrchatDesiredStateReconciliationService.Execute(new VrchatDesiredStateReconciliationRequest(
                _avatar, _project, new string[0], null, backend));

            Assert.That(result.Succeeded, Is.True, Describe(result));
            Assert.That(AssetDatabase.IsValidFolder(OneClickIntegrationService.DefaultOutputFolder), Is.False);
        }

        private static string Describe(VrchatDesiredStateReconciliationResult result)
        {
            var builder = new System.Text.StringBuilder();
            builder.Append("outcome=").Append(result.Outcome);
            for (var i = 0; i < result.Diagnostics.Count; i++)
                builder.Append(" | ").Append(result.Diagnostics[i].Code).Append(": ").Append(result.Diagnostics[i].Message);
            return builder.ToString();
        }

        private static void DeleteIfPresent(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
        }
    }
}
