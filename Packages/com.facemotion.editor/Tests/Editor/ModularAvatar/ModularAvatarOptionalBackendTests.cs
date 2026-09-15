using System.Collections;
using System.Reflection;
using FaceMotion.Editor.VRChat.Integration;
using FaceMotion.Editor.ModularAvatar;
using FaceMotion.Editor.UI.Panels;
using FaceMotion.Integration;
using nadena.dev.modular_avatar.core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace FaceMotion.Editor.Tests
{
    public sealed class ModularAvatarOptionalBackendTests
    {
        private const string Folder = "Assets/__FaceMotionTests_H";
        private GameObject _root;
        private VRCAvatarDescriptor _avatar;
        private AnimationClip _clip;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__FaceMotionTests_H");
            _root = new GameObject("Avatar"); _avatar = _root.AddComponent<VRCAvatarDescriptor>();
            _clip = new AnimationClip(); AssetDatabase.CreateAsset(_clip, Folder + "/motion.anim");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root); AssetDatabase.DeleteAsset(Folder); AssetDatabase.Refresh();
        }
        [Test]
        public void Metadata_MissingPackageIsUnavailableWithNoVersion()
        {
            var availability = ModularAvatarOptionalBackend.EvaluatePackageMetadata(false, "1.9.0");

            Assert.That(availability.PackageInstalled, Is.False);
            Assert.That(availability.HasPackageVersion, Is.False);
            Assert.That(availability.BackendAvailable, Is.False);
        }

        [Test]
        public void Metadata_InstalledPackageRetainsItsReportedVersionButDoesNotEnableAnUnimplementedBackend()
        {
            var availability = ModularAvatarOptionalBackend.EvaluatePackageMetadata(true, " 1.9.0 ");

            Assert.That(availability.PackageInstalled, Is.True);
            Assert.That(availability.PackageVersion, Is.EqualTo("1.9.0"));
            Assert.That(availability.BackendAvailable, Is.False);
        }

        [Test]
        public void MetadataUnavailableIsDistinctFromAnAbsentPackage()
        {
            var availability = new FaceMotion.Integration.OptionalIntegrationBackendAvailability(ModularAvatarOptionalBackend.Id, false, string.Empty, false, "metadata unavailable", false);

            Assert.That(availability.PackageMetadataAvailable, Is.False);
            Assert.That(availability.PackageInstalled, Is.False);
        }

        [Test]
        public void Metadata_WhenPackageIsAbsentIsBlockingAndPerformsNoIntegration()
        {
            var availability = ModularAvatarOptionalBackend.EvaluatePackageMetadata(false, string.Empty);

            Assert.That(availability.BackendAvailable, Is.False);
            Assert.That(availability.Reason, Does.Contain("not installed"));
        }

        [Test]
        public void Apply_ReapplyAndRemove_OnlyManageFaceMotionMaObjects()
        {
            var backend = new ModularAvatarIntegrationBackend();
            var request = new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, "Smile");
            var first = backend.Apply(backend.Plan(request));
            Assert.That(first.Succeeded, Is.True);
            Assert.That(_root.transform.Find("FaceMotion MA Smile"), Is.Not.Null);

            var second = backend.Apply(backend.Plan(request));
            Assert.That(second.Succeeded, Is.True, second.Diagnostics.Count == 0 ? "No diagnostic" : second.Diagnostics[0].Message);
            Assert.That(_root.transform.Find("FaceMotion MA Smile"), Is.Not.Null);
            Assert.That(backend.Remove(_avatar).Succeeded, Is.True);
            Assert.That(_root.transform.Find("FaceMotion MA Smile"), Is.Null);
        }

        [Test]
        public void Plan_RejectsNullRequestAndInvalidOutputFoldersWithoutMutation()
        {
            var backend = new ModularAvatarIntegrationBackend();

            var nullPlan = backend.Plan(null);
            var invalidPathPlan = backend.Plan(new ModularAvatarIntegrationRequest(_avatar, _clip, "Assets/../Assets", "Smile"));

            Assert.That(nullPlan.IsValid, Is.False);
            Assert.That(nullPlan.Diagnostics, Has.Some.Matches<FaceMotion.Diagnostics.FaceMotionDiagnostic>(d => d.Code == "FM-H-MA-AVATAR"));
            Assert.That(nullPlan.Diagnostics, Has.Some.Matches<FaceMotion.Diagnostics.FaceMotionDiagnostic>(d => d.Code == "FM-H-MA-CLIP"));
            Assert.That(invalidPathPlan.Diagnostics, Has.Some.Matches<FaceMotion.Diagnostics.FaceMotionDiagnostic>(d => d.Code == "FM-H-MA-PATH"));
            Assert.That(_root.transform.childCount, Is.EqualTo(0));
        }

        [Test]
        public void Plan_RejectsPrefabAssetsWithoutCreatingMaObjects()
        {
            var prefabPath = Folder + "/Avatar.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(_root, prefabPath);
            var backend = new ModularAvatarIntegrationBackend();

            var plan = backend.Plan(new ModularAvatarIntegrationRequest(prefab.GetComponent<VRCAvatarDescriptor>(), _clip, Folder, "Prefab"));

            Assert.That(plan.Diagnostics, Has.Some.Matches<FaceMotion.Diagnostics.FaceMotionDiagnostic>(d => d.Code == "FM-H-MA-PREFAB-ASSET"));
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/FaceMotionMA_Prefab"), Is.False);
        }

        [Test]
        public void Plan_SanitizesNamesAndRejectsOverlongParameters()
        {
            var backend = new ModularAvatarIntegrationBackend();
            var normal = backend.Plan(new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, "Happy Face!"));
            var overlong = backend.Plan(new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, new string('a', 257)));

            Assert.That(normal.ParameterName, Is.EqualTo("FaceMotion_Happy_Face_"));
            Assert.That(normal.ObjectName, Is.EqualTo("FaceMotion MA Happy_Face_"));
            Assert.That(overlong.Diagnostics, Has.Some.Matches<FaceMotion.Diagnostics.FaceMotionDiagnostic>(d => d.Code == "FM-H-MA-PARAMETER-NAME"));
        }

        [Test]
        public void Plan_RejectsExistingMaParameterButAllowsAnEmptyParameterList()
        {
            var other = new GameObject("Other"); other.transform.SetParent(_root.transform);
            var parameters = other.AddComponent<ModularAvatarParameters>();
            parameters.parameters = null;
            var backend = new ModularAvatarIntegrationBackend();

            Assert.That(backend.Plan(new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, "Smile")).IsValid, Is.True);
            parameters.parameters = new System.Collections.Generic.List<ParameterConfig> { new ParameterConfig { nameOrPrefix = "FaceMotion_Smile", syncType = ParameterSyncType.Bool } };
            Assert.That(backend.Plan(new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, "Smile")).Diagnostics, Has.Some.Matches<FaceMotion.Diagnostics.FaceMotionDiagnostic>(d => d.Code == "FM-H-MA-PARAMETER-CONFLICT"));
        }

        [Test]
        public void Plan_RejectsBindingsInOtherMaMergesAndAvatarFx()
        {
            var binding = EditorCurveBinding.FloatCurve("Body", typeof(Transform), "m_LocalPosition.x");
            AnimationUtility.SetEditorCurve(_clip, binding, AnimationCurve.Linear(0, 0, 1, 1));
            var otherClip = new AnimationClip(); AssetDatabase.CreateAsset(otherClip, Folder + "/other.anim");
            AnimationUtility.SetEditorCurve(otherClip, binding, AnimationCurve.Linear(0, 1, 1, 0));
            var other = new GameObject("Other"); other.transform.SetParent(_root.transform);
            other.AddComponent<ModularAvatarMergeAnimator>().animator = CreateControllerWithClip("other.controller", otherClip);
            var backend = new ModularAvatarIntegrationBackend();

            Assert.That(backend.Plan(new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, "Smile")).Diagnostics, Has.Some.Matches<FaceMotion.Diagnostics.FaceMotionDiagnostic>(d => d.Code == "FM-H-MA-BINDING-CONFLICT"));
            Object.DestroyImmediate(other);
            _avatar.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, animatorController = CreateControllerWithClip("fx.controller", otherClip) } };
            Assert.That(backend.Plan(new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, "Smile")).Diagnostics, Has.Some.Matches<FaceMotion.Diagnostics.FaceMotionDiagnostic>(d => d.Code == "FM-H-MA-CROSS-BINDING-CONFLICT"));
        }

        [Test]
        public void Apply_ConfiguresRealMaComponentsAndOwnedAssets()
        {
            var backend = new ModularAvatarIntegrationBackend();
            var result = backend.Apply(backend.Plan(new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, "Smile")));
            var node = _root.transform.Find("FaceMotion MA Smile").gameObject;
            var merge = node.GetComponent<ModularAvatarMergeAnimator>();
            var parameters = node.GetComponent<ModularAvatarParameters>();
            var installer = node.GetComponent<ModularAvatarMenuInstaller>();
            var manifest = (ModularAvatarIntegrationManifest)result.Manifest;

            Assert.That(result.Succeeded, Is.True);
            Assert.That(merge.animator, Is.Not.Null);
            Assert.That(merge.layerType, Is.EqualTo(VRCAvatarDescriptor.AnimLayerType.FX));
            Assert.That(merge.pathMode, Is.EqualTo(MergeAnimatorPathMode.Absolute));
            Assert.That(merge.deleteAttachedAnimator, Is.False);
            Assert.That(merge.matchAvatarWriteDefaults, Is.False);
            Assert.That(parameters.parameters, Has.Count.EqualTo(1));
            Assert.That(parameters.parameters[0].nameOrPrefix, Is.EqualTo("FaceMotion_Smile"));
            Assert.That(parameters.parameters[0].syncType, Is.EqualTo(ParameterSyncType.Bool));
            Assert.That(installer.menuToAppend.controls[0].parameter.name, Is.EqualTo("FaceMotion_Smile"));
            Assert.That(manifest.IntegrationObjectGlobalId, Is.Not.Empty);
            Assert.That(manifest.OwnedAssetPaths, Is.EquivalentTo(new[]
            {
                Folder + "/FaceMotionMA_Smile/FX.controller",
                Folder + "/FaceMotionMA_Smile/Menu.asset",
                Folder + "/FaceMotionMA_Smile/Reset.anim"
            }));
        }

        [Test]
        public void ApplyAndRemove_UseUndoForTheRealMaGameObject()
        {
            Undo.ClearAll();
            var backend = new ModularAvatarIntegrationBackend();
            Assert.That(backend.Apply(backend.Plan(new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, "Undo"))).Succeeded, Is.True);
            Assert.That(_root.transform.Find("FaceMotion MA Undo"), Is.Not.Null);

            Undo.PerformUndo();

            Assert.That(_root.transform.Find("FaceMotion MA Undo"), Is.Null);
        }

        [Test]
        public void Remove_SkipsTamperedOwnedAssetsAndDoesNotDeleteSameNamedUnmanagedObject()
        {
            var backend = new ModularAvatarIntegrationBackend();
            var result = backend.Apply(backend.Plan(new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, "Smile")));
            var manifest = (ModularAvatarIntegrationManifest)result.Manifest;
            var unrelated = new AnimationClip(); AssetDatabase.CreateAsset(unrelated, Folder + "/unrelated.anim");
            Object.DestroyImmediate(_root.transform.Find("FaceMotion MA Smile").gameObject);
            var replacement = new GameObject("FaceMotion MA Smile"); replacement.transform.SetParent(_root.transform);
            manifest.OwnedAssetPaths = new[] { Folder + "/unrelated.anim" };

            var removal = backend.Remove(_avatar);

            Assert.That(removal.Succeeded, Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/unrelated.anim"), Is.Not.Null);
            Assert.That(_root.transform.Find("FaceMotion MA Smile"), Is.SameAs(replacement.transform));
            Assert.That(removal.Diagnostics, Has.Some.Matches<FaceMotion.Diagnostics.FaceMotionDiagnostic>(d => d.Code == "FM-H-MA-OWNERSHIP"));
        }

        [Test]
        public void Remove_StaleGlobalIdsRecoverOnlyTheVerifiedOwnedRoot()
        {
            var backend = new ModularAvatarIntegrationBackend();
            var result = backend.Apply(backend.Plan(new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, "Stale")));
            var manifest = (ModularAvatarIntegrationManifest)result.Manifest;
            manifest.Avatar = null;
            manifest.AvatarGlobalId = "GlobalObjectId_V1-2-00000000000000000000000000000000-0-0";
            manifest.IntegrationObjectGlobalId = "GlobalObjectId_V1-2-00000000000000000000000000000000-0-0";
            ClearSessionManifests();

            var removal = new ModularAvatarIntegrationBackend().Remove(_avatar);

            Assert.That(removal.Succeeded, Is.True);
            Assert.That(_root.transform.Find("FaceMotion MA Stale"), Is.Null);
        }

        [Test]
        public void Remove_RetainsGeneratedAssetsAndUndoRestoresTheOwnedRoot()
        {
            Undo.ClearAll();
            var backend = new ModularAvatarIntegrationBackend();
            var result = backend.Apply(backend.Plan(new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, "UndoRemove")));
            var manifest = (ModularAvatarIntegrationManifest)result.Manifest;

            Assert.That(backend.Remove(_avatar).Succeeded, Is.True);
            Assert.That(_root.transform.Find("FaceMotion MA UndoRemove"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Object>(manifest.OwnedAssetPaths[0]), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Object>(manifest.OwnedAssetPaths[1]), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_UndoRemove/Manifest.asset"), Is.Not.Null);

            Undo.PerformUndo();

            Assert.That(_root.transform.Find("FaceMotion MA UndoRemove"), Is.Not.Null);
            Assert.That(new ModularAvatarIntegrationBackend().Remove(_avatar).Succeeded, Is.True);
        }

        [Test]
        public void Remove_ThenReapplyCreatesOneCleanIntegration()
        {
            var backend = new ModularAvatarIntegrationBackend();
            var request = new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, "Reapply");
            Assert.That(backend.Apply(backend.Plan(request)).Succeeded, Is.True);
            Assert.That(backend.Remove(_avatar).Succeeded, Is.True);

            var reapplied = backend.Apply(backend.Plan(request));

            Assert.That(reapplied.Succeeded, Is.True);
            Assert.That(_root.GetComponentsInChildren<ModularAvatarMergeAnimator>(true), Has.Length.EqualTo(1));
        }

        [Test]
        public void RemoveVisibility_ExistingIntegrationDoesNotRequireAClipOrPlan()
        {
            var backend = new ModularAvatarIntegrationBackend();
            Assert.That(backend.Apply(backend.Plan(new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, "Visibility"))).Succeeded, Is.True);

            Assert.That(DirectVRChatIntegrationPanel.ShouldShowModularAvatarRemove(
                IntegrationBackendSelection.ModularAvatar, backend, _avatar), Is.True);
        }

        [Test]
        public void RemoveVisibility_PersistedRecoverableIntegrationSurvivesSessionCacheLoss()
        {
            var backend = new ModularAvatarIntegrationBackend();
            var result = backend.Apply(backend.Plan(new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, "VisibleStale")));
            var manifest = (ModularAvatarIntegrationManifest)result.Manifest;
            manifest.Avatar = null;
            manifest.AvatarGlobalId = "GlobalObjectId_V1-2-00000000000000000000000000000000-0-0";
            manifest.IntegrationObjectGlobalId = "GlobalObjectId_V1-2-00000000000000000000000000000000-0-0";
            ClearSessionManifests();

            Assert.That(DirectVRChatIntegrationPanel.ShouldShowModularAvatarRemove(
                IntegrationBackendSelection.ModularAvatar, new ModularAvatarIntegrationBackend(), _avatar), Is.True);
        }

        [Test]
        public void RemoveVisibility_HidesForDirectBackendAndWithoutIntegration()
        {
            var backend = new ModularAvatarIntegrationBackend();

            Assert.That(DirectVRChatIntegrationPanel.ShouldShowModularAvatarRemove(
                IntegrationBackendSelection.ModularAvatar, backend, _avatar), Is.False);
            Assert.That(DirectVRChatIntegrationPanel.ShouldShowModularAvatarRemove(
                IntegrationBackendSelection.Direct, backend, _avatar), Is.False);
        }

        [Test]
        public void Remove_WithoutAManifestIsBlocked()
        {
            var result = new ModularAvatarIntegrationBackend().Remove(_avatar);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("FM-H-MA-MANIFEST"));
        }

        private AnimatorController CreateControllerWithClip(string name, AnimationClip clip)
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/" + name);
            controller.layers[0].stateMachine.AddState("State").motion = clip;
            return controller;
        }

        private static void ClearSessionManifests()
        {
            var field = typeof(ModularAvatarIntegrationBackend)
                .GetField("SessionManifests", BindingFlags.Static | BindingFlags.NonPublic);
            ((IDictionary)field.GetValue(null)).Clear();
        }

    }
}
