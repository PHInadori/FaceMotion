using System.Collections.Generic;
using FaceMotion.Data;
using FaceMotion.Diagnostics;
using FaceMotion.Editor.VRChat.Integration;
using FaceMotion.Integration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace FaceMotion.Editor.Tests
{
    /// <summary>
    /// J4 one-click "VRChatへ追加" flow contract tests. These run wherever the Direct
    /// backend exists and Modular Avatar is optional, so every one of them either uses the
    /// Direct backend explicitly or asserts the desired behavior when MA is missing.
    /// </summary>
    public sealed class OneClickIntegrationTests
    {
        private const string Folder = "Assets/__FaceMotionTests_J4";
        private const string Out = Folder + "/Out";
        private GameObject _root;
        private VRCAvatarDescriptor _avatar;
        private AnimatorController _fx;
        private FaceMotionAnimationData _animation;
        private FaceMotionProject _project;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__FaceMotionTests_J4");
            if (!AssetDatabase.IsValidFolder(Out)) AssetDatabase.CreateFolder(Folder, "Out");
            _root = new GameObject("Avatar"); _avatar = _root.AddComponent<VRCAvatarDescriptor>();
            _fx = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/original.controller");
            _avatar.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, isDefault = false, animatorController = _fx } };
            _animation = FaceMotionAnimationData.Create("Smile");
            _project = FaceMotionProject.CreateNew(); _project.AddAnimation(_animation);
            IntegrationBackendSelectionStore.Save(IntegrationBackendSelection.Direct);
        }

        [TearDown]
        public void TearDown()
        {
            IntegrationBackendSelectionStore.Save(IntegrationBackendSelection.Direct);
            DirectVRChatIntegration.ApplyFailureInjector = null;
            DirectVRChatIntegration.PlanFailureInjector = null;
            if (_animation != null) ExportedClipRegistry.Remove(_animation.AnimationId);
            Object.DestroyImmediate(_root); AssetDatabase.DeleteAsset(Folder); AssetDatabase.Refresh();
        }

        private OneClickIntegrationRequest Request(FaceMotionAnimationData animation = null, string outFolder = Out)
        {
            return new OneClickIntegrationRequest(_avatar, animation ?? _animation, _project, outFolder);
        }

        private static bool Contains(IReadOnlyList<FaceMotionDiagnostic> items, string code)
        {
            for (int i = 0; i < items.Count; i++) if (items[i].Code == code) return true;
            return false;
        }

        [Test]
        public void Execute_DirectHappyPath_ExportsAppliesAndReportsEveryOutcome()
        {
            string path = Out + "/" + OneClickIntegrationService.BuildStem(Request()) + ".anim";
            ExportedClipRegistry.Record(_animation.AnimationId, path);
            var stages = new System.Collections.Generic.List<OneClickStage>();

            var result = OneClickIntegrationService.Execute(Request(), stages.Add);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Stage, Is.EqualTo(OneClickStage.Done));
            Assert.That(result.Backend, Is.EqualTo(DirectVRChatIntegration.BackendId));
            Assert.That(result.ParameterName, Is.EqualTo("FaceMotion_Smile"));
            Assert.That(result.ExportPath, Is.EqualTo(path));
            Assert.That(result.Clip, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(path), Is.SameAs(result.Clip));
            Assert.That(result.Manifest, Is.Not.Null);
            Assert.That(_avatar.expressionParameters, Is.Not.Null);
            Assert.That(_avatar.expressionsMenu, Is.Not.Null);
            Assert.That(_avatar.baseAnimationLayers[0].animatorController, Is.Not.SameAs(_fx));
            Assert.That(AssetDatabase.IsValidFolder(Out + "/FaceMotion_Smile"), Is.True);
            Assert.That(result.Diagnostics, Has.Some.Matches<FaceMotionDiagnostic>(d => d.Code == FaceMotionDiagnosticCodes.OneClickExported));
            Assert.That(result.Diagnostics, Has.Some.Matches<FaceMotionDiagnostic>(d => d.Code == FaceMotionDiagnosticCodes.OneClickSucceeded));
            Assert.That(stages, Is.EqualTo(new[] { OneClickStage.Export, OneClickStage.Plan, OneClickStage.Apply }));
        }

        [Test]
        public void Execute_SecondRun_ReappliesInPlaceWithoutDuplicatingAnything()
        {
            string path = Out + "/" + OneClickIntegrationService.BuildStem(Request()) + ".anim";
            ExportedClipRegistry.Record(_animation.AnimationId, path);

            var first = OneClickIntegrationService.Execute(Request());
            var second = OneClickIntegrationService.Execute(Request());

            Assert.That(first.Succeeded, Is.True);
            Assert.That(second.Succeeded, Is.True, second.Diagnostics[second.Diagnostics.Count - 1].Message);
            Assert.That(second.Manifest, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<DirectIntegrationManifest>(Out + "/FaceMotion_Smile/Manifest.asset"), Is.Not.Null);
            Assert.That(Contains(first.Diagnostics, FaceMotionDiagnosticCodes.OneClickReapplied), Is.False);
            Assert.That(Contains(second.Diagnostics, FaceMotionDiagnosticCodes.OneClickReapplied), Is.True);
            var manifest = (DirectIntegrationManifest)second.Manifest;
            var controller = manifest.GeneratedFx;
            int faceMotionLayers = 0;
            for (int i = 0; i < controller.layers.Length; i++) if (controller.layers[i].name.StartsWith("FaceMotion", System.StringComparison.Ordinal)) faceMotionLayers++;
            Assert.That(faceMotionLayers, Is.EqualTo(1));
            Assert.That(_avatar.expressionParameters.parameters, Has.Some.Matches<VRCExpressionParameters.Parameter>(p => p.name == "FaceMotion_Smile"));
            Assert.That(AssetDatabase.IsValidFolder(Out + "/FaceMotion_Smile 1"), Is.False);
        }

        [Test]
        public void Execute_SecondRun_PreservesTheExportedClipGuid()
        {
            string path = Out + "/" + OneClickIntegrationService.BuildStem(Request()) + ".anim";
            ExportedClipRegistry.Record(_animation.AnimationId, path);

            OneClickIntegrationService.Execute(Request());
            string guid1 = AssetDatabase.AssetPathToGUID(path);
            var second = OneClickIntegrationService.Execute(Request());

            Assert.That(second.Succeeded, Is.True);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid1));
            Assert.That(ExportedClipRegistry.TryGetGuid(_animation.AnimationId, out string stored), Is.True);
            Assert.That(stored, Is.EqualTo(guid1));
            Assert.That(ExportedClipRegistry.IsOwned(_animation.AnimationId, path), Is.True);
        }

        [Test]
        public void Execute_PlanBlock_StopsBeforeApplyAfterExporting()
        {
            var machine = new AnimatorStateMachine { name = "FaceMotion Existing" };
            _fx.AddLayer(new AnimatorControllerLayer { name = "FaceMotion Existing", stateMachine = machine });
            string path = Out + "/" + OneClickIntegrationService.BuildStem(Request()) + ".anim";
            ExportedClipRegistry.Record(_animation.AnimationId, path);

            var result = OneClickIntegrationService.Execute(Request());

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Stage, Is.EqualTo(OneClickStage.Validate));
            Assert.That(result.Clip, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(path), Is.SameAs(result.Clip));
            Assert.That(result.Manifest, Is.Null);
            Assert.That(_avatar.baseAnimationLayers[0].animatorController, Is.SameAs(_fx));
            Assert.That(_avatar.expressionParameters, Is.Null);
            Assert.That(AssetDatabase.IsValidFolder(Out + "/FaceMotion_Smile"), Is.False);
            Assert.That(Contains(result.Diagnostics, "FM-G-LAYER-CONFLICT"), Is.True);
        }

        [Test]
        public void Execute_ExportBlock_StopsBeforePlanning()
        {
            _animation.Timeline.Duration = 0f;
            string path = Out + "/" + OneClickIntegrationService.BuildStem(Request()) + ".anim";
            ExportedClipRegistry.Record(_animation.AnimationId, path);

            var result = OneClickIntegrationService.Execute(Request());

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Stage, Is.EqualTo(OneClickStage.Export));
            Assert.That(result.Clip, Is.Null);
            Assert.That(result.ParameterName, Is.EqualTo("FaceMotion_Smile"));
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(path), Is.Null);
            Assert.That(_avatar.baseAnimationLayers[0].animatorController, Is.SameAs(_fx));
            Assert.That(Contains(result.Diagnostics, FaceMotionDiagnosticCodes.ExportInvalidDuration), Is.True);
        }

        [Test]
        public void Execute_ForeignClipAtDestination_BlocksAndPreservesTheForeignAsset()
        {
            string path = Out + "/foreign-source.anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();
            ExportedClipRegistry.Record(_animation.AnimationId, path);
            var foreign = new AnimationClip();
            AssetDatabase.CreateAsset(foreign, path);
            Assert.That(ExportedClipRegistry.IsOwned(_animation.AnimationId, path), Is.False);

            var result = OneClickIntegrationService.Execute(Request());

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Stage, Is.EqualTo(OneClickStage.Export));
            Assert.That(Contains(result.Diagnostics, FaceMotionDiagnosticCodes.OneClickForeignClip), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(path), Is.SameAs(foreign));
            Assert.That(_avatar.baseAnimationLayers[0].animatorController, Is.SameAs(_fx));
            Assert.That(AssetDatabase.IsValidFolder(Out + "/FaceMotion_Smile"), Is.False);
        }

        [Test]
        public void Execute_NoCurrentAnimation_BlocksBeforeExport()
        {
            var result = OneClickIntegrationService.Execute(new OneClickIntegrationRequest(_avatar, null, _project, Out));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Stage, Is.EqualTo(OneClickStage.NotStarted));
            Assert.That(result.Clip, Is.Null);
            Assert.That(result.ExportPath, Is.Empty);
            Assert.That(Contains(result.Diagnostics, FaceMotionDiagnosticCodes.OneClickNoCurrentAnimation), Is.True);
            Assert.That(_avatar.baseAnimationLayers[0].animatorController, Is.SameAs(_fx));
        }

        [Test]
        public void Execute_NoAvatar_BlocksBeforeExport()
        {
            var result = OneClickIntegrationService.Execute(new OneClickIntegrationRequest(null, _animation, _project, Out));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Stage, Is.EqualTo(OneClickStage.NotStarted));
            Assert.That(result.Clip, Is.Null);
            Assert.That(Contains(result.Diagnostics, FaceMotionDiagnosticCodes.OneClickNoAvatar), Is.True);
        }

        [Test]
        public void Execute_ApplyException_RollsBackAndReportsTheApplyStage()
        {
            DirectVRChatIntegration.ApplyFailureInjector = stage => { if (stage == "after-parameters") throw new System.InvalidOperationException("j4-injected"); };
            string path = Out + "/" + OneClickIntegrationService.BuildStem(Request()) + ".anim";
            ExportedClipRegistry.Record(_animation.AnimationId, path);

            var result = OneClickIntegrationService.Execute(Request());

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Stage, Is.EqualTo(OneClickStage.Apply));
            Assert.That(result.Manifest, Is.Null);
            Assert.That(_avatar.baseAnimationLayers[0].animatorController, Is.SameAs(_fx));
            Assert.That(_avatar.expressionParameters, Is.Null);
            Assert.That(AssetDatabase.IsValidFolder(Out + "/FaceMotion_Smile"), Is.False);
            Assert.That(Contains(result.Diagnostics, "FM-G-APPLY"), Is.True);
            Assert.That(Contains(result.Diagnostics, FaceMotionDiagnosticCodes.OneClickNoPartialState), Is.True);
        }

        [Test]
        public void Execute_RequestedMaButMissing_BlocksWithoutExporting()
        {
            if (ModularAvatarIntegrationBackendLocator.Create() != null) Assert.Ignore("Modular Avatar is installed; the no-MA backend-unavailable path is covered by the no-MA smoke project.");
            IntegrationBackendSelectionStore.Save(IntegrationBackendSelection.ModularAvatar);

            var result = OneClickIntegrationService.Execute(Request());

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Stage, Is.EqualTo(OneClickStage.NotStarted));
            Assert.That(result.Backend, Is.EqualTo(OneClickIntegrationService.ModularAvatarBackendId));
            Assert.That(result.Clip, Is.Null);
            Assert.That(result.ExportPath, Is.Empty);
            Assert.That(Contains(result.Diagnostics, FaceMotionDiagnosticCodes.OneClickBackendUnavailable), Is.True);
            Assert.That(_avatar.baseAnimationLayers[0].animatorController, Is.SameAs(_fx));
        }

        [Test]
        public void Preflight_HappyPath_IsWriteFreeAndReady()
        {
            string path = Out + "/" + OneClickIntegrationService.BuildStem(Request()) + ".anim";
            ExportedClipRegistry.Record(_animation.AnimationId, path);
            AssetDatabase.Refresh();

            var preflight = OneClickIntegrationService.Preflight(Request());

            Assert.That(preflight.Ready, Is.True);
            Assert.That(preflight.BackendId, Is.EqualTo(DirectVRChatIntegration.BackendId));
            Assert.That(preflight.ExportPath, Is.EqualTo(path));
            Assert.That(preflight.ParameterName, Is.EqualTo("FaceMotion_Smile"));
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(path), Is.Null);
            Assert.That(AssetDatabase.IsValidFolder(Out + "/FaceMotion_Smile"), Is.False);
            Assert.That(_avatar.baseAnimationLayers[0].animatorController, Is.SameAs(_fx));
        }

        [Test]
        public void Preflight_ExportBlock_ReportsReadyFalse()
        {
            _animation.Timeline.Duration = 0f;
            string path = Out + "/" + OneClickIntegrationService.BuildStem(Request()) + ".anim";
            ExportedClipRegistry.Record(_animation.AnimationId, path);

            var preflight = OneClickIntegrationService.Preflight(Request());

            Assert.That(preflight.Ready, Is.False);
            Assert.That(Contains(preflight.Diagnostics, FaceMotionDiagnosticCodes.ExportInvalidDuration), Is.True);
            Assert.That(preflight.ExportPath, Is.EqualTo(path));
        }

        [Test]
        public void Preflight_NoCurrentAnimation_ReportsAnEmptyNotReadySummary()
        {
            var preflight = OneClickIntegrationService.Preflight(new OneClickIntegrationRequest(_avatar, null, _project, Out));

            Assert.That(preflight.Ready, Is.False);
            Assert.That(preflight.BackendId, Is.Empty);
            Assert.That(preflight.ExportPath, Is.Empty);
            Assert.That(Contains(preflight.Diagnostics, FaceMotionDiagnosticCodes.OneClickNoCurrentAnimation), Is.True);
        }

        [Test]
        public void Preflight_RequestedMaButMissing_ReportsNotReadyBackendId()
        {
            if (ModularAvatarIntegrationBackendLocator.Create() != null) Assert.Ignore("Modular Avatar is installed; the no-MA backend-unavailable path is covered by the no-MA smoke project.");
            IntegrationBackendSelectionStore.Save(IntegrationBackendSelection.ModularAvatar);

            var preflight = OneClickIntegrationService.Preflight(Request());

            Assert.That(preflight.Ready, Is.False);
            Assert.That(preflight.BackendId, Is.EqualTo(OneClickIntegrationService.ModularAvatarBackendId));
            Assert.That(Contains(preflight.Diagnostics, FaceMotionDiagnosticCodes.OneClickBackendUnavailable), Is.True);
        }

        [Test]
        public void BuildStem_SanitizesNamesAndDisambiguatesDuplicateDisplayNames()
        {
            var messy = new OneClickIntegrationRequest(_avatar, FaceMotionAnimationData.Create("Happy Face!"), _project, Out);

            Assert.That(OneClickIntegrationService.BuildStem(messy), Is.EqualTo("Happy_Face_"));

            var second = FaceMotionAnimationData.Create("Smile");
            _project.AddAnimation(second);
            var duplicate = new OneClickIntegrationRequest(_avatar, _animation, _project, Out);
            string suffix = _animation.AnimationId.Substring(0, 6);

            Assert.That(OneClickIntegrationService.BuildStem(duplicate), Is.EqualTo("Smile_" + suffix));
        }

        [Test]
        public void BuildStem_NullAnimationAndEmptyName_FallBackToFaceMotion()
        {
            var nullAnimation = new OneClickIntegrationRequest(_avatar, null, _project, Out);

            Assert.That(OneClickIntegrationService.BuildStem(nullAnimation), Is.EqualTo("FaceMotion"));
            Assert.That(OneClickIntegrationService.Sanitize(""), Is.EqualTo("FaceMotion"));
            Assert.That(OneClickIntegrationService.Sanitize(null), Is.EqualTo("FaceMotion"));
        }

        [Test]
        public void ResolveExportPath_NewAnimationsUseReadableDisplayNamePaths()
        {
            _animation.DisplayName = "shirome";
            var second = FaceMotionAnimationData.Create("yorokobi");
            _project.AddAnimation(second);
            try
            {
                Assert.That(OneClickIntegrationService.ResolveExportPath(Request(_animation), new List<FaceMotionDiagnostic>()), Is.EqualTo(Out + "/shirome.anim"));
                Assert.That(OneClickIntegrationService.ResolveExportPath(Request(second), new List<FaceMotionDiagnostic>()), Is.EqualTo(Out + "/yorokobi.anim"));
                Assert.That(
                    OneClickIntegrationService.ResolveExportPath(
                        new OneClickIntegrationRequest(_avatar, _animation, _project, null),
                        new List<FaceMotionDiagnostic>()),
                    Is.EqualTo("Assets/FaceMotion/Exports/shirome.anim"));
            }
            finally
            {
                ExportedClipRegistry.Remove(second.AnimationId);
            }
        }

        [Test]
        public void ResolveExportPath_DuplicateNamesUseDeterministicNumericSuffixes()
        {
            _animation.DisplayName = "smile";
            var second = FaceMotionAnimationData.Create("smile");
            _project.AddAnimation(second);
            try
            {
                Assert.That(OneClickIntegrationService.ResolveExportPath(Request(_animation), new List<FaceMotionDiagnostic>()), Is.EqualTo(Out + "/smile.anim"));
                Assert.That(OneClickIntegrationService.ResolveExportPath(Request(second), new List<FaceMotionDiagnostic>()), Is.EqualTo(Out + "/smile_2.anim"));
            }
            finally
            {
                ExportedClipRegistry.Remove(second.AnimationId);
            }
        }

        [Test]
        public void SanitizeExportFileName_PreservesJapaneseAndRemovesUnsafeCharacters()
        {
            Assert.That(OneClickIntegrationService.SanitizeExportFileName("喜び"), Is.EqualTo("喜び"));
            Assert.That(OneClickIntegrationService.SanitizeExportFileName("  Happy / \\ : * ? \" < > |  Face  "), Is.EqualTo("Happy Face"));
            Assert.That(OneClickIntegrationService.SanitizeExportFileName(" /\\:*?\"<>| "), Is.EqualTo("FaceMotion"));
        }

        [Test]
        public void ResolveExportPath_DuplicateAnimationGetsFreshUniqueDefaultPath()
        {
            _animation.DisplayName = "yorokobi";
            var duplicate = _project.DuplicateAnimation(_animation.AnimationId);
            try
            {
                Assert.That(duplicate, Is.Not.Null);
                Assert.That(duplicate.AnimationId, Is.Not.EqualTo(_animation.AnimationId));
                Assert.That(OneClickIntegrationService.ResolveExportPath(Request(_animation), new List<FaceMotionDiagnostic>()), Is.EqualTo(Out + "/yorokobi.anim"));
                Assert.That(OneClickIntegrationService.ResolveExportPath(Request(duplicate), new List<FaceMotionDiagnostic>()), Is.EqualTo(Out + "/yorokobi_2.anim"));
            }
            finally
            {
                if (duplicate != null) ExportedClipRegistry.Remove(duplicate.AnimationId);
            }
        }

        [Test]
        public void ResolveExportPath_ManualPathSurvivesRename()
        {
            string manualPath = Out + "/Hand Authored.anim";
            ExportedClipRegistry.Record(_animation.AnimationId, manualPath);
            var rename = new FaceMotion.Editor.RenameAnimationCommand(_animation.AnimationId, "renamed");

            Assert.That(rename.Validate(_project, out _), Is.True);
            rename.Execute(_project);

            Assert.That(_animation.DisplayName, Is.EqualTo("renamed"));
            Assert.That(OneClickIntegrationService.ResolveExportPath(Request(), new List<FaceMotionDiagnostic>()), Is.EqualTo(manualPath));
        }

        [Test]
        public void BackendStore_ResolvesDirectWhenModularAvatarIsMissing()
        {
            Assert.That(IntegrationBackendSelectionStore.ResolveInitial(false, IntegrationBackendSelection.Direct, false), Is.EqualTo(IntegrationBackendSelection.Direct));
            Assert.That(IntegrationBackendSelectionStore.ResolveInitial(false, IntegrationBackendSelection.ModularAvatar, false), Is.EqualTo(IntegrationBackendSelection.Direct));
            Assert.That(IntegrationBackendSelectionStore.ResolveInitial(true, IntegrationBackendSelection.Direct, false), Is.EqualTo(IntegrationBackendSelection.Direct));
            Assert.That(IntegrationBackendSelectionStore.ResolveInitial(true, IntegrationBackendSelection.ModularAvatar, false), Is.EqualTo(IntegrationBackendSelection.ModularAvatar));
        }

        [Test]
        public void BackendStore_PrefersModularAvatarByDefaultWhenAvailable()
        {
            Assert.That(IntegrationBackendSelectionStore.ResolveInitial(false, IntegrationBackendSelection.Direct, true), Is.EqualTo(IntegrationBackendSelection.ModularAvatar));
            Assert.That(IntegrationBackendSelectionStore.ResolveInitial(true, IntegrationBackendSelection.Direct, true), Is.EqualTo(IntegrationBackendSelection.Direct));
            Assert.That(IntegrationBackendSelectionStore.ResolveInitial(true, IntegrationBackendSelection.ModularAvatar, true), Is.EqualTo(IntegrationBackendSelection.ModularAvatar));
        }

        [Test]
        public void ExportedClipRegistry_OwnershipRequiresBothPathAndGuidToMatch()
        {
            string path = Out + "/Smile.anim";
            var clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);

            Assert.That(ExportedClipRegistry.IsOwned(_animation.AnimationId, path), Is.False);

            ExportedClipRegistry.Record(_animation.AnimationId, path);

            Assert.That(ExportedClipRegistry.IsOwned(_animation.AnimationId, path), Is.True);
            Assert.That(ExportedClipRegistry.IsOwned(_animation.AnimationId, Out + "/Other.anim"), Is.False);

            string other = Out + "/Other.anim";
            var otherClip = new AnimationClip();
            AssetDatabase.CreateAsset(otherClip, other);
            ExportedClipRegistry.Record(_animation.AnimationId, other);
            Assert.That(ExportedClipRegistry.IsOwned(_animation.AnimationId, path), Is.False);

            ExportedClipRegistry.Remove(_animation.AnimationId);
            Assert.That(ExportedClipRegistry.TryGetPath(_animation.AnimationId, out _), Is.False);
        }

        [Test]
        public void Execute_WithDuplicateDisplayName_DisambiguatesTheExportFileButKeepsBackendNaming()
        {
            var second = FaceMotionAnimationData.Create("Smile");
            _project.AddAnimation(second);
            string stem = "Smile_" + _animation.AnimationId.Substring(0, 6);
            string path = Out + "/" + stem + ".anim";
            ExportedClipRegistry.Record(_animation.AnimationId, path);

            var result = OneClickIntegrationService.Execute(Request());

            Assert.That(result.Succeeded, Is.True, result.Diagnostics[result.Diagnostics.Count - 1].Message);
            Assert.That(result.ExportPath, Is.EqualTo(path));
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(path), Is.SameAs(result.Clip));
            Assert.That(result.ParameterName, Is.EqualTo("FaceMotion_Smile"));
            Assert.That(ExportedClipRegistry.IsOwned(_animation.AnimationId, path), Is.True);
        }
    }
}
