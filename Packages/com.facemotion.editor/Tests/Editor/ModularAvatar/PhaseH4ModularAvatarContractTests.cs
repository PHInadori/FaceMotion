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
using VRC.SDK3.Avatars.ScriptableObjects;

namespace FaceMotion.Editor.Tests
{
    /// <summary>Independent Phase H.4 coverage for the public MA 1.18.7 boundary.</summary>
    public sealed class PhaseH4ModularAvatarContractTests
    {
        private const string Folder = "Assets/__FaceMotionTests_H4";
        private GameObject _root;
        private VRCAvatarDescriptor _avatar;
        private AnimationClip _clip;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__FaceMotionTests_H4");
            _root = new GameObject("Avatar");
            _avatar = _root.AddComponent<VRCAvatarDescriptor>();
            _clip = new AnimationClip();
            AssetDatabase.CreateAsset(_clip, Folder + "/motion.anim");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.Refresh();
        }

        [Test]
        public void InstalledModularAvatarPackage_IsExactly1187()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ModularAvatarMergeAnimator).Assembly);

            Assert.That(package.name, Is.EqualTo("nadena.dev.modular-avatar"));
            Assert.That(package.version, Is.EqualTo("1.18.7"));
        }

        [Test]
        public void Locator_ReturnsThePackageGatedBackend()
        {
            var backend = ModularAvatarIntegrationBackendLocator.Create();

            Assert.That(backend, Is.TypeOf<ModularAvatarIntegrationBackend>());
        }

        [Test]
        public void PublicMaComponents_ExposeThe1187MembersUsedByTheBackend()
        {
            Assert.That(typeof(ModularAvatarMergeAnimator).GetField("animator"), Is.Not.Null);
            Assert.That(typeof(ModularAvatarMergeAnimator).GetField("layerType"), Is.Not.Null);
            Assert.That(typeof(ModularAvatarMergeAnimator).GetField("pathMode"), Is.Not.Null);
            Assert.That(typeof(ModularAvatarParameters).GetField("parameters"), Is.Not.Null);
            Assert.That(typeof(ModularAvatarMenuInstaller).GetField("menuToAppend"), Is.Not.Null);
            Assert.That(typeof(ModularAvatarMenuInstaller).GetField("installTargetMenu"), Is.Not.Null);
        }

        [Test]
        public void OptionalContracts_NormalizeNullsAndBlockUnavailableBackends()
        {
            var availability = new OptionalIntegrationBackendAvailability(null, false, null, false, null);
            var plan = new OptionalIntegrationPlan(null, availability, null);

            Assert.That(availability.BackendId, Is.Empty);
            Assert.That(availability.PackageVersion, Is.Empty);
            Assert.That(availability.Reason, Is.Empty);
            Assert.That(plan.Diagnostics, Is.Empty);
            Assert.That(plan.IsValid, Is.False);
        }

        [Test]
        public void OptionalPlan_RequiresAnAvailabilityValue()
        {
            Assert.That(
                () => new OptionalIntegrationPlan("modular-avatar", null, null),
                Throws.TypeOf<System.ArgumentNullException>());
        }

        [Test]
        public void OptionalMetadata_InstalledVersionIsTrimmedButNeverEnablesTheStub()
        {
            var availability = ModularAvatarOptionalBackend.EvaluatePackageMetadata(true, " 1.18.7 ");

            Assert.That(availability.PackageInstalled, Is.True);
            Assert.That(availability.PackageVersion, Is.EqualTo("1.18.7"));
            Assert.That(availability.BackendAvailable, Is.False);
        }

        [Test]
        public void OptionalMetadata_MissingVersionIsDistinctFromAnAbsentPackage()
        {
            var availability = ModularAvatarOptionalBackend.EvaluatePackageMetadata(true, " ");

            Assert.That(availability.PackageInstalled, Is.True);
            Assert.That(availability.HasPackageVersion, Is.False);
            Assert.That(availability.BackendAvailable, Is.False);
        }

        [Test]
        public void OptionalBackend_ReportsItsStableIdentityAndVersion()
        {
            var backend = new ModularAvatarOptionalBackend();

            Assert.That(backend.BackendId, Is.EqualTo("modular-avatar"));
            Assert.That(backend.BackendVersion, Is.EqualTo(1));
        }

        [Test]
        public void Request_PreservesTheBackendInputsWithoutTranslation()
        {
            var request = new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, "Smile");

            Assert.That(request.Avatar, Is.SameAs(_avatar));
            Assert.That(request.Clip, Is.SameAs(_clip));
            Assert.That(request.OutputFolder, Is.EqualTo(Folder));
            Assert.That(request.DisplayName, Is.EqualTo("Smile"));
        }

        [Test]
        public void Result_NormalizesNullDiagnosticsWithoutChangingItsOutcome()
        {
            var result = new ModularAvatarIntegrationResult(false, null, null);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Manifest, Is.Null);
            Assert.That(result.Diagnostics, Is.Empty);
        }

        [Test]
        public void ModularAvatarPlan_OnlyBlockingDiagnosticsDetermineValidity()
        {
            var request = new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, "Smile");
            var informational = new FaceMotionDiagnostic("FM-H4-INFO", FaceMotionDiagnosticSeverity.Info, "info", string.Empty, false, string.Empty);
            var blocking = new FaceMotionDiagnostic("FM-H4-ERROR", FaceMotionDiagnosticSeverity.Error, "error", string.Empty, true, string.Empty);

            Assert.That(new ModularAvatarIntegrationPlan(request, "FaceMotion_Smile", "FaceMotion MA Smile", new[] { informational }).IsValid, Is.True);
            Assert.That(new ModularAvatarIntegrationPlan(request, "FaceMotion_Smile", "FaceMotion MA Smile", new[] { blocking }).IsValid, Is.False);
        }

        [Test]
        public void Plan_ValidRequestIsPureAndProposesStableNames()
        {
            var backend = new ModularAvatarIntegrationBackend();
            var plan = backend.Plan(new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, "Smile Test"));

            Assert.That(plan.IsValid, Is.True);
            Assert.That(plan.ParameterName, Is.EqualTo("FaceMotion_Smile_Test"));
            Assert.That(plan.ObjectName, Is.EqualTo("FaceMotion MA Smile_Test"));
            Assert.That(_root.transform.childCount, Is.EqualTo(0));
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/FaceMotionMA_Smile_Test"), Is.False);
        }

        [Test]
        public void Apply_NullPlanReturnsAStableBlockingDiagnostic()
        {
            var result = new ModularAvatarIntegrationBackend().Apply(null);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Manifest, Is.Null);
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("FM-H-MA-PLAN"));
        }

        [Test]
        public void Apply_BlockedPlanReturnsTheOriginalDiagnosticsWithoutMutation()
        {
            var backend = new ModularAvatarIntegrationBackend();
            var plan = backend.Plan(new ModularAvatarIntegrationRequest(_avatar, null, Folder, "Smile"));

            var result = backend.Apply(plan);

            Assert.That(plan.IsValid, Is.False);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostics, Is.SameAs(plan.Diagnostics));
            Assert.That(_root.transform.childCount, Is.EqualTo(0));
        }

        [Test]
        public void Apply_DoesNotMutateDescriptorOwnedAssets()
        {
            var fx = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/original.controller");
            var parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>(); AssetDatabase.CreateAsset(parameters, Folder + "/original-parameters.asset");
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>(); AssetDatabase.CreateAsset(menu, Folder + "/original-menu.asset");
            _avatar.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, animatorController = fx } };
            _avatar.expressionParameters = parameters;
            _avatar.expressionsMenu = menu;

            var result = Apply("Smile");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(_avatar.baseAnimationLayers[0].animatorController, Is.SameAs(fx));
            Assert.That(_avatar.expressionParameters, Is.SameAs(parameters));
            Assert.That(_avatar.expressionsMenu, Is.SameAs(menu));
        }

        [Test]
        public void Apply_CreatesPublicMaComponentsWithExactConfiguration()
        {
            var result = Apply("Smile");
            var node = _root.transform.Find("FaceMotion MA Smile").gameObject;
            var merge = node.GetComponent<ModularAvatarMergeAnimator>();
            var parameters = node.GetComponent<ModularAvatarParameters>();
            var installer = node.GetComponent<ModularAvatarMenuInstaller>();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(merge.layerType, Is.EqualTo(VRCAvatarDescriptor.AnimLayerType.FX));
            Assert.That(merge.pathMode, Is.EqualTo(MergeAnimatorPathMode.Absolute));
            Assert.That(merge.deleteAttachedAnimator, Is.False);
            Assert.That(merge.matchAvatarWriteDefaults, Is.False);
            Assert.That(parameters.parameters[0].nameOrPrefix, Is.EqualTo("FaceMotion_Smile"));
            Assert.That(parameters.parameters[0].syncType, Is.EqualTo(ParameterSyncType.Bool));
            Assert.That(parameters.parameters[0].saved, Is.False);
            Assert.That(installer.installTargetMenu, Is.Null);
        }

        [Test]
        public void Apply_CreatesAnimatorAndMenuTopologyForTheGeneratedParameter()
        {
            Apply("Smile");
            var node = _root.transform.Find("FaceMotion MA Smile").gameObject;
            var controller = node.GetComponent<ModularAvatarMergeAnimator>().animator as AnimatorController;
            var layer = controller.layers[1];
            var states = layer.stateMachine.states;
            var menu = node.GetComponent<ModularAvatarMenuInstaller>().menuToAppend;

            Assert.That(controller.parameters, Has.Some.Matches<AnimatorControllerParameter>(p => p.name == "FaceMotion_Smile" && p.type == AnimatorControllerParameterType.Bool));
            Assert.That(layer.name, Is.EqualTo("FaceMotion MA Smile"));
            Assert.That(states, Has.Some.Matches<ChildAnimatorState>(s => s.state.name == "Off" && s.state.writeDefaultValues == false));
            Assert.That(states, Has.Some.Matches<ChildAnimatorState>(s => s.state.name == "On" && s.state.motion == _clip && s.state.writeDefaultValues == false));
            Assert.That(menu.controls, Has.Count.EqualTo(1));
            Assert.That(menu.controls[0].type, Is.EqualTo(VRCExpressionsMenu.Control.ControlType.Toggle));
            Assert.That(menu.controls[0].parameter.name, Is.EqualTo("FaceMotion_Smile"));
        }

        [Test]
        public void Apply_ReapplyKeepsOneUntargetedRootMenuInstaller()
        {
            var rootMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            AssetDatabase.CreateAsset(rootMenu, Folder + "/original-menu.asset");
            _avatar.expressionsMenu = rootMenu;

            Assert.That(Apply("Smile").Succeeded, Is.True);
            Assert.That(Apply("Smile").Succeeded, Is.True);

            var installers = _root.GetComponentsInChildren<ModularAvatarMenuInstaller>(true);
            Assert.That(installers, Has.Length.EqualTo(1));
            Assert.That(installers[0].installTargetMenu, Is.Null);
            Assert.That(installers[0].menuToAppend.controls, Has.Count.EqualTo(1));
            Assert.That(installers[0].menuToAppend.controls[0].parameter.name, Is.EqualTo("FaceMotion_Smile"));
            Assert.That(_avatar.expressionsMenu, Is.SameAs(rootMenu));
        }

        [Test]
        public void Apply_PersistsTheControllerMenuAndResetClipAsOwnedAssets()
        {
            var result = Apply("Smile");
            var manifest = (ModularAvatarIntegrationManifest)result.Manifest;

            Assert.That(manifest.OwnedAssetPaths, Is.EquivalentTo(new[]
            {
                Folder + "/FaceMotionMA_Smile/FX.controller",
                Folder + "/FaceMotionMA_Smile/Menu.asset",
                Folder + "/FaceMotionMA_Smile/Reset.anim"
            }));
            Assert.That(AssetDatabase.GetAssetPath(manifest), Is.EqualTo(Folder + "/FaceMotionMA_Smile/Manifest.asset"));
        }

        [Test]
        public void Apply_CreatesAnOffResetClipUsingTheAvatarBaselineForOwnedBindings()
        {
            var face = new GameObject("Face"); face.transform.SetParent(_root.transform); face.transform.localPosition = new Vector3(1f, 2f, 3f); face.transform.localRotation = Quaternion.Euler(10f, 20f, 30f); face.transform.localScale = new Vector3(2f, 3f, 4f);
            var mesh = new Mesh(); mesh.AddBlendShapeFrame("Smile", 100f, new Vector3[0], new Vector3[0], new Vector3[0]);
            var renderer = face.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh; renderer.SetBlendShapeWeight(0, 42f);
            SetCurve("Face", typeof(SkinnedMeshRenderer), "blendShape.Smile");
            SetCurve("Face", typeof(Transform), "m_LocalPosition.x"); SetCurve("Face", typeof(Transform), "m_LocalPosition.y"); SetCurve("Face", typeof(Transform), "m_LocalPosition.z");
            SetCurve("Face", typeof(Transform), "m_LocalRotation.x"); SetCurve("Face", typeof(Transform), "m_LocalRotation.y"); SetCurve("Face", typeof(Transform), "m_LocalRotation.z"); SetCurve("Face", typeof(Transform), "m_LocalRotation.w");
            SetCurve("Face", typeof(Transform), "m_LocalScale.x"); SetCurve("Face", typeof(Transform), "m_LocalScale.y"); SetCurve("Face", typeof(Transform), "m_LocalScale.z");

            Apply("Smile");

            var controller = _root.transform.Find("FaceMotion MA Smile").GetComponent<ModularAvatarMergeAnimator>().animator as AnimatorController;
            var off = System.Array.Find(controller.layers[1].stateMachine.states, s => s.state.name == "Off").state;
            var reset = off.motion as AnimationClip;
            Assert.That(reset, Is.Not.Null);
            Assert.That(CurveValue(reset, "Face", typeof(SkinnedMeshRenderer), "blendShape.Smile"), Is.EqualTo(42f));
            Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalPosition.x"), Is.EqualTo(1f));
            Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalPosition.y"), Is.EqualTo(2f));
            Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalPosition.z"), Is.EqualTo(3f));
            Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalRotation.x"), Is.EqualTo(face.transform.localRotation.x));
            Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalRotation.y"), Is.EqualTo(face.transform.localRotation.y));
            Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalRotation.z"), Is.EqualTo(face.transform.localRotation.z));
            Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalRotation.w"), Is.EqualTo(face.transform.localRotation.w));
            Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalScale.x"), Is.EqualTo(2f));
            Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalScale.y"), Is.EqualTo(3f));
            Assert.That(CurveValue(reset, "Face", typeof(Transform), "m_LocalScale.z"), Is.EqualTo(4f));
        }

        [Test]
        public void Apply_CreatesBidirectionalBooleanTransitions()
        {
            Apply("Smile");
            var controller = _root.transform.Find("FaceMotion MA Smile").GetComponent<ModularAvatarMergeAnimator>().animator as AnimatorController;
            var states = controller.layers[1].stateMachine.states;
            AnimatorState off = null;
            AnimatorState on = null;
            for (var i = 0; i < states.Length; i++)
            {
                if (states[i].state.name == "Off") off = states[i].state;
                if (states[i].state.name == "On") on = states[i].state;
            }

            Assert.That(off.transitions, Has.Length.EqualTo(1));
            Assert.That(off.transitions[0].conditions[0].mode, Is.EqualTo(AnimatorConditionMode.If));
            Assert.That(off.transitions[0].duration, Is.Zero);
            Assert.That(on.transitions, Has.Length.EqualTo(1));
            Assert.That(on.transitions[0].conditions[0].mode, Is.EqualTo(AnimatorConditionMode.IfNot));
            Assert.That(on.transitions[0].duration, Is.Zero);
            Assert.That(off.transitions[0].conditions[0].parameter, Is.EqualTo("FaceMotion_Smile"));
            Assert.That(on.transitions[0].conditions[0].parameter, Is.EqualTo("FaceMotion_Smile"));
        }

        [Test]
        public void Replan_ForTheSameManagedNameRemainsValid()
        {
            Apply("Smile");

            var plan = new ModularAvatarIntegrationBackend().Plan(new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, "Smile"));

            Assert.That(plan.IsValid, Is.True);
            Assert.That(plan.Diagnostics, Is.Empty);
        }

        [Test]
        public void Replan_ForADifferentManagedNameBlocksManifestReplacement()
        {
            Apply("Smile");

            var plan = new ModularAvatarIntegrationBackend().Plan(new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, "Frown"));

            Assert.That(plan.Diagnostics, Has.Some.Matches<FaceMotionDiagnostic>(d => d.Code == "FM-H-MA-MANIFEST-CONFLICT"));
        }

        [Test]
        public void Remove_RejectsTraversalInTamperedOwnedAssetPath()
        {
            var result = Apply("Smile");
            var manifest = (ModularAvatarIntegrationManifest)result.Manifest;
            var unrelated = new AnimationClip(); AssetDatabase.CreateAsset(unrelated, Folder + "/unrelated.anim");
            manifest.OwnedAssetPaths = new[] { Folder + "/FaceMotionMA_Smile/../unrelated.anim" };

            var removal = new ModularAvatarIntegrationBackend().Remove(_avatar);

            Assert.That(removal.Succeeded, Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/unrelated.anim"), Is.Not.Null);
            Assert.That(removal.Diagnostics, Has.Some.Matches<FaceMotionDiagnostic>(d => d.Code == "FM-H-MA-OWNERSHIP"));
        }

        [Test]
        public void Remove_DeletesOnlyTheManagedChildAndPreservesAvatarSiblings()
        {
            Apply("Smile");
            var sibling = new GameObject("User Owned Child");
            sibling.transform.SetParent(_root.transform);

            var removal = new ModularAvatarIntegrationBackend().Remove(_avatar);

            Assert.That(removal.Succeeded, Is.True);
            Assert.That(_root.transform.Find("FaceMotion MA Smile"), Is.Null);
            Assert.That(_root.transform.Find("User Owned Child"), Is.SameAs(sibling.transform));
        }

        [Test]
        public void Remove_RetainsGeneratedAssets()
        {
            Apply("Smile");

            var removal = new ModularAvatarIntegrationBackend().Remove(_avatar);

            Assert.That(removal.Succeeded, Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/FaceMotionMA_Smile/FX.controller"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(Folder + "/FaceMotionMA_Smile/Menu.asset"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/FaceMotionMA_Smile/Reset.anim"), Is.Not.Null);
        }

        [Test]
        public void Remove_RetainsManifest()
        {
            Apply("Smile");
            var manifest = AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Smile/Manifest.asset");

            Assert.That(new ModularAvatarIntegrationBackend().Remove(_avatar).Succeeded, Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Smile/Manifest.asset"), Is.Not.Null);
            Assert.That(new ModularAvatarIntegrationBackend().HasExistingIntegration(_avatar), Is.True);
            Assert.That(manifest, Is.Not.Null);
        }

        [Test]
        public void ReapplyAfterRemove_ReusesExistingFxController()
        {
            Apply("Smile");

            Assert.That(new ModularAvatarIntegrationBackend().Remove(_avatar).Succeeded, Is.True);
            Assert.That(Apply("Smile").Succeeded, Is.True);

            var controller = _root.transform.Find("FaceMotion MA Smile").GetComponent<ModularAvatarMergeAnimator>().animator;
            Assert.That(AssetDatabase.GetAssetPath(controller), Is.EqualTo(Folder + "/FaceMotionMA_Smile/FX.controller"));
        }

        [Test]
        public void ReapplyAfterRemove_ReusesExistingMenu()
        {
            Apply("Smile");

            Assert.That(new ModularAvatarIntegrationBackend().Remove(_avatar).Succeeded, Is.True);
            Assert.That(Apply("Smile").Succeeded, Is.True);

            var menu = _root.transform.Find("FaceMotion MA Smile").GetComponent<ModularAvatarMenuInstaller>().menuToAppend;
            Assert.That(AssetDatabase.GetAssetPath(menu), Is.EqualTo(Folder + "/FaceMotionMA_Smile/Menu.asset"));
        }

        [Test]
        public void ReapplyAfterRemove_ReusesExistingResetClip()
        {
            Apply("Smile");

            Assert.That(new ModularAvatarIntegrationBackend().Remove(_avatar).Succeeded, Is.True);
            Assert.That(Apply("Smile").Succeeded, Is.True);

            var controller = _root.transform.Find("FaceMotion MA Smile").GetComponent<ModularAvatarMergeAnimator>().animator as AnimatorController;
            var off = System.Array.Find(controller.layers[1].stateMachine.states, s => s.state.name == "Off").state;
            Assert.That(AssetDatabase.GetAssetPath(off.motion), Is.EqualTo(Folder + "/FaceMotionMA_Smile/Reset.anim"));
        }

        [Test]
        public void ReapplyAfterRemove_DoesNotCreateSecondGeneratedFolder()
        {
            Apply("Smile");
            Assert.That(new ModularAvatarIntegrationBackend().Remove(_avatar).Succeeded, Is.True);

            Assert.That(Apply("Smile").Succeeded, Is.True);

            var dirs = System.IO.Directory.GetDirectories(Folder, "FaceMotionMA_Smile*");
            Assert.That(dirs, Has.Length.EqualTo(1));
            Assert.That(AssetDatabase.IsValidFolder(Folder + "/FaceMotionMA_Smile"), Is.True);
        }

        [Test]
        public void ReapplyAfterRemove_DoesNotDuplicateHierarchyRoot()
        {
            Apply("Smile");
            Assert.That(new ModularAvatarIntegrationBackend().Remove(_avatar).Succeeded, Is.True);

            Assert.That(Apply("Smile").Succeeded, Is.True);

            Assert.That(_root.GetComponentsInChildren<ModularAvatarMergeAnimator>(true), Has.Length.EqualTo(1));
            Assert.That(_root.transform.Find("FaceMotion MA Smile"), Is.Not.Null);
        }

        [Test]
        public void Reapply_PreservesForeignAssetBesideGeneratedAssets()
        {
            Apply("Smile");
            var foreign = new AnimationClip(); AssetDatabase.CreateAsset(foreign, Folder + "/FaceMotionMA_Smile/MyUserMemo.anim");
            Assert.That(new ModularAvatarIntegrationBackend().Remove(_avatar).Succeeded, Is.True);

            Assert.That(Apply("Smile").Succeeded, Is.True);

            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/FaceMotionMA_Smile/MyUserMemo.anim"), Is.Not.Null);
            AssetDatabase.DeleteAsset(Folder + "/FaceMotionMA_Smile/MyUserMemo.anim");
        }

        [Test]
        public void Reapply_TamperedManifestDoesNotOverwriteForeignAsset()
        {
            Apply("Smile");
            var foreign = new AnimationClip(); AssetDatabase.CreateAsset(foreign, Folder + "/FaceMotionMA_Smile/MyUserMemo.anim");
            var manifest = AssetDatabase.LoadAssetAtPath<ModularAvatarIntegrationManifest>(Folder + "/FaceMotionMA_Smile/Manifest.asset");
            manifest.OwnedAssetPaths = new[] { Folder + "/FaceMotionMA_Smile/FX.controller", Folder + "/FaceMotionMA_Smile/Menu.asset", Folder + "/FaceMotionMA_Smile/Reset.anim", Folder + "/FaceMotionMA_Smile/MyUserMemo.anim" };

            var reapplied = Apply("Smile");

            Assert.That(reapplied.Succeeded, Is.False);
            Assert.That(reapplied.Diagnostics, Has.Some.Matches<FaceMotionDiagnostic>(d => d.Code == "FM-OWNERSHIP-TAMPERED"));
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/FaceMotionMA_Smile/MyUserMemo.anim"), Is.Not.Null);
            Assert.That(_root.GetComponentsInChildren<ModularAvatarMergeAnimator>(true), Has.Length.EqualTo(1));
            AssetDatabase.DeleteAsset(Folder + "/FaceMotionMA_Smile/MyUserMemo.anim");
        }

        private ModularAvatarIntegrationResult Apply(string displayName)
        {
            var backend = new ModularAvatarIntegrationBackend();
            return backend.Apply(backend.Plan(new ModularAvatarIntegrationRequest(_avatar, _clip, Folder, displayName)));
        }

        private void SetCurve(string path, System.Type type, string property) { _clip.SetCurve(path, type, property, AnimationCurve.Constant(0f, 1f, 100f)); }
        private static float CurveValue(AnimationClip clip, string path, System.Type type, string property) { return AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property)).Evaluate(0f); }
    }
}
